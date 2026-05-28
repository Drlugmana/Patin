using TurneroTcs.Services.RotacionV2.Constraints;
using TurneroTcs.Services.RotacionV2.Domain;
using TurneroTcs.Services.RotacionV2.Model;
using System.Diagnostics;
using System.Text;

namespace TurneroTcs.Services.RotacionV2.Solver;

/// <summary>
/// Resuelve el horizonte de planificación completo semana a semana, acumulando el estado
/// de cada semana confirmada para usarlo como contexto en la siguiente.
/// <para>
/// La estrategia de resolución por ventana semanal tiene tres fases por semana:
/// <list type="number">
///   <item>
///     <b>Búsqueda de factibilidad rápida</b>: se construye el modelo sin función objetivo
///     y se lanza con un portafolio de semillas aleatorias, deteniéndose en la primera solución factible.
///   </item>
///   <item>
///     <b>Optimización guiada</b>: con la solución factible como sugerencia inicial, se reconstruye
///     el modelo con la función objetivo completa y se busca mejorar la calidad con el tiempo restante.
///   </item>
///   <item>
///     <b>Estrategias de fallback</b>: si la semana resulta infactible, se reintenta relajando
///     el descanso mínimo a 7 horas o permitiendo sobrecupo en feriado según las opciones configuradas.
///   </item>
/// </list>
/// El proceso acumula las asignaciones confirmadas de cada semana y actualiza el
/// <see cref="EstadoResolucionSemanal"/> para garantizar la continuidad de restricciones cross-semana.
/// </para>
/// </summary>
public sealed class ResolvedorSecuencialSemanalRotacion
{
    private const int DescansoMinimoFallbackMinutos = 7 * 60;
    private readonly ConstructorModeloCp _constructorModelo = new();
    private readonly ResolvedorCpSatRotacion _resolvedorSemanal = new();
    private sealed record PresupuestoDescanso7Horas(int MaximoGlobal, int MaximoPorEmpleado);

    /// <summary>
    /// Resuelve el horizonte de planificación completo y devuelve la solución acumulada de todas las semanas.
    /// Si alguna semana no puede resolverse, la resolución se interrumpe y se devuelve el estado de error.
    /// </summary>
    /// <param name="problema">Problema de rotación completo con empleados, slots, ausencias y reglas.</param>
    /// <param name="opciones">Parámetros de ejecución del motor. Si es <see langword="null"/>, se usan los predeterminados.</param>
    /// <returns>
    /// Solución acumulada con el estado global (<see cref="EstadoSolucionRotacion.Optima"/> si todas las semanas
    /// fueron óptimas, <see cref="EstadoSolucionRotacion.Factible"/> si alguna no fue óptima)
    /// y las asignaciones de todo el horizonte.
    /// </returns>
    public SolucionRotacionCp Resolver(
        ProblemaRotacion problema,
        OpcionesSolverRotacion? opciones = null,
        EstadoResolucionSemanal? estadoInicial = null)
    {
        ArgumentNullException.ThrowIfNull(problema);

        problema = AplicarNivelEvitarFinesSemanaConsecutivos(problema, opciones);
        problema = AplicarNivelAgruparTiposTurnoConsecutivos(problema, opciones);

        var estado = ClonarEstadoInicial(estadoInicial);
        var asignacionesAcumuladas = new List<AsignacionSlot>();
        var detalleSemanas = new List<string>();
        var huboFactibleNoOptimo = false;
        var mantenerDescanso7Horas = false;
        var presupuestoDescanso7Horas = CrearPresupuestoDescanso7Horas(problema, opciones);

        for (var indiceSemana = 0; indiceSemana < problema.CantidadSemanas; indiceSemana++)
        {
            var semanasVentana = DeterminarSemanasVentana(problema, indiceSemana);
            var problemaVentana = ConstructorProblemaSemanal.CrearVentana(problema, indiceSemana, semanasVentana);
            var problemaVentanaResuelto = problemaVentana;

            if (DebeUsarDescanso7HorasDesdeInicio(problemaVentanaResuelto, opciones, mantenerDescanso7Horas))
            {
                var descansoOriginal = problemaVentanaResuelto.Reglas.Obligatorias.MinutosMinimosDescansoEntreTurnos;
                ReportarDiagnostico(
                    opciones,
                    $"RotacionV2 semana S{indiceSemana + 1} descanso 7h preventivo: nivel={opciones?.NivelUsoDescanso7Horas ?? NivelUsoDescanso7Horas.Bajo} original={descansoOriginal}min maxGlobal={presupuestoDescanso7Horas.MaximoGlobal} maxPersona={presupuestoDescanso7Horas.MaximoPorEmpleado}");

                problemaVentanaResuelto = CrearProblemaConDescansoMinimo(problemaVentanaResuelto, DescansoMinimoFallbackMinutos, presupuestoDescanso7Horas);
            }

            ReportarDiagnostico(
                opciones,
                ConstruirDiagnosticoInicioSemana(problemaVentanaResuelto, estado, indiceSemana));

            var solucionVentana = ResolverVentana(problemaVentanaResuelto, estado, opciones);

            if (DebeReintentarConFallbackDescansoMinimo(problemaVentanaResuelto, solucionVentana, opciones))
            {
                var descansoOriginal = problemaVentanaResuelto.Reglas.Obligatorias.MinutosMinimosDescansoEntreTurnos;
                ReportarDiagnostico(
                    opciones,
                    $"RotacionV2 semana S{indiceSemana + 1} fallback descanso: base={solucionVentana.Estado}/{solucionVentana.DetalleEstado} reintento={DescansoMinimoFallbackMinutos}min original={descansoOriginal}min maxGlobal={presupuestoDescanso7Horas.MaximoGlobal} maxPersona={presupuestoDescanso7Horas.MaximoPorEmpleado}");

                problemaVentanaResuelto = CrearProblemaConDescansoMinimo(problemaVentanaResuelto, DescansoMinimoFallbackMinutos, presupuestoDescanso7Horas);
                ReportarDiagnostico(
                    opciones,
                    ConstruirDiagnosticoInicioSemana(problemaVentanaResuelto, estado, indiceSemana));

                var solucionFallback = ResolverVentana(problemaVentanaResuelto, estado, opciones);
                solucionVentana = MarcarDetalleFallbackDescansoMinimo(solucionFallback, DescansoMinimoFallbackMinutos);

                if (solucionVentana.Estado is EstadoSolucionRotacion.Optima or EstadoSolucionRotacion.Factible &&
                    opciones?.NivelUsoDescanso7Horas == NivelUsoDescanso7Horas.Medio)
                {
                    mantenerDescanso7Horas = true;
                }
            }

            if (DebeReintentarConSobrecupoFeriado(problemaVentanaResuelto, solucionVentana, opciones))
            {
                ReportarDiagnostico(
                    opciones,
                    $"RotacionV2 semana S{indiceSemana + 1} fallback feriado: base={solucionVentana.Estado}/{solucionVentana.DetalleEstado} permitir objetivo semanal flexible por feriado");

                problemaVentanaResuelto = CrearProblemaConSobrecupoFeriado(problemaVentanaResuelto);
                ReportarDiagnostico(
                    opciones,
                    ConstruirDiagnosticoInicioSemana(problemaVentanaResuelto, estado, indiceSemana));

                var solucionSobrecupo = ResolverVentana(problemaVentanaResuelto, estado, opciones);
                solucionVentana = MarcarDetalleFallbackSobrecupoFeriado(solucionSobrecupo);
            }

            if (DebeReintentarSinNocturnoHistorial(solucionVentana, estado))
            {
                ReportarDiagnostico(
                    opciones,
                    $"RotacionV2 semana S{indiceSemana + 1} fallback nocturno-historial: base={solucionVentana.Estado}/{solucionVentana.DetalleEstado} reintentando sin restricción nocturno-historial");

                var estadoSinNocturnoHistorial = ClonarEstadoSinNocturnoHistorial(estado);
                ReportarDiagnostico(
                    opciones,
                    ConstruirDiagnosticoInicioSemana(problemaVentanaResuelto, estadoSinNocturnoHistorial, indiceSemana));

                var solucionFallbackNH = ResolverVentana(problemaVentanaResuelto, estadoSinNocturnoHistorial, opciones);
                solucionVentana = MarcarDetalleFallbackNocturnoHistorial(solucionFallbackNH);
            }

            detalleSemanas.Add($"S{indiceSemana + 1}={solucionVentana.Estado}/{solucionVentana.DetalleEstado}");
            ReportarDiagnostico(
                opciones,
                $"RotacionV2 semana S{indiceSemana + 1}: estado={solucionVentana.Estado} detalle={solucionVentana.DetalleEstado} asignaciones={solucionVentana.Asignaciones.Count} ventana={semanasVentana} descansoMin={problemaVentanaResuelto.Reglas.Obligatorias.MinutosMinimosDescansoEntreTurnos}");

            if (solucionVentana.Estado is not EstadoSolucionRotacion.Optima and not EstadoSolucionRotacion.Factible)
            {
                ReportarDiagnostico(
                    opciones,
                    ConstruirDiagnosticoFallaSemana(problemaVentanaResuelto, estado, indiceSemana, solucionVentana));
                return CrearSolucionAcumulada(
                    problema,
                    asignacionesAcumuladas,
                    solucionVentana.Estado,
                    string.Join(", ", detalleSemanas));
            }

            if (solucionVentana.Estado == EstadoSolucionRotacion.Factible)
            {
                huboFactibleNoOptimo = true;
            }

            var asignacionesSemanaConfirmada = ExtraerAsignacionesSemanaConfirmada(problemaVentanaResuelto, solucionVentana);
            asignacionesAcumuladas.AddRange(asignacionesSemanaConfirmada);
            ActualizarEstadoSemanal(estado, problemaVentanaResuelto, asignacionesSemanaConfirmada);
        }

        return CrearSolucionAcumulada(
            problema,
            asignacionesAcumuladas,
            huboFactibleNoOptimo ? EstadoSolucionRotacion.Factible : EstadoSolucionRotacion.Optima,
            string.Join(", ", detalleSemanas));
    }

    private SolucionRotacionCp ResolverVentana(
        ProblemaRotacion problemaVentana,
        EstadoResolucionSemanal estado,
        OpcionesSolverRotacion? opciones)
    {
        var opcionesBase = opciones ?? new OpcionesSolverRotacion();
        var reloj = Stopwatch.StartNew();

        var (solucionFactible, semillaFactible) = BuscarSolucionFactible(problemaVentana, estado, opcionesBase, reloj);
        if (solucionFactible.Estado is not EstadoSolucionRotacion.Optima and not EstadoSolucionRotacion.Factible)
        {
            return solucionFactible;
        }

        var tiempoRestante = opcionesBase.TiempoMaximoResolucion - reloj.Elapsed;
        if (tiempoRestante <= TimeSpan.FromSeconds(1))
        {
            return ReemplazarDetalle(solucionFactible, $"{solucionFactible.DetalleEstado}/sin_opt");
        }

        var contextoOptimizado = _constructorModelo.Construir(
            problemaVentana,
            incluirObjetivo: true,
            sugerenciaInicial: solucionFactible.Asignaciones,
            estadoSemanalAcumulado: estado);
        RestriccionEstadoSemanalAcumulado.Aplicar(contextoOptimizado, estado);

        var solucionOptimizada = _resolvedorSemanal.Resolver(
            contextoOptimizado,
            opcionesBase with
            {
                TiempoMaximoResolucion = tiempoRestante,
                SemillaAleatoria = semillaFactible
            });

        if (solucionOptimizada.Estado is EstadoSolucionRotacion.Optima or EstadoSolucionRotacion.Factible)
        {
            return ReemplazarDetalle(solucionOptimizada, $"Hinted(seed={semillaFactible},{solucionOptimizada.DetalleEstado})");
        }

        return ReemplazarDetalle(
            solucionFactible,
            $"{solucionFactible.DetalleEstado}/fallback_opt:{solucionOptimizada.DetalleEstado}");
    }

    private static EstadoResolucionSemanal ClonarEstadoInicial(EstadoResolucionSemanal? estadoInicial)
    {
        if (estadoInicial is null)
        {
            return new EstadoResolucionSemanal();
        }

        var estado = new EstadoResolucionSemanal();

        foreach (var empleado in estadoInicial.EmpleadosConFinSemanaAnterior)
        {
            estado.EmpleadosConFinSemanaAnterior.Add(empleado);
        }

        foreach (var kvp in estadoInicial.RachaFinesSemanaConsecutivosPorEmpleado)
        {
            estado.RachaFinesSemanaConsecutivosPorEmpleado[kvp.Key] = kvp.Value;
        }

        foreach (var empleado in estadoInicial.EmpleadosConNocturnoUltimoDiaAnterior)
        {
            estado.EmpleadosConNocturnoUltimoDiaAnterior.Add(empleado);
        }

        foreach (var kvp in estadoInicial.UltimoFinTurnoPorEmpleado)
        {
            estado.UltimoFinTurnoPorEmpleado[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in estadoInicial.TurnosNocturnosPorEmpleadoMes)
        {
            estado.TurnosNocturnosPorEmpleadoMes[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in estadoInicial.SlotsFinSemanaPorEmpleadoMes)
        {
            estado.SlotsFinSemanaPorEmpleadoMes[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in estadoInicial.MinutosNocturnosAcumuladosPorEmpleado)
        {
            estado.MinutosNocturnosAcumuladosPorEmpleado[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in estadoInicial.MinutosFinSemanaAcumuladosPorEmpleado)
        {
            estado.MinutosFinSemanaAcumuladosPorEmpleado[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in estadoInicial.MinutosTotalesAcumuladosPorEmpleado)
        {
            estado.MinutosTotalesAcumuladosPorEmpleado[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in estadoInicial.MinutosFeriadoAcumuladosPorEmpleado)
        {
            estado.MinutosFeriadoAcumuladosPorEmpleado[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in estadoInicial.Descansos7HorasAcumuladosPorEmpleado)
        {
            estado.Descansos7HorasAcumuladosPorEmpleado[kvp.Key] = kvp.Value;
        }

        estado.Descansos7HorasAcumuladosTotal = estadoInicial.Descansos7HorasAcumuladosTotal;

        foreach (var kvp in estadoInicial.EmpleadosGrupoEspecialSemanaAnterior)
        {
            estado.EmpleadosGrupoEspecialSemanaAnterior[kvp.Key] = new HashSet<string>(kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        foreach (var kvp in estadoInicial.EmpleadosGrupoEspecialCicloActual)
        {
            estado.EmpleadosGrupoEspecialCicloActual[kvp.Key] = new HashSet<string>(kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        foreach (var kvp in estadoInicial.UsosGrupoEspecialPorEmpleado)
        {
            estado.UsosGrupoEspecialPorEmpleado[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in estadoInicial.RachaNocturnaBordeHistoriaPorEmpleado)
        {
            estado.RachaNocturnaBordeHistoriaPorEmpleado[kvp.Key] = kvp.Value;
        }

        return estado;
    }

    private (SolucionRotacionCp Solucion, int Semilla) BuscarSolucionFactible(
        ProblemaRotacion problemaVentana,
        EstadoResolucionSemanal estado,
        OpcionesSolverRotacion opciones,
        Stopwatch reloj)
    {
        var semillas = ConstruirPortfolioSemillas(opciones.SemillaAleatoria).ToArray();
        SolucionRotacionCp? ultimaSolucion = null;

        for (var indice = 0; indice < semillas.Length; indice++)
        {
            var tiempoRestante = opciones.TiempoMaximoResolucion - reloj.Elapsed;
            if (tiempoRestante <= TimeSpan.Zero)
            {
                break;
            }

            var semillasRestantes = semillas.Length - indice;
            var tiempoIntento = TimeSpan.FromSeconds(Math.Max(1, Math.Floor(tiempoRestante.TotalSeconds / semillasRestantes)));
            var contexto = _constructorModelo.Construir(
                problemaVentana,
                incluirObjetivo: false,
                estadoSemanalAcumulado: estado);
            RestriccionEstadoSemanalAcumulado.Aplicar(contexto, estado);

            var solucion = _resolvedorSemanal.Resolver(
                contexto,
                opciones with
                {
                    TiempoMaximoResolucion = tiempoIntento,
                    SemillaAleatoria = semillas[indice]
                },
                detenerEnPrimeraSolucion: true);

            if (solucion.Estado is EstadoSolucionRotacion.Optima or EstadoSolucionRotacion.Factible)
            {
                return (ReemplazarDetalle(solucion, $"FeasibleFirst(seed={semillas[indice]})"), semillas[indice]);
            }

            ultimaSolucion = solucion;
            if (solucion.Estado is EstadoSolucionRotacion.Infactible or EstadoSolucionRotacion.ModeloInvalido or EstadoSolucionRotacion.Error)
            {
                return (solucion, semillas[indice]);
            }
        }

        return (ultimaSolucion ?? new SolucionRotacionCp
        {
            Estado = EstadoSolucionRotacion.NoResuelta,
            DetalleEstado = "Unknown"
        }, opciones.SemillaAleatoria);
    }

    private static void ReportarDiagnostico(OpcionesSolverRotacion? opciones, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
        {
            return;
        }

        opciones?.ReportarDiagnostico?.Invoke(mensaje);
    }

    private static IEnumerable<int> ConstruirPortfolioSemillas(int semillaBase)
    {
        yield return semillaBase;
        yield return semillaBase + 17;
        yield return semillaBase + 101;
        yield return semillaBase + 503;
    }

    private static bool DebeReintentarConFallbackDescansoMinimo(
        ProblemaRotacion problemaVentana,
        SolucionRotacionCp solucionVentana,
        OpcionesSolverRotacion? opciones)
    {
        return solucionVentana.Estado == EstadoSolucionRotacion.Infactible &&
               opciones?.NivelUsoDescanso7Horas != NivelUsoDescanso7Horas.NoUsar &&
               problemaVentana.Reglas.Obligatorias.MinutosMinimosDescansoEntreTurnos > DescansoMinimoFallbackMinutos;
    }

    private static bool DebeUsarDescanso7HorasDesdeInicio(
        ProblemaRotacion problemaVentana,
        OpcionesSolverRotacion? opciones,
        bool mantenerDescanso7Horas)
    {
        if (problemaVentana.Reglas.Obligatorias.MinutosMinimosDescansoEntreTurnos <= DescansoMinimoFallbackMinutos)
        {
            return false;
        }

        return opciones?.NivelUsoDescanso7Horas switch
        {
            NivelUsoDescanso7Horas.Alto => true,
            NivelUsoDescanso7Horas.Medio => mantenerDescanso7Horas,
            _ => false
        };
    }

    private static PresupuestoDescanso7Horas CrearPresupuestoDescanso7Horas(
        ProblemaRotacion problema,
        OpcionesSolverRotacion? opciones)
    {
        var nivel = opciones?.NivelUsoDescanso7Horas ?? NivelUsoDescanso7Horas.Bajo;
        var semanas = Math.Max(1, problema.CantidadSemanas);
        var transicionesEstimadas = EstimarTransicionesAsignadas(problema);

        var (porcentaje, maximoPorEmpleado) = nivel switch
        {
            NivelUsoDescanso7Horas.Alto => (0.30d, Math.Max(2, semanas * 2)),
            NivelUsoDescanso7Horas.Medio => (0.15d, Math.Max(2, semanas)),
            NivelUsoDescanso7Horas.NoUsar => (0d, 0),
            _ => (0.05d, 1)
        };

        var maximoGlobal = nivel == NivelUsoDescanso7Horas.NoUsar
            ? 0
            : Math.Max(1, (int)Math.Ceiling(transicionesEstimadas * porcentaje));
        maximoGlobal = Math.Min(maximoGlobal, nivel == NivelUsoDescanso7Horas.NoUsar
            ? 0
            : Math.Max(1, problema.Empleados.Count * maximoPorEmpleado));
        return new PresupuestoDescanso7Horas(maximoGlobal, maximoPorEmpleado);
    }

    private static int EstimarTransicionesAsignadas(ProblemaRotacion problema)
    {
        var asignacionesRequeridas = problema.Slots
            .Where(slot => !slot.EsAuxiliar)
            .Sum(slot => Math.Max(0, slot.EmpleadosRequeridos));
        var empleadosPorSemana = Math.Max(1, problema.Empleados.Count * Math.Max(1, problema.CantidadSemanas));
        return Math.Max(1, asignacionesRequeridas - empleadosPorSemana);
    }

    private static ProblemaRotacion CrearProblemaConDescansoMinimo(
        ProblemaRotacion problemaVentana,
        int minutosDescansoMinimo,
        PresupuestoDescanso7Horas presupuesto)
    {
        return problemaVentana with
        {
            Reglas = problemaVentana.Reglas with
            {
                Obligatorias = problemaVentana.Reglas.Obligatorias with
                {
                    MinutosMinimosDescansoEntreTurnos = minutosDescansoMinimo
                },
                Configurables = problemaVentana.Reglas.Configurables with
                {
                    MaximoDescansos7HorasGlobal = presupuesto.MaximoGlobal,
                    MaximoDescansos7HorasPorEmpleado = presupuesto.MaximoPorEmpleado,
                    PenalizarDescansos7Horas = true
                }
            }
        };
    }

    private static bool DebeReintentarConSobrecupoFeriado(
        ProblemaRotacion problemaVentana,
        SolucionRotacionCp solucionVentana,
        OpcionesSolverRotacion? opciones)
    {
        return solucionVentana.Estado == EstadoSolucionRotacion.Infactible &&
               opciones?.AutorizarSobrecupoSemanalEnFeriado == true &&
               !problemaVentana.Reglas.Configurables.PermitirSobrecupoSemanalEnFeriado &&
               TieneFeriadoLaborable(problemaVentana);
    }

    private static ProblemaRotacion CrearProblemaConSobrecupoFeriado(ProblemaRotacion problemaVentana)
    {
        return problemaVentana with
        {
            Reglas = problemaVentana.Reglas with
            {
                Configurables = problemaVentana.Reglas.Configurables with
                {
                    PermitirSobrecupoSemanalEnFeriado = true
                }
            }
        };
    }

    private static ProblemaRotacion AplicarNivelEvitarFinesSemanaConsecutivos(
        ProblemaRotacion problema,
        OpcionesSolverRotacion? opciones)
    {
        var nivel = opciones?.NivelEvitarFinesSemanaConsecutivos
            ?? problema.Reglas.Configurables.NivelEvitarFinesSemanaConsecutivos;
        var maximoConsecutivos = nivel == NivelEvitarFinesSemanaConsecutivos.NoUsar
            ? 1
            : problema.Reglas.Configurables.MaximoFinesSemanaConsecutivos;

        return problema with
        {
            Reglas = problema.Reglas with
            {
                Configurables = problema.Reglas.Configurables with
                {
                    NivelEvitarFinesSemanaConsecutivos = nivel,
                    EvitarFinesSemanaConsecutivos = true,
                    MaximoFinesSemanaConsecutivos = Math.Max(1, maximoConsecutivos)
                }
            }
        };
    }

    private static ProblemaRotacion AplicarNivelAgruparTiposTurnoConsecutivos(
        ProblemaRotacion problema,
        OpcionesSolverRotacion? opciones)
    {
        var nivel = opciones?.NivelAgruparTiposTurnoConsecutivos
            ?? problema.Reglas.Configurables.NivelAgruparTiposTurnoConsecutivos;

        return problema with
        {
            Reglas = problema.Reglas with
            {
                Configurables = problema.Reglas.Configurables with
                {
                    NivelAgruparTiposTurnoConsecutivos = nivel
                }
            }
        };
    }

    private static bool TieneFeriadoLaborable(ProblemaRotacion problemaVentana)
    {
        var fechaFin = problemaVentana.FechaInicio.AddDays((problemaVentana.CantidadSemanas * 7) - 1);
        return problemaVentana.Feriados.Any(fecha =>
            fecha >= problemaVentana.FechaInicio &&
            fecha <= fechaFin &&
            CalculadoraCreditoFeriado.EsFeriadoLaborable(problemaVentana, fecha));
    }

    private static SolucionRotacionCp MarcarDetalleFallbackDescansoMinimo(SolucionRotacionCp solucion, int minutosDescansoMinimo)
    {
        var horas = minutosDescansoMinimo / 60d;
        var etiquetaHoras = horas % 1 == 0
            ? ((int)horas).ToString()
            : horas.ToString("0.#");

        return ReemplazarDetalle(solucion, $"Rest{etiquetaHoras}h/{solucion.DetalleEstado}");
    }

    private static SolucionRotacionCp MarcarDetalleFallbackSobrecupoFeriado(SolucionRotacionCp solucion)
    {
        return ReemplazarDetalle(solucion, $"FeriadoOvertime/{solucion.DetalleEstado}");
    }

    /// <summary>
    /// Reintentar sin restricción nocturno-historial cuando la solución es infáctible y hay datos
    /// de racha nocturna del historial previo que podrían estar causando la infactibilidad.
    /// </summary>
    private static bool DebeReintentarSinNocturnoHistorial(
        SolucionRotacionCp solucionVentana,
        EstadoResolucionSemanal estado)
    {
        return solucionVentana.Estado == EstadoSolucionRotacion.Infactible &&
               (estado.EmpleadosConNocturnoUltimoDiaAnterior.Count > 0 ||
                estado.RachaNocturnaBordeHistoriaPorEmpleado.Count > 0);
    }

    /// <summary>
    /// Clona el estado omitiendo los campos de nocturno-historial.
    /// El estado resultante se usa solo para el intento de fallback; el estado original
    /// no se modifica y sigue siendo la fuente de verdad para las semanas siguientes.
    /// </summary>
    private static EstadoResolucionSemanal ClonarEstadoSinNocturnoHistorial(EstadoResolucionSemanal estado)
    {
        var clon = new EstadoResolucionSemanal();
        // Copiar todo excepto EmpleadosConNocturnoUltimoDiaAnterior y RachaNocturnaBordeHistoriaPorEmpleado.
        foreach (var empleado in estado.EmpleadosConFinSemanaAnterior)
            clon.EmpleadosConFinSemanaAnterior.Add(empleado);
        foreach (var kvp in estado.RachaFinesSemanaConsecutivosPorEmpleado)
            clon.RachaFinesSemanaConsecutivosPorEmpleado[kvp.Key] = kvp.Value;
        // EmpleadosConNocturnoUltimoDiaAnterior: omitido intencionalmente (restricción suavizada)
        // RachaNocturnaBordeHistoriaPorEmpleado: omitido intencionalmente (restricción suavizada)
        foreach (var kvp in estado.UltimoFinTurnoPorEmpleado)
            clon.UltimoFinTurnoPorEmpleado[kvp.Key] = kvp.Value;
        foreach (var kvp in estado.TurnosNocturnosPorEmpleadoMes)
            clon.TurnosNocturnosPorEmpleadoMes[kvp.Key] = kvp.Value;
        foreach (var kvp in estado.SlotsFinSemanaPorEmpleadoMes)
            clon.SlotsFinSemanaPorEmpleadoMes[kvp.Key] = kvp.Value;
        foreach (var kvp in estado.MinutosNocturnosAcumuladosPorEmpleado)
            clon.MinutosNocturnosAcumuladosPorEmpleado[kvp.Key] = kvp.Value;
        foreach (var kvp in estado.MinutosFinSemanaAcumuladosPorEmpleado)
            clon.MinutosFinSemanaAcumuladosPorEmpleado[kvp.Key] = kvp.Value;
        foreach (var kvp in estado.MinutosTotalesAcumuladosPorEmpleado)
            clon.MinutosTotalesAcumuladosPorEmpleado[kvp.Key] = kvp.Value;
        foreach (var kvp in estado.MinutosFeriadoAcumuladosPorEmpleado)
            clon.MinutosFeriadoAcumuladosPorEmpleado[kvp.Key] = kvp.Value;
        foreach (var kvp in estado.Descansos7HorasAcumuladosPorEmpleado)
            clon.Descansos7HorasAcumuladosPorEmpleado[kvp.Key] = kvp.Value;
        clon.Descansos7HorasAcumuladosTotal = estado.Descansos7HorasAcumuladosTotal;
        foreach (var kvp in estado.EmpleadosGrupoEspecialSemanaAnterior)
            clon.EmpleadosGrupoEspecialSemanaAnterior[kvp.Key] = new HashSet<string>(kvp.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in estado.EmpleadosGrupoEspecialCicloActual)
            clon.EmpleadosGrupoEspecialCicloActual[kvp.Key] = new HashSet<string>(kvp.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in estado.UsosGrupoEspecialPorEmpleado)
            clon.UsosGrupoEspecialPorEmpleado[kvp.Key] = kvp.Value;
        return clon;
    }

    private static SolucionRotacionCp MarcarDetalleFallbackNocturnoHistorial(SolucionRotacionCp solucion)
    {
        return ReemplazarDetalle(solucion, $"NHist/{solucion.DetalleEstado}");
    }

    private static int DeterminarSemanasVentana(ProblemaRotacion problema, int indiceSemana)
    {
        var restante = problema.CantidadSemanas - indiceSemana;
        if (restante <= 1)
        {
            return 1;
        }

        var ventanaAmpliada =
            HayVacacionesEnSemana(problema, indiceSemana - 1) ||
            HayVacacionesEnSemana(problema, indiceSemana) ||
            HayVacacionesEnSemana(problema, indiceSemana + 1);

        return ventanaAmpliada ? 2 : 1;
    }

    private static bool HayVacacionesEnSemana(ProblemaRotacion problema, int indiceSemana)
    {
        if (indiceSemana < 0 || indiceSemana >= problema.CantidadSemanas)
        {
            return false;
        }

        var fechaInicioSemana = problema.FechaInicio.AddDays(indiceSemana * 7);
        var fechaFinSemana = fechaInicioSemana.AddDays(7);
        return problema.Ausencias.Any(ausencia =>
            ausencia.Fechas.Any(fecha => fecha >= fechaInicioSemana && fecha < fechaFinSemana));
    }

    private static SolucionRotacionCp ReemplazarDetalle(SolucionRotacionCp solucion, string detalle)
    {
        return solucion with { DetalleEstado = detalle };
    }

    private static List<AsignacionSlot> ExtraerAsignacionesSemanaConfirmada(
        ProblemaRotacion problemaVentana,
        SolucionRotacionCp solucionVentana)
    {
        var idsSemanaConfirmada = problemaVentana.Slots
            .Where(slot => slot.IndiceSemana == 0)
            .Select(slot => slot.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return solucionVentana.Asignaciones
            .Where(asignacion => idsSemanaConfirmada.Contains(asignacion.IdSlot))
            .ToList();
    }

    private static void ActualizarEstadoSemanal(
        EstadoResolucionSemanal estado,
        ProblemaRotacion problemaVentana,
        IReadOnlyCollection<AsignacionSlot> asignacionesSemanaConfirmada)
    {
        var slotsPorId = problemaVentana.Slots.ToDictionary(slot => slot.Id);
        ActualizarUsoDescanso7Horas(estado, problemaVentana, asignacionesSemanaConfirmada, slotsPorId);
        ActualizarUsoGruposEspeciales(estado, problemaVentana, asignacionesSemanaConfirmada, slotsPorId);

        var rachasPrevias = estado.RachaFinesSemanaConsecutivosPorEmpleado
            .ToDictionary(par => par.Key, par => par.Value, StringComparer.OrdinalIgnoreCase);
        var empleadosConFinSemanaActual = asignacionesSemanaConfirmada
            .Where(asignacion => slotsPorId[asignacion.IdSlot].IndiceDia is 5 or 6)
            .Select(asignacion => asignacion.EmpleadoId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        estado.EmpleadosConFinSemanaAnterior.Clear();
        estado.RachaFinesSemanaConsecutivosPorEmpleado.Clear();
        estado.EmpleadosConNocturnoUltimoDiaAnterior.Clear();
        estado.RachaNocturnaBordeHistoriaPorEmpleado.Clear();
        foreach (var empleado in problemaVentana.Empleados)
        {
            if (!empleadosConFinSemanaActual.Contains(empleado.Id))
            {
                continue;
            }

            estado.EmpleadosConFinSemanaAnterior.Add(empleado.Id);
            rachasPrevias.TryGetValue(empleado.Id, out var rachaPrevia);
            estado.RachaFinesSemanaConsecutivosPorEmpleado[empleado.Id] = rachaPrevia + 1;
        }

        foreach (var asignacion in asignacionesSemanaConfirmada)
        {
            var slot = slotsPorId[asignacion.IdSlot];
            if (slot.IndiceDia == 6 && slot.EsTurnoNocturno)
            {
                estado.EmpleadosConNocturnoUltimoDiaAnterior.Add(asignacion.EmpleadoId);
            }
        }

        // Calcular la racha nocturna consecutiva al final de la semana confirmada.
        // Se usa para que AplicarNocturnasConsecutivasDesdeHistoria valide el cruce hacia la semana siguiente.
        // Ejemplo: Sab+Dom noche (racha=2, max=3) → la próxima ventana puede tener máx 1 noche los primeros días.
        var fechaFinSemanaConfirmada = slotsPorId.Values.Max(s => s.Fecha);
        var fechasNocturnasPorEmpleado = asignacionesSemanaConfirmada
            .Select(a => new { a.EmpleadoId, Slot = slotsPorId[a.IdSlot] })
            .Where(x => x.Slot.EsTurnoNocturno)
            .GroupBy(x => x.EmpleadoId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Slot.Fecha).ToHashSet(), StringComparer.OrdinalIgnoreCase);

        foreach (var empleado in problemaVentana.Empleados)
        {
            if (!fechasNocturnasPorEmpleado.TryGetValue(empleado.Id, out var fechasNocturnas))
            {
                continue;
            }

            var racha = 0;
            var fechaActual = fechaFinSemanaConfirmada;
            while (fechasNocturnas.Contains(fechaActual))
            {
                racha++;
                fechaActual = fechaActual.AddDays(-1);
            }

            if (racha > 0)
            {
                estado.RachaNocturnaBordeHistoriaPorEmpleado[empleado.Id] = racha;
            }
        }

        estado.UltimoFinTurnoPorEmpleado.Clear();
        foreach (var grupoEmpleado in asignacionesSemanaConfirmada.GroupBy(asignacion => asignacion.EmpleadoId))
        {
            var ultimoFin = grupoEmpleado
                .Select(asignacion => slotsPorId[asignacion.IdSlot].FinLocal)
                .Max();
            estado.UltimoFinTurnoPorEmpleado[grupoEmpleado.Key] = ultimoFin;
        }

        foreach (var grupo in asignacionesSemanaConfirmada
                     .Select(asignacion => new { asignacion.EmpleadoId, Slot = slotsPorId[asignacion.IdSlot] })
                     .Where(item => item.Slot.IndiceDia is 5 or 6)
                     .GroupBy(item => new { item.EmpleadoId, item.Slot.Fecha.Year, item.Slot.Fecha.Month }))
        {
            var clave = (grupo.Key.EmpleadoId, grupo.Key.Year, grupo.Key.Month);
            estado.SlotsFinSemanaPorEmpleadoMes[clave] = estado.SlotsFinSemanaPorEmpleadoMes.TryGetValue(clave, out var slotsActuales)
                ? slotsActuales + grupo.Count()
                : grupo.Count();
        }

        foreach (var grupo in asignacionesSemanaConfirmada
                     .Select(asignacion => new { asignacion.EmpleadoId, Slot = slotsPorId[asignacion.IdSlot] })
                     .Where(item => item.Slot.EsTurnoNocturno)
                     .GroupBy(item => new { item.EmpleadoId, item.Slot.Fecha.Year, item.Slot.Fecha.Month }))
        {
            var clave = (grupo.Key.EmpleadoId, grupo.Key.Year, grupo.Key.Month);
            estado.TurnosNocturnosPorEmpleadoMes[clave] = estado.TurnosNocturnosPorEmpleadoMes.TryGetValue(clave, out var turnosActuales)
                ? turnosActuales + grupo.Count()
                : grupo.Count();
        }

        foreach (var grupo in asignacionesSemanaConfirmada
                     .Select(asignacion => new { asignacion.EmpleadoId, Slot = slotsPorId[asignacion.IdSlot] })
                     .Where(item => item.Slot.EsTurnoNocturno)
                     .GroupBy(item => item.EmpleadoId))
        {
            var minutosNocturnosSemana = grupo.Sum(item => Math.Max(0, item.Slot.MinutosVentanaNocturna));
            if (minutosNocturnosSemana <= 0)
            {
                continue;
            }

            estado.MinutosNocturnosAcumuladosPorEmpleado[grupo.Key] =
                estado.MinutosNocturnosAcumuladosPorEmpleado.TryGetValue(grupo.Key, out var minutosNocturnosActuales)
                    ? minutosNocturnosActuales + minutosNocturnosSemana
                    : minutosNocturnosSemana;
        }

        foreach (var grupo in asignacionesSemanaConfirmada
                     .Select(asignacion => new { asignacion.EmpleadoId, Slot = slotsPorId[asignacion.IdSlot] })
                     .Where(item => item.Slot.IndiceDia is 5 or 6)
                     .GroupBy(item => item.EmpleadoId))
        {
            var minutosFinSemanaSemana = grupo.Sum(item => Math.Max(0, item.Slot.MinutosTrabajoComputables));
            if (minutosFinSemanaSemana <= 0)
            {
                continue;
            }

            estado.MinutosFinSemanaAcumuladosPorEmpleado[grupo.Key] =
                estado.MinutosFinSemanaAcumuladosPorEmpleado.TryGetValue(grupo.Key, out var minutosFinSemanaActuales)
                    ? minutosFinSemanaActuales + minutosFinSemanaSemana
                    : minutosFinSemanaSemana;
        }

        foreach (var grupo in asignacionesSemanaConfirmada
                     .Select(asignacion => new { asignacion.EmpleadoId, Slot = slotsPorId[asignacion.IdSlot] })
                     .GroupBy(item => item.EmpleadoId))
        {
            var minutosTotalesSemana = grupo.Sum(item => Math.Max(0, item.Slot.MinutosTrabajoComputables));
            if (minutosTotalesSemana <= 0)
            {
                continue;
            }

            estado.MinutosTotalesAcumuladosPorEmpleado[grupo.Key] =
                estado.MinutosTotalesAcumuladosPorEmpleado.TryGetValue(grupo.Key, out var minutosTotalesActuales)
                    ? minutosTotalesActuales + minutosTotalesSemana
                    : minutosTotalesSemana;
        }

        foreach (var grupo in asignacionesSemanaConfirmada
                     .Select(asignacion => new { asignacion.EmpleadoId, Slot = slotsPorId[asignacion.IdSlot] })
                     .Where(item => problemaVentana.Feriados.Contains(item.Slot.Fecha))
                     .GroupBy(item => item.EmpleadoId))
        {
            var minutosFeriadoSemana = grupo.Sum(item => Math.Max(0, item.Slot.MinutosTrabajoComputables));
            if (minutosFeriadoSemana <= 0)
            {
                continue;
            }

            estado.MinutosFeriadoAcumuladosPorEmpleado[grupo.Key] =
                estado.MinutosFeriadoAcumuladosPorEmpleado.TryGetValue(grupo.Key, out var minutosFeriadoActuales)
                    ? minutosFeriadoActuales + minutosFeriadoSemana
                    : minutosFeriadoSemana;
        }
    }

    private static void ActualizarUsoDescanso7Horas(
        EstadoResolucionSemanal estado,
        ProblemaRotacion problemaVentana,
        IReadOnlyCollection<AsignacionSlot> asignacionesSemanaConfirmada,
        IReadOnlyDictionary<string, SlotTurno> slotsPorId)
    {
        foreach (var empleado in problemaVentana.Empleados)
        {
            var slotsEmpleado = asignacionesSemanaConfirmada
                .Where(asignacion => string.Equals(asignacion.EmpleadoId, empleado.Id, StringComparison.OrdinalIgnoreCase))
                .Select(asignacion => slotsPorId[asignacion.IdSlot])
                .OrderBy(slot => slot.InicioLocal)
                .ToArray();

            if (slotsEmpleado.Length == 0)
            {
                continue;
            }

            var usos = 0;
            if (estado.UltimoFinTurnoPorEmpleado.TryGetValue(empleado.Id, out var ultimoFinTurno) &&
                CalculadoraDescanso7Horas.EsDescanso7Horas(ultimoFinTurno, slotsEmpleado[0].InicioLocal))
            {
                usos++;
            }

            for (var indice = 1; indice < slotsEmpleado.Length; indice++)
            {
                if (CalculadoraDescanso7Horas.EsDescanso7Horas(slotsEmpleado[indice - 1].FinLocal, slotsEmpleado[indice].InicioLocal))
                {
                    usos++;
                }
            }

            if (usos <= 0)
            {
                continue;
            }

            estado.Descansos7HorasAcumuladosTotal += usos;
            estado.Descansos7HorasAcumuladosPorEmpleado[empleado.Id] =
                estado.Descansos7HorasAcumuladosPorEmpleado.TryGetValue(empleado.Id, out var usosActuales)
                    ? usosActuales + usos
                    : usos;
        }
    }

    private static void ActualizarUsoGruposEspeciales(
        EstadoResolucionSemanal estado,
        ProblemaRotacion problemaVentana,
        IReadOnlyCollection<AsignacionSlot> asignacionesSemanaConfirmada,
        IReadOnlyDictionary<string, SlotTurno> slotsPorId)
    {
        estado.EmpleadosGrupoEspecialSemanaAnterior.Clear();
        foreach (var grupoEspecialId in problemaVentana.Reglas.Configurables.GruposEspecialesPersonaUnicaPorSemana)
        {
            var empleados = asignacionesSemanaConfirmada
                .Where(asignacion => slotsPorId.TryGetValue(asignacion.IdSlot, out var slot) &&
                                     string.Equals(slot.GrupoId, grupoEspecialId, StringComparison.OrdinalIgnoreCase))
                .Select(asignacion => asignacion.EmpleadoId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (empleados.Count > 0)
            {
                estado.EmpleadosGrupoEspecialSemanaAnterior[grupoEspecialId] = empleados;
                foreach (var empleadoId in empleados)
                {
                    var clave = (grupoEspecialId, empleadoId);
                    estado.UsosGrupoEspecialPorEmpleado[clave] = estado.UsosGrupoEspecialPorEmpleado.TryGetValue(clave, out var usos)
                        ? usos + 1
                        : 1;
                }

                if (problemaVentana.Reglas.Configurables.GrupoFuentePorGrupoEspecial.TryGetValue(grupoEspecialId, out var grupoFuenteId))
                {
                    var elegiblesCount = problemaVentana.Empleados
                        .Count(empleado => EsElegibleGrupoEspecial(empleado, grupoEspecialId, grupoFuenteId));

                    if (!estado.EmpleadosGrupoEspecialCicloActual.TryGetValue(grupoEspecialId, out var usadosEnCiclo))
                    {
                        usadosEnCiclo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        estado.EmpleadosGrupoEspecialCicloActual[grupoEspecialId] = usadosEnCiclo;
                    }

                    foreach (var empleadoId in empleados)
                    {
                        usadosEnCiclo.Add(empleadoId);
                    }

                    if (elegiblesCount > 0 && usadosEnCiclo.Count >= elegiblesCount)
                    {
                        usadosEnCiclo.Clear();
                    }

                    if (usadosEnCiclo.Count == 0)
                    {
                        estado.EmpleadosGrupoEspecialCicloActual.Remove(grupoEspecialId);
                    }
                }
            }
        }
    }

    private static bool EsElegibleGrupoEspecial(Empleado empleado, string grupoEspecialId, string grupoFuenteId)
    {
        return string.Equals(empleado.GrupoPrimarioId, grupoFuenteId, StringComparison.OrdinalIgnoreCase)
               && empleado.GruposSecundariosIds.Contains(grupoEspecialId);
    }

    private static string ConstruirDiagnosticoInicioSemana(
        ProblemaRotacion problemaVentana,
        EstadoResolucionSemanal estado,
        int indiceSemanaBase)
    {
        var fechaFin = problemaVentana.FechaInicio.AddDays((problemaVentana.CantidadSemanas * 7) - 1);
        var sb = new StringBuilder();

        sb.AppendLine(
            $"RotacionV2 semana S{indiceSemanaBase + 1} inicio: ventana={problemaVentana.CantidadSemanas} rango={problemaVentana.FechaInicio:yyyy-MM-dd}..{fechaFin:yyyy-MM-dd} slots={problemaVentana.Slots.Count} empleados={problemaVentana.Empleados.Count} feriados=[{FormatearFechas(problemaVentana.Feriados)}] descansoMin={problemaVentana.Reglas.Obligatorias.MinutosMinimosDescansoEntreTurnos}");
        sb.AppendLine(
            $"Estado previo: rachasFds=[{FormatearRachasFds(problemaVentana, estado)}] noctDomPrev=[{FormatearNombres(problemaVentana, estado.EmpleadosConNocturnoUltimoDiaAnterior)}] ultimoFin=[{FormatearUltimosFines(problemaVentana, estado)}] fdsMes=[{FormatearConteosMesFinDeSemana(problemaVentana, estado.SlotsFinSemanaPorEmpleadoMes)}] noctMes=[{FormatearConteosMes(problemaVentana, estado.TurnosNocturnosPorEmpleadoMes)}]");
        sb.AppendLine(ConstruirResumenCoberturaVentana(problemaVentana));
        sb.AppendLine(ConstruirDetalleCoberturaVentana(problemaVentana));

        var turnosObjetivoBasePorSemana = CalcularTurnosObjetivoBasePorSemana(problemaVentana);
        for (var indiceSemana = 0; indiceSemana < problemaVentana.CantidadSemanas; indiceSemana++)
        {
            var fechaInicioSemana = problemaVentana.FechaInicio.AddDays(indiceSemana * 7);
            var fechaFinSemana = fechaInicioSemana.AddDays(6);
            var ajustesSemana = problemaVentana.Empleados
                .Select(empleado => ConstruirDiagnosticoEmpleadoSemana(
                    problemaVentana,
                    estado,
                    empleado,
                    indiceSemana,
                    turnosObjetivoBasePorSemana))
                .Where(texto => !string.IsNullOrWhiteSpace(texto))
                .ToList();

            if (ajustesSemana.Count == 0)
            {
                continue;
            }

            sb.AppendLine(
                $"Semana local {indiceSemana + 1}: {fechaInicioSemana:yyyy-MM-dd}..{fechaFinSemana:yyyy-MM-dd}");
            foreach (var ajuste in ajustesSemana)
            {
                sb.AppendLine($"  {ajuste}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string ConstruirResumenCoberturaVentana(ProblemaRotacion problemaVentana)
    {
        var partesSemana = new List<string>();
        var turnosObjetivoBasePorSemana = CalcularTurnosObjetivoBasePorSemana(problemaVentana);

        for (var indiceSemana = 0; indiceSemana < problemaVentana.CantidadSemanas; indiceSemana++)
        {
            var slotsSemana = problemaVentana.Slots
                .Where(slot => slot.IndiceSemana == indiceSemana)
                .ToArray();
            var slotsNormales = slotsSemana.Where(slot => !slot.EsAuxiliar).ToArray();
            var reqNoFeriado = slotsNormales
                .Where(slot => !CalculadoraCreditoFeriado.EsFeriadoLaborable(problemaVentana, slot.Fecha))
                .Sum(slot => slot.EmpleadosRequeridos);
            var reqFeriado = slotsNormales
                .Where(slot => CalculadoraCreditoFeriado.EsFeriadoLaborable(problemaVentana, slot.Fecha))
                .Sum(slot => slot.EmpleadosRequeridos);
            var auxNoFeriado = slotsSemana
                .Where(slot => slot.EsAuxiliar && !CalculadoraCreditoFeriado.EsFeriadoLaborable(problemaVentana, slot.Fecha))
                .Sum(slot => Math.Max(0, slot.CapacidadPlanificada > 0 ? slot.CapacidadPlanificada : slot.EmpleadosRequeridos));
            var auxFeriado = slotsSemana
                .Where(slot => slot.EsAuxiliar && CalculadoraCreditoFeriado.EsFeriadoLaborable(problemaVentana, slot.Fecha))
                .Sum(slot => Math.Max(0, slot.CapacidadPlanificada > 0 ? slot.CapacidadPlanificada : slot.EmpleadosRequeridos));
            var tieneFeriadoLaborable = problemaVentana.Feriados.Any(fecha =>
                fecha >= problemaVentana.FechaInicio.AddDays(indiceSemana * 7) &&
                fecha <= problemaVentana.FechaInicio.AddDays((indiceSemana * 7) + 6) &&
                CalculadoraCreditoFeriado.EsFeriadoLaborable(problemaVentana, fecha));
            var objetivoTotal = problemaVentana.Empleados.Sum(empleado =>
                turnosObjetivoBasePorSemana.TryGetValue(indiceSemana, out var objetivoBase) && objetivoBase.HasValue
                    ? CalculadoraObjetivoSemanal.CalcularTurnosObjetivo(problemaVentana, empleado, indiceSemana, objetivoBase.Value)
                    : 0);
            var objetivoBaseTotal = turnosObjetivoBasePorSemana.TryGetValue(indiceSemana, out var objetivoBaseSemana) && objetivoBaseSemana.HasValue
                ? problemaVentana.Empleados.Count * objetivoBaseSemana.Value
                : objetivoTotal;
            var etiquetaObjetivo = tieneFeriadoLaborable && problemaVentana.Reglas.Configurables.PermitirSobrecupoSemanalEnFeriado
                ? $"flex:{objetivoTotal}..{objetivoBaseTotal}"
                : $"{(tieneFeriadoLaborable ? "soft" : "hard")}:{objetivoTotal}";

            partesSemana.Add(
                $"S{indiceSemana + 1}:objNoFeriado={etiquetaObjetivo} reqNoFeriado={reqNoFeriado} reqFeriado={reqFeriado} auxCapNoFeriado={auxNoFeriado} auxCapFeriado={auxFeriado}");
        }

        return $"Cobertura: {string.Join("; ", partesSemana)}";
    }

    private static string ConstruirDetalleCoberturaVentana(ProblemaRotacion problemaVentana)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Cobertura detalle:");

        for (var indiceSemana = 0; indiceSemana < problemaVentana.CantidadSemanas; indiceSemana++)
        {
            var fechaInicioSemana = problemaVentana.FechaInicio.AddDays(indiceSemana * 7);
            var fechaFinSemana = fechaInicioSemana.AddDays(6);
            sb.AppendLine($"  S{indiceSemana + 1}: {fechaInicioSemana:yyyy-MM-dd}..{fechaFinSemana:yyyy-MM-dd}");

            foreach (var grupoDia in problemaVentana.Slots
                         .Where(slot => slot.IndiceSemana == indiceSemana && !slot.EsAuxiliar)
                         .GroupBy(slot => slot.Fecha)
                         .OrderBy(grupo => grupo.Key))
            {
                var fecha = grupoDia.Key;
                var reqDia = grupoDia.Sum(slot => slot.EmpleadosRequeridos);
                var detalleDia = grupoDia
                    .GroupBy(slot => new
                    {
                        slot.GrupoId,
                        Inicio = slot.InicioLocal.TimeOfDay,
                        Fin = slot.FinLocal.TimeOfDay,
                        CruzaDia = slot.FinLocal.Date > slot.InicioLocal.Date
                    })
                    .OrderBy(grupo => ObtenerNombreGrupo(problemaVentana, grupo.Key.GrupoId), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(grupo => grupo.Key.Inicio)
                    .ThenBy(grupo => grupo.Key.Fin)
                    .Select(grupo =>
                        $"{ObtenerNombreGrupo(problemaVentana, grupo.Key.GrupoId)}/{FormatearRangoTurno(grupo.First())}={grupo.Sum(slot => slot.EmpleadosRequeridos)}");

                sb.AppendLine(
                    $"    {fecha:yyyy-MM-dd} {(EsFinSemana(fecha) ? "FDS" : "LAB")} req={reqDia} [{string.Join(", ", detalleDia)}]");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static bool EsFinSemana(DateOnly fecha)
    {
        return fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    private static string ConstruirDiagnosticoFallaSemana(
        ProblemaRotacion problemaVentana,
        EstadoResolucionSemanal estado,
        int indiceSemanaBase,
        SolucionRotacionCp solucionVentana)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            $"RotacionV2 semana S{indiceSemanaBase + 1} fallo: estado={solucionVentana.Estado} detalle={solucionVentana.DetalleEstado}");

        var objetivos = ConstruirObjetivosPorEmpleadoSemana(problemaVentana);
        var slotsCriticos = problemaVentana.Slots
            .OrderBy(slot => slot.IndiceSemana)
            .ThenBy(slot => slot.Fecha)
            .ThenBy(slot => slot.InicioLocal)
            .Where(slot => !slot.EsAuxiliar)
            .Select(slot => new
            {
                Slot = slot,
                Diagnostico = DiagnosticarSlot(problemaVentana, estado, slot, objetivos)
            })
            .Where(item => item.Diagnostico.Candidatos.Count < item.Slot.EmpleadosRequeridos)
            .ToList();

        if (slotsCriticos.Count == 0)
        {
            sb.Append("No se detectaron slots obligatorios con deficit obvio de candidatos bajo estado acumulado actual.");
            return sb.ToString();
        }

        foreach (var item in slotsCriticos)
        {
            var slot = item.Slot;
            var diag = item.Diagnostico;
            var banderas = new List<string>();
            if (slot.PuedeOmitirsePorVacacion)
            {
                banderas.Add("opcionalVacacion");
            }

            if (slot.MaximoApoyoCedible > 0)
            {
                banderas.Add($"apoyoCedible={slot.MaximoApoyoCedible}");
            }

            sb.AppendLine(
                $"  slot=S{slot.IndiceSemana + 1} {slot.Fecha:yyyy-MM-dd} grupo={ObtenerNombreGrupo(problemaVentana, slot.GrupoId)} turno={FormatearRangoTurno(slot)} req={slot.EmpleadosRequeridos} cand={diag.Candidatos.Count} candidatos=[{string.Join(", ", diag.Candidatos)}] bloqueos=[{FormatearConteos(diag.Bloqueos)}] flags=[{string.Join(", ", banderas)}]");
        }

        return sb.ToString().TrimEnd();
    }

    private static Dictionary<int, int?> CalcularTurnosObjetivoBasePorSemana(ProblemaRotacion problema)
    {
        var resultado = new Dictionary<int, int?>();
        for (var indiceSemana = 0; indiceSemana < problema.CantidadSemanas; indiceSemana++)
        {
            var slotsSemana = problema.Slots
                .Where(slot =>
                    slot.IndiceSemana == indiceSemana &&
                    !CalculadoraCreditoFeriado.EsFeriadoLaborable(problema, slot.Fecha))
                .ToArray();

            if (slotsSemana.Length == 0)
            {
                resultado[indiceSemana] = null;
                continue;
            }

            var minutosDistintos = slotsSemana
                .Select(slot => slot.MinutosTrabajoComputables)
                .Distinct()
                .ToArray();

            if (minutosDistintos.Length != 1)
            {
                resultado[indiceSemana] = null;
                continue;
            }

            var minutosPorTurno = minutosDistintos[0];
            if (minutosPorTurno <= 0 || problema.Reglas.Obligatorias.MinutosObjetivoSemanales % minutosPorTurno != 0)
            {
                resultado[indiceSemana] = null;
                continue;
            }

            resultado[indiceSemana] = problema.Reglas.Obligatorias.MinutosObjetivoSemanales / minutosPorTurno;
        }

        return resultado;
    }

    private static Dictionary<(string EmpleadoId, int IndiceSemana), int> ConstruirObjetivosPorEmpleadoSemana(ProblemaRotacion problema)
    {
        var objetivos = new Dictionary<(string EmpleadoId, int IndiceSemana), int>();
        var turnosObjetivoBasePorSemana = CalcularTurnosObjetivoBasePorSemana(problema);

        foreach (var empleado in problema.Empleados)
        {
            for (var indiceSemana = 0; indiceSemana < problema.CantidadSemanas; indiceSemana++)
            {
                var turnosObjetivoBase = turnosObjetivoBasePorSemana.TryGetValue(indiceSemana, out var objetivoBase)
                    ? objetivoBase
                    : null;
                objetivos[(empleado.Id, indiceSemana)] = turnosObjetivoBase.HasValue
                    ? CalculadoraObjetivoSemanal.CalcularTurnosObjetivo(
                        problema,
                        empleado,
                        indiceSemana,
                        turnosObjetivoBase.Value)
                    : -1;
            }
        }

        return objetivos;
    }

    private static string? ConstruirDiagnosticoEmpleadoSemana(
        ProblemaRotacion problema,
        EstadoResolucionSemanal estado,
        Empleado empleado,
        int indiceSemana,
        IReadOnlyDictionary<int, int?> turnosObjetivoBasePorSemana)
    {
        var bloqueadas = CalculadoraDisponibilidadVacaciones.ObtenerFechasBloqueadas(problema, empleado.Id)
            .Where(fecha => problema.Slots.Any(slot => slot.IndiceSemana == indiceSemana && slot.Fecha == fecha))
            .OrderBy(fecha => fecha)
            .ToArray();

        var disponibles = ObtenerFechasDisponiblesSemana(problema, empleado, indiceSemana)
            .OrderBy(fecha => fecha)
            .ToArray();

        var turnosObjetivoBase = turnosObjetivoBasePorSemana.TryGetValue(indiceSemana, out var objetivoBase)
            ? objetivoBase
            : null;
        var turnosObjetivoSemana = turnosObjetivoBase.HasValue
            ? CalculadoraObjetivoSemanal.CalcularTurnosObjetivo(
                problema,
                empleado,
                indiceSemana,
                turnosObjetivoBase.Value)
            : -1;

        var soloFinDeSemana = turnosObjetivoSemana == 0 &&
            bloqueadas.Length > 0 &&
            disponibles.Length > 0 &&
            disponibles.All(fecha => problema.Slots.Any(slot =>
                slot.IndiceSemana == indiceSemana &&
                slot.Fecha == fecha &&
                slot.IndiceDia is 5 or 6));

        var tieneAjusteRelevante =
            bloqueadas.Length > 0 ||
            soloFinDeSemana ||
            (turnosObjetivoBase.HasValue && turnosObjetivoSemana >= 0 && turnosObjetivoSemana < turnosObjetivoBase.Value) ||
            TieneRestriccionesAcumuladasEnSemana(problema, estado, empleado, indiceSemana);

        if (!tieneAjusteRelevante)
        {
            return null;
        }

        var fechaReferenciaSemana = problema.FechaInicio.AddDays(indiceSemana * 7);
        var rachaFds = estado.RachaFinesSemanaConsecutivosPorEmpleado.TryGetValue(empleado.Id, out var racha)
            ? racha
            : 0;
        var slotsFdsMes = estado.SlotsFinSemanaPorEmpleadoMes.TryGetValue((empleado.Id, fechaReferenciaSemana.Year, fechaReferenciaSemana.Month), out var usadosFds)
            ? usadosFds
            : 0;
        var maximoSlotsFdsMes = CalculadoraLimiteSlotsFinSemanaMensual.ObtenerMaximoSlotsFinSemanaPorMes(
            problema,
            fechaReferenciaSemana.Year,
            fechaReferenciaSemana.Month);
        var noctMes = estado.TurnosNocturnosPorEmpleadoMes.TryGetValue((empleado.Id, fechaReferenciaSemana.Year, fechaReferenciaSemana.Month), out var usadosNoct)
            ? usadosNoct
            : 0;
        var ultimoFin = estado.UltimoFinTurnoPorEmpleado.TryGetValue(empleado.Id, out var ultimoFinTurno)
            ? ultimoFinTurno.ToString("yyyy-MM-dd HH:mm")
            : "-";

        return $"emp={empleado.Nombre} obj={turnosObjetivoSemana}/{(turnosObjetivoBase?.ToString() ?? "?")} disp=[{FormatearFechas(disponibles)}] bloqueadas=[{FormatearFechas(bloqueadas)}] soloFds={soloFinDeSemana} rachaFds={rachaFds} fdsMes={slotsFdsMes}/{(maximoSlotsFdsMes?.ToString() ?? "-")} noctMes={noctMes} ultimoFin={ultimoFin}";
    }

    private static bool TieneRestriccionesAcumuladasEnSemana(
        ProblemaRotacion problema,
        EstadoResolucionSemanal estado,
        Empleado empleado,
        int indiceSemana)
    {
        if (indiceSemana == 0)
        {
            if (estado.EmpleadosConNocturnoUltimoDiaAnterior.Contains(empleado.Id))
            {
                return true;
            }

            if (estado.RachaFinesSemanaConsecutivosPorEmpleado.TryGetValue(empleado.Id, out var rachaFds) &&
                rachaFds > 0)
            {
                return true;
            }

            if (estado.UltimoFinTurnoPorEmpleado.ContainsKey(empleado.Id))
            {
                return true;
            }
        }

        return problema.Slots
            .Where(slot => slot.IndiceSemana == indiceSemana)
            .Any(slot => TieneBloqueoAcumuladoPorEstado(problema, estado, empleado, slot));
    }

    private static HashSet<DateOnly> ObtenerFechasDisponiblesSemana(ProblemaRotacion problema, Empleado empleado, int indiceSemana)
    {
        var fechasBloqueadas = CalculadoraDisponibilidadVacaciones.ObtenerFechasBloqueadas(problema, empleado.Id);

        return problema.Slots
            .Where(slot => slot.IndiceSemana == indiceSemana)
            .Select(slot => slot.Fecha)
            .Distinct()
            .Where(fecha =>
                !CalculadoraCreditoFeriado.EsFeriadoLaborable(problema, fecha) &&
                !fechasBloqueadas.Contains(fecha) &&
                problema.Slots.Any(slot =>
                    slot.IndiceSemana == indiceSemana &&
                    slot.Fecha == fecha &&
                    PuedeCubrirGrupo(empleado, slot.GrupoId)))
            .ToHashSet();
    }

    private static (List<string> Candidatos, Dictionary<string, int> Bloqueos) DiagnosticarSlot(
        ProblemaRotacion problema,
        EstadoResolucionSemanal estado,
        SlotTurno slot,
        IReadOnlyDictionary<(string EmpleadoId, int IndiceSemana), int> objetivos)
    {
        var candidatos = new List<string>();
        var bloqueos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var empleado in problema.Empleados)
        {
            var razones = ObtenerRazonesBloqueo(problema, estado, empleado, slot, objetivos);
            if (razones.Count == 0)
            {
                candidatos.Add(empleado.Nombre);
                continue;
            }

            foreach (var razon in razones.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                bloqueos[razon] = bloqueos.TryGetValue(razon, out var total)
                    ? total + 1
                    : 1;
            }
        }

        return (candidatos, bloqueos);
    }

    private static List<string> ObtenerRazonesBloqueo(
        ProblemaRotacion problema,
        EstadoResolucionSemanal estado,
        Empleado empleado,
        SlotTurno slot,
        IReadOnlyDictionary<(string EmpleadoId, int IndiceSemana), int> objetivos)
    {
        var razones = new List<string>();

        if (objetivos.TryGetValue((empleado.Id, slot.IndiceSemana), out var objetivoSemana) && objetivoSemana == 0)
        {
            razones.Add("objetivo0");
        }

        if (!PuedeCubrirGrupo(empleado, slot.GrupoId))
        {
            razones.Add("grupo");
        }

        if (CalculadoraDisponibilidadVacaciones.EstaBloqueado(problema, empleado.Id, slot.Fecha))
        {
            razones.Add("vacaciones");
        }

        if (slot.IndiceSemana == 0 &&
            slot.IndiceDia == 0 &&
            slot.EsTurnoNocturno &&
            estado.EmpleadosConNocturnoUltimoDiaAnterior.Contains(empleado.Id))
        {
            razones.Add("nocturnoDomingoPrevio");
        }

        if (slot.IndiceSemana == 0 &&
            slot.IndiceDia is 5 or 6 &&
            problema.Reglas.Configurables.EvitarFinesSemanaConsecutivos)
        {
            var maximoConsecutivos = Math.Max(1, problema.Reglas.Configurables.MaximoFinesSemanaConsecutivos);
            estado.RachaFinesSemanaConsecutivosPorEmpleado.TryGetValue(empleado.Id, out var rachaActual);
            if (rachaActual >= maximoConsecutivos)
            {
                razones.Add("rachaFds");
            }
        }

        if (estado.UltimoFinTurnoPorEmpleado.TryGetValue(empleado.Id, out var ultimoFinTurno))
        {
            var descansoMinimo = TimeSpan.FromMinutes(problema.Reglas.Obligatorias.MinutosMinimosDescansoEntreTurnos);
            if (slot.InicioLocal - ultimoFinTurno < descansoMinimo)
            {
                razones.Add("descansoPrevio");
            }
        }

        if (slot.EsTurnoNocturno &&
            problema.Reglas.Configurables.MaximoTurnosNocturnosPorMes is > 0 &&
            estado.TurnosNocturnosPorEmpleadoMes.TryGetValue((empleado.Id, slot.Fecha.Year, slot.Fecha.Month), out var turnosNocturnosMes) &&
            turnosNocturnosMes >= problema.Reglas.Configurables.MaximoTurnosNocturnosPorMes)
        {
            razones.Add("maxNoctMes");
        }

        var maximoSlotsFinSemanaMes = CalculadoraLimiteSlotsFinSemanaMensual.ObtenerMaximoSlotsFinSemanaPorMes(
            problema,
            slot.Fecha.Year,
            slot.Fecha.Month);
        if (slot.IndiceDia is 5 or 6 &&
            maximoSlotsFinSemanaMes is > 0 &&
            estado.SlotsFinSemanaPorEmpleadoMes.TryGetValue((empleado.Id, slot.Fecha.Year, slot.Fecha.Month), out var slotsFdsMes) &&
            slotsFdsMes >= maximoSlotsFinSemanaMes)
        {
            razones.Add("maxFdsMes");
        }

        return razones;
    }

    private static bool TieneBloqueoAcumuladoPorEstado(
        ProblemaRotacion problema,
        EstadoResolucionSemanal estado,
        Empleado empleado,
        SlotTurno slot)
    {
        if (slot.IndiceSemana == 0 &&
            slot.IndiceDia == 0 &&
            slot.EsTurnoNocturno &&
            estado.EmpleadosConNocturnoUltimoDiaAnterior.Contains(empleado.Id))
        {
            return true;
        }

        if (slot.IndiceSemana == 0 &&
            slot.IndiceDia is 5 or 6 &&
            problema.Reglas.Configurables.EvitarFinesSemanaConsecutivos)
        {
            var maximoConsecutivos = Math.Max(1, problema.Reglas.Configurables.MaximoFinesSemanaConsecutivos);
            estado.RachaFinesSemanaConsecutivosPorEmpleado.TryGetValue(empleado.Id, out var rachaActual);
            if (rachaActual >= maximoConsecutivos)
            {
                return true;
            }
        }

        if (estado.UltimoFinTurnoPorEmpleado.TryGetValue(empleado.Id, out var ultimoFinTurno))
        {
            var descansoMinimo = TimeSpan.FromMinutes(problema.Reglas.Obligatorias.MinutosMinimosDescansoEntreTurnos);
            if (slot.InicioLocal - ultimoFinTurno < descansoMinimo)
            {
                return true;
            }
        }

        if (slot.EsTurnoNocturno &&
            problema.Reglas.Configurables.MaximoTurnosNocturnosPorMes is > 0 &&
            estado.TurnosNocturnosPorEmpleadoMes.TryGetValue((empleado.Id, slot.Fecha.Year, slot.Fecha.Month), out var turnosNocturnosMes) &&
            turnosNocturnosMes >= problema.Reglas.Configurables.MaximoTurnosNocturnosPorMes)
        {
            return true;
        }

        var maximoSlotsFinSemanaMes = CalculadoraLimiteSlotsFinSemanaMensual.ObtenerMaximoSlotsFinSemanaPorMes(
            problema,
            slot.Fecha.Year,
            slot.Fecha.Month);
        if (slot.IndiceDia is 5 or 6 &&
            maximoSlotsFinSemanaMes is > 0 &&
            estado.SlotsFinSemanaPorEmpleadoMes.TryGetValue((empleado.Id, slot.Fecha.Year, slot.Fecha.Month), out var slotsFdsMes) &&
            slotsFdsMes >= maximoSlotsFinSemanaMes)
        {
            return true;
        }

        return false;
    }

    private static bool PuedeCubrirGrupo(Empleado empleado, string grupoId)
    {
        if (string.IsNullOrWhiteSpace(grupoId))
        {
            return true;
        }

        if (string.Equals(empleado.GrupoPrimarioId, grupoId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return empleado.GruposSecundariosIds.Contains(grupoId);
    }

    private static string FormatearFechas(IEnumerable<DateOnly> fechas)
    {
        return string.Join(", ", fechas.OrderBy(fecha => fecha).Select(fecha => fecha.ToString("yyyy-MM-dd")));
    }

    private static string ObtenerNombreGrupo(ProblemaRotacion problema, string grupoId)
    {
        var grupo = problema.Grupos.FirstOrDefault(grupo =>
            string.Equals(grupo.Id, grupoId, StringComparison.OrdinalIgnoreCase));

        return !string.IsNullOrWhiteSpace(grupo?.Nombre) ? grupo.Nombre : grupoId;
    }

    private static string FormatearRangoTurno(SlotTurno slot)
    {
        var sufijoFin = slot.FinLocal.Date > slot.InicioLocal.Date ? "+1" : string.Empty;
        return $"{slot.InicioLocal:HH:mm}-{slot.FinLocal:HH:mm}{sufijoFin}";
    }

    private static string FormatearNombres(ProblemaRotacion problema, IEnumerable<string> empleadosIds)
    {
        var nombresPorId = problema.Empleados.ToDictionary(empleado => empleado.Id, empleado => empleado.Nombre, StringComparer.OrdinalIgnoreCase);
        return string.Join(", ",
            empleadosIds
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(empleadoId => nombresPorId.TryGetValue(empleadoId, out var nombre) ? nombre : empleadoId)
                .OrderBy(nombre => nombre));
    }

    private static string FormatearRachasFds(ProblemaRotacion problema, EstadoResolucionSemanal estado)
    {
        var nombresPorId = problema.Empleados.ToDictionary(empleado => empleado.Id, empleado => empleado.Nombre, StringComparer.OrdinalIgnoreCase);
        return string.Join(", ",
            estado.RachaFinesSemanaConsecutivosPorEmpleado
                .Where(item => item.Value > 0)
                .OrderBy(item => nombresPorId.TryGetValue(item.Key, out var nombre) ? nombre : item.Key)
                .Select(item =>
                {
                    var nombre = nombresPorId.TryGetValue(item.Key, out var encontrado) ? encontrado : item.Key;
                    return $"{nombre}:{item.Value}";
                }));
    }

    private static string FormatearUltimosFines(ProblemaRotacion problema, EstadoResolucionSemanal estado)
    {
        var nombresPorId = problema.Empleados.ToDictionary(empleado => empleado.Id, empleado => empleado.Nombre, StringComparer.OrdinalIgnoreCase);
        return string.Join(", ",
            estado.UltimoFinTurnoPorEmpleado
                .OrderBy(item => nombresPorId.TryGetValue(item.Key, out var nombre) ? nombre : item.Key)
                .Take(12)
                .Select(item =>
                {
                    var nombre = nombresPorId.TryGetValue(item.Key, out var encontrado) ? encontrado : item.Key;
                    return $"{nombre}:{item.Value:yyyy-MM-dd HH:mm}";
                }));
    }

    private static string FormatearConteosMes(
        ProblemaRotacion problema,
        IReadOnlyDictionary<(string EmpleadoId, int Anio, int Mes), int> conteos)
    {
        var nombresPorId = problema.Empleados.ToDictionary(empleado => empleado.Id, empleado => empleado.Nombre, StringComparer.OrdinalIgnoreCase);
        return string.Join(", ",
            conteos
                .Where(item => item.Value > 0)
                .OrderBy(item => nombresPorId.TryGetValue(item.Key.EmpleadoId, out var nombre) ? nombre : item.Key.EmpleadoId)
                .ThenBy(item => item.Key.Anio)
                .ThenBy(item => item.Key.Mes)
                .Select(item =>
                {
                    var nombre = nombresPorId.TryGetValue(item.Key.EmpleadoId, out var encontrado) ? encontrado : item.Key.EmpleadoId;
                    return $"{nombre}:{item.Key.Anio}-{item.Key.Mes:00}={item.Value}";
                }));
    }

    private static string FormatearConteosMesFinDeSemana(
        ProblemaRotacion problema,
        IReadOnlyDictionary<(string EmpleadoId, int Anio, int Mes), int> conteos)
    {
        var nombresPorId = problema.Empleados.ToDictionary(empleado => empleado.Id, empleado => empleado.Nombre, StringComparer.OrdinalIgnoreCase);
        return string.Join(", ",
            conteos
                .Where(item => item.Value > 0)
                .OrderBy(item => nombresPorId.TryGetValue(item.Key.EmpleadoId, out var nombre) ? nombre : item.Key.EmpleadoId)
                .ThenBy(item => item.Key.Anio)
                .ThenBy(item => item.Key.Mes)
                .Select(item =>
                {
                    var nombre = nombresPorId.TryGetValue(item.Key.EmpleadoId, out var encontrado) ? encontrado : item.Key.EmpleadoId;
                    var maximo = CalculadoraLimiteSlotsFinSemanaMensual.ObtenerMaximoSlotsFinSemanaPorMes(
                        problema,
                        item.Key.Anio,
                        item.Key.Mes);
                    return $"{nombre}:{item.Key.Anio}-{item.Key.Mes:00}={item.Value}/{(maximo?.ToString() ?? "-")}";
                }));
    }

    private static string FormatearConteos(IReadOnlyDictionary<string, int> conteos)
    {
        return string.Join(", ", conteos.OrderBy(item => item.Key).Select(item => $"{item.Key}:{item.Value}"));
    }

    private static SolucionRotacionCp CrearSolucionAcumulada(
        ProblemaRotacion problema,
        List<AsignacionSlot> asignaciones,
        EstadoSolucionRotacion estado,
        string detalleEstado)
    {
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var asignacionesPorSlot = asignaciones
            .GroupBy(asignacion => asignacion.IdSlot)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count());

        var demandaTotal = problema.Slots.Sum(slot => slot.EmpleadosRequeridos);
        var faltantes = problema.Slots.Sum(slot =>
        {
            asignacionesPorSlot.TryGetValue(slot.Id, out var asignados);
            return Math.Max(0, slot.EmpleadosRequeridos - asignados);
        });

        return new SolucionRotacionCp
        {
            Estado = estado,
            DetalleEstado = detalleEstado,
            Asignaciones = [.. asignaciones],
            Metricas = new MetricasSolucionRotacion
            {
                SlotsAsignados = asignaciones.Count,
                SlotsSinAsignar = faltantes,
                AsignacionesAuxiliares = asignaciones.Count(asignacion => slotsPorId[asignacion.IdSlot].EsAuxiliar),
                AsignacionesNocturnas = asignaciones.Count(asignacion => slotsPorId[asignacion.IdSlot].EsTurnoNocturno)
            }
        };
    }
}
