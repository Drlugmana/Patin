using TurneroTcs.Services.RotacionV2.Application;
using TurneroTcs.Services.RotacionV2.Domain;
using Xunit;
using Xunit.Abstractions;

namespace TurneroTcs.Tests.RotacionV2;

public sealed class BatchControladoTests
{
    private static readonly DateTime FechaInicioLunes = new(2026, 4, 6, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime FechaInicioFeriados = new(2026, 4, 20, 0, 0, 0, DateTimeKind.Unspecified);
    private readonly ITestOutputHelper _salida;

    public BatchControladoTests(ITestOutputHelper salida)
    {
        _salida = salida;
    }

    [Fact]
    public void Resolver_DebeGenerarCuatroSemanasBatch_SinAusenciasNiFeriados()
    {
        var servicio = new ServicioRotacion();
        var plantilla = CrearPlantillaBatchControlada();
        var reglas = new ReglasRotacion
        {
            Obligatorias = new ReglasGlobalesObligatorias
            {
                MinutosObjetivoSemanales = 40 * 60,
                MinutosMinimosDescansoEntreTurnos = 8 * 60,
                MinimoDiasDescansoConsecutivosPorSemana = 2
            },
            Configurables = new PoliticasConfigurablesEquipo
            {
                EvitarFinesSemanaConsecutivos = true,
                MaximoFinesSemanaConsecutivos = 2,
                MaximoSlotsFinSemanaPorMes = 4,
                MaximoTurnosNocturnosPorMes = 10,
                BalancearHorasSemanales = true,
                BalancearTurnosNocturnos = true,
                BalancearCargaFeriados = false
            }
        };
        var problema = servicio.CrearProblema(
            plantilla,
            cantidadSemanas: 4,
            fechaInicio: FechaInicioLunes,
            reglas: reglas);

        var solucion = servicio.Resolver(
            plantilla,
            cantidadSemanas: 4,
            fechaInicio: FechaInicioLunes,
            reglas: reglas,
            opcionesSolver: new OpcionesSolverRotacion
            {
                TiempoMaximoResolucion = TimeSpan.FromSeconds(20),
                CantidadWorkers = 1
            });

        var agendaSemanal = RotacionV2TestHelper.ConstruirAgendaSemanal(problema, solucion, ResolverGrupoVisual);
        _salida.WriteLine(agendaSemanal);

        Assert.True(
            solucion.Estado is EstadoSolucionRotacion.Optima or EstadoSolucionRotacion.Factible,
            $"Estado inesperado: {solucion.Estado} / {solucion.DetalleEstado}{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");

        var asignacionesPorSemanaEmpleado = solucion.Asignaciones
            .GroupBy(asignacion =>
            {
                var partes = asignacion.IdSlot.Split(':');
                return new { EmpleadoId = asignacion.EmpleadoId, Semana = int.Parse(partes[0]) };
            })
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count());

        foreach (var empleadoId in ObtenerIdsEmpleados())
        {
            for (var semana = 0; semana < 4; semana++)
            {
                Assert.True(
                    asignacionesPorSemanaEmpleado.TryGetValue(new { EmpleadoId = empleadoId, Semana = semana }, out var cantidad) && cantidad == 5,
                    $"Empleado {empleadoId} no completo 5 turnos en la semana {semana}. Detalle={solucion.DetalleEstado}{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");
            }
        }

        ValidarNoHayVeladasConsecutivasEntreSemanas(problema, solucion, agendaSemanal);
    }

    [Fact]
    public void Resolver_DebeAplicarVisibilidadEspecialEnFeriadosBatch_SinAlterarPlanificacionBase()
    {
        var escenario = CrearEscenarioFeriadosBatch();

        var validaciones = new (string Nombre, Action Ejecutar)[]
        {
            ("Feriado 2026-04-30 selecciona tres coberturas visibles", () =>
                ValidarCoberturaVisiblePorFecha(
                    new DateOnly(2026, 4, 30),
                    escenario.Problema,
                    escenario.Solucion,
                    escenario.VisibilidadFeriados,
                    escenario.AgendaSemanal)),
            ("Feriado 2026-05-01 evita repetir visibles consecutivos", () =>
                ValidarNoRepiteVisiblesConsecutivos(
                    new DateOnly(2026, 4, 30),
                    new DateOnly(2026, 5, 1),
                    escenario.VisibilidadFeriados,
                    escenario.AgendaSemanal)),
            ("Semana con feriados reduce carga no-feriado obligatoria", () =>
                ValidarCreditoSemanalPorFeriados(
                    escenario.Problema,
                    escenario.Solucion,
                    escenario.AgendaSemanal)),
            ("Feriado 2026-05-25 conserva tres coberturas visibles", () =>
                ValidarCoberturaVisiblePorFecha(
                    new DateOnly(2026, 5, 25),
                    escenario.Problema,
                    escenario.Solucion,
                    escenario.VisibilidadFeriados,
                    escenario.AgendaSemanal)),
            ("Agenda semanal reporta ocultados por semana", () =>
            {
                Assert.Contains("Fechas semana: 2026-04-20 -> 2026-04-26", escenario.AgendaSemanal);
                Assert.Contains("Vacaciones semana: Giselle[2026-04-20..2026-04-26]", escenario.AgendaSemanal);
                Assert.Contains("Vacaciones semana: Giselle[2026-04-27..2026-04-30]", escenario.AgendaSemanal);
                Assert.Contains("Vacaciones semana: Xavier M[2026-05-06..2026-05-10]", escenario.AgendaSemanal);
                Assert.Contains("Vacaciones semana: Xavier M[2026-05-11..2026-05-14]", escenario.AgendaSemanal);
                Assert.Contains("Ocultados por feriado: 2026-04-30:", escenario.AgendaSemanal);
                Assert.Contains("2026-05-01:", escenario.AgendaSemanal);
                Assert.Contains("2026-05-25:", escenario.AgendaSemanal);
            })
        };

        var errores = new List<string>();
        var exitos = 0;

        foreach (var validacion in validaciones)
        {
            try
            {
                validacion.Ejecutar();
                exitos++;
                _salida.WriteLine($"[OK] {validacion.Nombre}");
            }
            catch (Exception ex)
            {
                errores.Add($"[FAIL] {validacion.Nombre}: {ex.Message}");
                _salida.WriteLine($"[FAIL] {validacion.Nombre}: {ex.Message}");
            }
        }

        var porcentajeExito = (double)exitos / validaciones.Length * 100d;
        _salida.WriteLine($"Porcentaje exito helper feriados: {porcentajeExito:0.##}% ({exitos}/{validaciones.Length})");

        Assert.True(
            errores.Count == 0,
            $"Porcentaje exito helper feriados: {porcentajeExito:0.##}% ({exitos}/{validaciones.Length}){Environment.NewLine}{string.Join(Environment.NewLine, errores)}{Environment.NewLine}{Environment.NewLine}{escenario.AgendaSemanal}");
    }

    [Fact]
    public void HelperVisibilidadFeriados_DebeSeleccionarVisiblesEsperados_EnFeriado_2026_04_30()
    {
        var escenario = CrearEscenarioFeriadosBatch();
        ValidarCoberturaVisiblePorFecha(
            new DateOnly(2026, 4, 30),
            escenario.Problema,
            escenario.Solucion,
            escenario.VisibilidadFeriados,
            escenario.AgendaSemanal);
    }

    [Fact]
    public void HelperVisibilidadFeriados_DebeEvitarRepetirVisiblesConsecutivos_EnFeriado_2026_05_01()
    {
        var escenario = CrearEscenarioFeriadosBatch();
        ValidarNoRepiteVisiblesConsecutivos(
            new DateOnly(2026, 4, 30),
            new DateOnly(2026, 5, 1),
            escenario.VisibilidadFeriados,
            escenario.AgendaSemanal);
    }

    [Fact]
    public void HelperVisibilidadFeriados_DebeBalancearHistoricoVisible_EnFeriado_2026_05_25()
    {
        var escenario = CrearEscenarioFeriadosBatch();
        ValidarCoberturaVisiblePorFecha(
            new DateOnly(2026, 5, 25),
            escenario.Problema,
            escenario.Solucion,
            escenario.VisibilidadFeriados,
            escenario.AgendaSemanal);
    }

    [Fact]
    public void EscenarioFeriados_DebeAplicarCreditoSemanalPorDiasFeriado()
    {
        var escenario = CrearEscenarioFeriadosBatch();
        ValidarCreditoSemanalPorFeriados(escenario.Problema, escenario.Solucion, escenario.AgendaSemanal);
    }

    [Fact]
    public void Resolver_DebeUsarAuxiliaresParaCompletarCuatroTurnosNoFeriado_ConFeriadoYVacaciones()
    {
        var servicio = new ServicioRotacion();
        var plantilla = CrearPlantillaFeriadoLunesConAuxiliares();
        var fechaInicio = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Unspecified);
        var feriados = new HashSet<DateOnly> { new(2026, 5, 25) };
        var vacaciones = new Dictionary<string, HashSet<DateOnly>>(StringComparer.OrdinalIgnoreCase)
        {
            ["P1"] = CrearRangoFechas(new DateOnly(2026, 5, 25), new DateOnly(2026, 5, 25)),
            ["P2"] = CrearRangoFechas(new DateOnly(2026, 5, 25), new DateOnly(2026, 5, 27))
        };
        var reglas = CrearReglasBatchControladas();
        var problema = servicio.CrearProblema(
            plantilla,
            cantidadSemanas: 1,
            fechaInicio: fechaInicio,
            vacacionesPorPersonaId: vacaciones,
            feriados: feriados,
            reglas: reglas);

        var solucion = servicio.Resolver(
            plantilla,
            cantidadSemanas: 1,
            fechaInicio: fechaInicio,
            vacacionesPorPersonaId: vacaciones,
            feriados: feriados,
            reglas: reglas,
            opcionesSolver: new OpcionesSolverRotacion
            {
                TiempoMaximoResolucion = TimeSpan.FromSeconds(10),
                CantidadWorkers = 1,
                SemillaAleatoria = 1
            });

        var agendaSemanal = RotacionV2TestHelper.ConstruirAgendaSemanal(problema, solucion, ResolverGrupoVisual);
        _salida.WriteLine(agendaSemanal);

        Assert.True(
            solucion.Estado is EstadoSolucionRotacion.Optima or EstadoSolucionRotacion.Factible,
            $"Estado inesperado: {solucion.Estado} / {solucion.DetalleEstado}{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");

        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var turnosNoFeriadoPorEmpleado = solucion.Asignaciones
            .Where(asignacion => !problema.Feriados.Contains(slotsPorId[asignacion.IdSlot].Fecha))
            .GroupBy(asignacion => asignacion.EmpleadoId)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count(), StringComparer.OrdinalIgnoreCase);

        Assert.Equal(4, turnosNoFeriadoPorEmpleado.GetValueOrDefault("P1"));
        Assert.Equal(0, turnosNoFeriadoPorEmpleado.GetValueOrDefault("P2"));
        Assert.Equal(4, turnosNoFeriadoPorEmpleado.GetValueOrDefault("P3"));
        Assert.Equal(4, turnosNoFeriadoPorEmpleado.GetValueOrDefault("P4"));
        Assert.Equal(4, turnosNoFeriadoPorEmpleado.GetValueOrDefault("P5"));

        var auxiliaresUsados = solucion.Asignaciones.Count(asignacion => slotsPorId[asignacion.IdSlot].EsAuxiliar);
        Assert.Equal(4, auxiliaresUsados);
    }

    [Fact]
    public void RotacionV2TestHelper_DebeReportarOcultadosPorFeriado_EnAgendaSemanal()
    {
        var escenario = CrearEscenarioFeriadosBatch();

        Assert.Contains(
            "Fechas semana: 2026-04-20 -> 2026-04-26",
            escenario.AgendaSemanal);
        Assert.Contains(
            "Fechas semana: 2026-05-25 -> 2026-05-31",
            escenario.AgendaSemanal);
        Assert.Contains(
            "Vacaciones semana: Giselle[2026-04-20..2026-04-26]",
            escenario.AgendaSemanal);
        Assert.Contains(
            "Vacaciones semana: Giselle[2026-04-27..2026-04-30]",
            escenario.AgendaSemanal);
        Assert.Contains(
            "Ocultados por feriado: 2026-04-30:",
            escenario.AgendaSemanal);
        Assert.Contains(
            "2026-05-01:",
            escenario.AgendaSemanal);
        Assert.Contains(
            "2026-05-25:",
            escenario.AgendaSemanal);
    }

    [Fact]
    public void EscenarioFeriados_DebeMantenerApoyoControlMDelEscenarioBase()
    {
        var escenario = CrearEscenarioFeriadosBatch();
        ValidarApoyoControlMEnBatchDuranteVacacionesDeGiselle(escenario.Problema, escenario.Solucion, escenario.AgendaSemanal);
        ValidarControlMConCoberturaCedida(escenario.Problema, escenario.Solucion, escenario.AgendaSemanal);
    }

    [Fact]
    public void EscenarioFeriados_DebeRespetarDescansoMinimoDe8Horas()
    {
        var escenario = CrearEscenarioFeriadosBatch();
        ValidarDescansoMinimo(escenario.Problema, escenario.Solucion, escenario.AgendaSemanal);
    }

    private static void ValidarNoHayVeladasConsecutivasEntreSemanas(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        string agendaSemanal)
    {
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);

        foreach (var empleado in problema.Empleados)
        {
            for (var semana = 0; semana < problema.CantidadSemanas - 1; semana++)
            {
                var domingoNocturno = solucion.Asignaciones.Any(asignacion =>
                    asignacion.EmpleadoId == empleado.Id &&
                    slotsPorId[asignacion.IdSlot].IndiceSemana == semana &&
                    slotsPorId[asignacion.IdSlot].IndiceDia == 6 &&
                    slotsPorId[asignacion.IdSlot].EsTurnoNocturno);

                var lunesNocturno = solucion.Asignaciones.Any(asignacion =>
                    asignacion.EmpleadoId == empleado.Id &&
                    slotsPorId[asignacion.IdSlot].IndiceSemana == semana + 1 &&
                    slotsPorId[asignacion.IdSlot].IndiceDia == 0 &&
                    slotsPorId[asignacion.IdSlot].EsTurnoNocturno);

                Assert.False(
                    domingoNocturno && lunesNocturno,
                    $"Velada consecutiva entre semanas detectada para {empleado.Nombre} entre S{semana + 1} y S{semana + 2}.{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");
            }
        }
    }

    private static string ResolverGrupoVisual(SlotTurno slot, Empleado empleado)
    {
        if (!string.IsNullOrWhiteSpace(slot.GrupoId))
        {
            return slot.GrupoId;
        }

        if (slot.EsAuxiliar && slot.LlaveCompartidaAuxiliar.StartsWith("BATCH", StringComparison.OrdinalIgnoreCase))
        {
            return "Batch";
        }

        return empleado.GrupoPrimarioId;
    }

    private static void ValidarCoberturaVisiblePorFecha(
        DateOnly fecha,
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        ResultadoVisibilidadFeriados visibilidadFeriados,
        string agendaSemanal)
    {
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var coberturasEsperadas = new[] { "MANANA", "TARDE", "NOCHE" };
        var visiblesFecha = ObtenerVisiblesPorFecha(fecha, visibilidadFeriados, agendaSemanal);

        Assert.Equal(
            coberturasEsperadas.Length,
            visiblesFecha.Count);

        Assert.Equal(
            coberturasEsperadas.Length,
            visiblesFecha
                .Select(item => item.CoberturaId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        foreach (var cobertura in coberturasEsperadas)
        {
            Assert.Contains(
                visiblesFecha,
                item => string.Equals(item.CoberturaId, cobertura, StringComparison.OrdinalIgnoreCase));
        }

        Assert.Equal(
            coberturasEsperadas.Length,
            visiblesFecha
                .Select(item => item.EmpleadoId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        Assert.All(
            visiblesFecha
                .Select(item => slotsPorId[item.IdSlot]),
            slot =>
            {
                Assert.True(new[] { "TT03", "TT01", "TT02" }.Contains(slot.TipoTurnoId, StringComparer.OrdinalIgnoreCase));
                Assert.True(new[] { "Batch", "Servidores" }.Contains(slot.GrupoId, StringComparer.OrdinalIgnoreCase));
            });

        Assert.True(
            visibilidadFeriados.OcultamientosPorSemana.TryGetValue(visiblesFecha[0].IndiceSemana, out var ocultadosSemana) &&
            ocultadosSemana.Any(item => item.Fecha == fecha),
            $"No se registraron ocultados para feriado {fecha:yyyy-MM-dd}.{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");

        var totalAsignacionesFecha = solucion.Asignaciones.Count(asignacion => slotsPorId[asignacion.IdSlot].Fecha == fecha);

        Assert.Equal(
            totalAsignacionesFecha,
            visiblesFecha.Count +
            visibilidadFeriados.OcultamientosPorSemana[visiblesFecha[0].IndiceSemana].Count(item => item.Fecha == fecha));
    }

    private static void ValidarNoRepiteVisiblesConsecutivos(
        DateOnly fechaAnterior,
        DateOnly fechaActual,
        ResultadoVisibilidadFeriados visibilidadFeriados,
        string agendaSemanal)
    {
        var visiblesAnterior = ObtenerVisiblesPorFecha(fechaAnterior, visibilidadFeriados, agendaSemanal)
            .Select(item => item.EmpleadoId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var visiblesActual = ObtenerVisiblesPorFecha(fechaActual, visibilidadFeriados, agendaSemanal)
            .Select(item => item.EmpleadoId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Empty(visiblesAnterior.Intersect(visiblesActual, StringComparer.OrdinalIgnoreCase));
    }

    private static List<AsignacionVisibleFeriado> ObtenerVisiblesPorFecha(
        DateOnly fecha,
        ResultadoVisibilidadFeriados visibilidadFeriados,
        string agendaSemanal)
    {
        Assert.True(
            visibilidadFeriados.VisiblesPorFecha.TryGetValue(fecha, out var visiblesFecha),
            $"No hubo proyeccion visual para feriado {fecha:yyyy-MM-dd}.{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");

        return visiblesFecha;
    }

    private static void ValidarCreditoSemanalPorFeriados(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        string agendaSemanal)
    {
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);

        foreach (var empleado in problema.Empleados)
        {
            for (var indiceSemana = 0; indiceSemana < problema.CantidadSemanas; indiceSemana++)
            {
                var creditoFeriado = problema.Feriados.Count(fecha =>
                    fecha >= problema.FechaInicio.AddDays(indiceSemana * 7) &&
                    fecha <= problema.FechaInicio.AddDays(indiceSemana * 7 + 6) &&
                    fecha.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday);

                if (creditoFeriado == 0)
                {
                    continue;
                }

                var turnosNoFeriadoEsperados = CalcularTurnosNoFeriadoEsperados(problema, empleado, indiceSemana, 5 - creditoFeriado);
                var turnosNoFeriadoActuales = solucion.Asignaciones.Count(asignacion =>
                    asignacion.EmpleadoId == empleado.Id &&
                    slotsPorId[asignacion.IdSlot].IndiceSemana == indiceSemana &&
                    !problema.Feriados.Contains(slotsPorId[asignacion.IdSlot].Fecha));

                Assert.Equal(
                    turnosNoFeriadoEsperados,
                    turnosNoFeriadoActuales);
            }
        }
    }

    private static int CalcularTurnosNoFeriadoEsperados(
        ProblemaRotacion problema,
        Empleado empleado,
        int indiceSemana,
        int turnosObjetivoBaseNoFeriado)
    {
        if (turnosObjetivoBaseNoFeriado <= 0)
        {
            return 0;
        }

        var fechasBloqueadas = CalcularFechasBloqueadasPorVacacion(problema, empleado.Id);
        var fechasDisponibles = problema.Slots
            .Where(slot => slot.IndiceSemana == indiceSemana)
            .Select(slot => slot.Fecha)
            .Distinct()
            .Where(fecha =>
                !problema.Feriados.Contains(fecha) &&
                !fechasBloqueadas.Contains(fecha) &&
                problema.Slots.Any(slot =>
                    slot.IndiceSemana == indiceSemana &&
                    slot.Fecha == fecha &&
                    PuedeCubrirGrupo(empleado, slot.GrupoId)))
            .ToHashSet();

        var soloFinDeSemanaDisponible = fechasBloqueadas
            .Any(fecha => problema.Slots.Any(slot => slot.IndiceSemana == indiceSemana && slot.Fecha == fecha)) &&
            fechasDisponibles.Count > 0 &&
            fechasDisponibles.All(fecha =>
                problema.Slots.Any(slot =>
                    slot.IndiceSemana == indiceSemana &&
                    slot.Fecha == fecha &&
                    slot.IndiceDia is 5 or 6));

        if (soloFinDeSemanaDisponible)
        {
            return 0;
        }

        return Math.Min(
            turnosObjetivoBaseNoFeriado,
            CalcularMaximoFechasTrabajables(problema, empleado, indiceSemana, fechasDisponibles));
    }

    private static int CalcularMaximoFechasTrabajables(
        ProblemaRotacion problema,
        Empleado empleado,
        int indiceSemana,
        IReadOnlySet<DateOnly> fechasDisponibles)
    {
        if (fechasDisponibles.Count == 0)
        {
            return 0;
        }

        var fechasOrdenadas = fechasDisponibles
            .OrderBy(fecha => fecha)
            .ToArray();
        var mejor = 0;
        var totalPatrones = 1 << fechasOrdenadas.Length;

        for (var mascara = 0; mascara < totalPatrones; mascara++)
        {
            var fechasTrabajadas = new HashSet<DateOnly>();
            var cantidadTrabajada = 0;

            for (var indice = 0; indice < fechasOrdenadas.Length; indice++)
            {
                if ((mascara & (1 << indice)) == 0)
                {
                    continue;
                }

                fechasTrabajadas.Add(fechasOrdenadas[indice]);
                cantidadTrabajada++;
            }

            if (cantidadTrabajada <= mejor)
            {
                continue;
            }

            if (!CumpleDescansoPosteriorVacacionSemanal(problema, empleado.Id, indiceSemana, fechasTrabajadas))
            {
                continue;
            }

            mejor = cantidadTrabajada;
        }

        return mejor;
    }

    private static bool CumpleDescansoPosteriorVacacionSemanal(
        ProblemaRotacion problema,
        string empleadoId,
        int indiceSemana,
        IReadOnlySet<DateOnly> fechasTrabajadas)
    {
        var fechaInicioSemana = problema.FechaInicio.AddDays(indiceSemana * 7);
        var fechaFinSemana = fechaInicioSemana.AddDays(6);
        var descansosSemana = problema.DescansosPosterioresVacacion
            .Where(descanso =>
                string.Equals(descanso.EmpleadoId, empleadoId, StringComparison.OrdinalIgnoreCase) &&
                descanso.FechaRegreso >= fechaInicioSemana &&
                descanso.FechaRegreso <= fechaFinSemana)
            .OrderBy(descanso => descanso.FechaRegreso)
            .ToArray();

        foreach (var descanso in descansosSemana)
        {
            var cumpleAlguno = false;
            for (var fecha = descanso.FechaRegreso; fecha < fechaFinSemana; fecha = fecha.AddDays(1))
            {
                if (!fechasTrabajadas.Contains(fecha) && !fechasTrabajadas.Contains(fecha.AddDays(1)))
                {
                    cumpleAlguno = true;
                    break;
                }
            }

            if (!cumpleAlguno)
            {
                return false;
            }
        }

        return true;
    }

    private static HashSet<DateOnly> CalcularFechasBloqueadasPorVacacion(ProblemaRotacion problema, string empleadoId)
    {
        return problema.Ausencias
            .Where(ausencia => string.Equals(ausencia.EmpleadoId, empleadoId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(ausencia => ausencia.Fechas)
            .ToHashSet();
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

    private static void ValidarDescansoMinimo(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        string agendaSemanal)
    {
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var asignacionesPorEmpleado = solucion.Asignaciones
            .GroupBy(asignacion => asignacion.EmpleadoId);

        foreach (var grupoEmpleado in asignacionesPorEmpleado)
        {
            var slotsOrdenados = grupoEmpleado
                .Select(asignacion => slotsPorId[asignacion.IdSlot])
                .OrderBy(slot => slot.InicioLocal)
                .ToArray();

            for (var indice = 1; indice < slotsOrdenados.Length; indice++)
            {
                var previo = slotsOrdenados[indice - 1];
                var actual = slotsOrdenados[indice];
                var descanso = actual.InicioLocal - previo.FinLocal;

                Assert.True(
                    descanso >= TimeSpan.FromHours(8),
                    $"Descanso invalido para {grupoEmpleado.Key}: {descanso.TotalHours:0.##}h entre {previo.Fecha:yyyy-MM-dd} {previo.CodigoTurno} ({previo.InicioLocal:MM-dd HH:mm}-{previo.FinLocal:MM-dd HH:mm}) y {actual.Fecha:yyyy-MM-dd} {actual.CodigoTurno} ({actual.InicioLocal:MM-dd HH:mm}-{actual.FinLocal:MM-dd HH:mm}).{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");
            }
        }
    }

    private static void ValidarApoyoControlMEnBatchDuranteVacacionesDeGiselle(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        string agendaSemanal)
    {
        var inicio = new DateOnly(2026, 4, 20);
        var fin = new DateOnly(2026, 4, 30);
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var empleadosPorId = problema.Empleados.ToDictionary(empleado => empleado.Id);

        var apoyos = solucion.Asignaciones.Count(asignacion =>
        {
            var slot = slotsPorId[asignacion.IdSlot];
            if (slot.EsAuxiliar || slot.Fecha < inicio || slot.Fecha > fin || !string.Equals(slot.GrupoId, "Batch", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(empleadosPorId[asignacion.EmpleadoId].GrupoPrimarioId, "Control M", StringComparison.OrdinalIgnoreCase);
        });

        Assert.True(
            apoyos > 0,
            $"No hubo apoyo de Control M hacia Batch durante las vacaciones de Giselle.{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");
    }

    private static void ValidarControlMConCoberturaCedida(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        string agendaSemanal)
    {
        var slotsPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var asignacionesPorSlot = solucion.Asignaciones
            .GroupBy(asignacion => asignacion.IdSlot)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count());

        var slotsControlMSinCubrir = problema.Slots
            .Where(slot =>
                string.Equals(slot.GrupoId, "Control M", StringComparison.OrdinalIgnoreCase) &&
                slot.Fecha >= new DateOnly(2026, 5, 6) &&
                slot.Fecha <= new DateOnly(2026, 5, 14))
            .Count(slot => !asignacionesPorSlot.ContainsKey(slot.Id));

        Assert.True(
            slotsControlMSinCubrir > 0,
            $"No se detecto cobertura cedida en Control M durante las vacaciones de Xavier M.{Environment.NewLine}{Environment.NewLine}{agendaSemanal}");
    }

    private EscenarioPruebaFeriadosBatch CrearEscenarioFeriadosBatch()
    {
        var servicio = new ServicioRotacion();
        var plantilla = CrearPlantillaBatchConControlM();
        var feriados = CrearFeriadosBatch();
        var vacaciones = CrearVacacionesBatchConFeriados();
        var reglas = CrearReglasBatchControladas();
        var problema = servicio.CrearProblema(
            plantilla,
            cantidadSemanas: 6,
            fechaInicio: FechaInicioFeriados,
            vacacionesPorPersonaId: vacaciones,
            feriados: feriados,
            reglas: reglas);
        var solucion = servicio.Resolver(
            plantilla,
            cantidadSemanas: 6,
            fechaInicio: FechaInicioFeriados,
            vacacionesPorPersonaId: vacaciones,
            feriados: feriados,
            reglas: reglas,
            opcionesSolver: new OpcionesSolverRotacion
            {
                TiempoMaximoResolucion = TimeSpan.FromSeconds(30),
                CantidadWorkers = 1,
                SemillaAleatoria = 1
            });

        Assert.True(
            solucion.Estado is EstadoSolucionRotacion.Optima or EstadoSolucionRotacion.Factible,
            $"Estado inesperado en escenario feriados: {solucion.Estado} / {solucion.DetalleEstado}");

        var visibilidadFeriados = HelperVisibilidadFeriados.Calcular(
            problema,
            solucion,
            CrearConfiguracionVisibilidadFeriadoBatch());
        var agendaSemanal = RotacionV2TestHelper.ConstruirAgendaSemanal(
            problema,
            solucion,
            ResolverGrupoVisual,
            visibilidadFeriados);

        _salida.WriteLine(agendaSemanal);

        return new EscenarioPruebaFeriadosBatch
        {
            Problema = problema,
            Solucion = solucion,
            VisibilidadFeriados = visibilidadFeriados,
            AgendaSemanal = agendaSemanal
        };
    }

    private static ReglasRotacion CrearReglasBatchControladas()
    {
        return new ReglasRotacion
        {
            Obligatorias = new ReglasGlobalesObligatorias
            {
                MinutosObjetivoSemanales = 40 * 60,
                MinutosMinimosDescansoEntreTurnos = 8 * 60,
                MinimoDiasDescansoConsecutivosPorSemana = 2
            },
            Configurables = new PoliticasConfigurablesEquipo
            {
                AplicarVacaciones = true,
                PermiteTurnosAuxiliares = true,
                EvitarFinesSemanaConsecutivos = true,
                MaximoFinesSemanaConsecutivos = 2,
                MaximoSlotsFinSemanaPorMes = 4,
                MaximoTurnosNocturnosPorMes = 10,
                BalancearHorasSemanales = true,
                BalancearTurnosNocturnos = true,
                BalancearCargaFeriados = false
            }
        };
    }

    private static HashSet<DateOnly> CrearFeriadosBatch()
    {
        return
        [
            new DateOnly(2026, 4, 30),
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 25)
        ];
    }

    private static Dictionary<string, HashSet<DateOnly>> CrearVacacionesBatchConFeriados()
    {
        return new Dictionary<string, HashSet<DateOnly>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Giselle"] = CrearRangoFechas(new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 30)),
            ["Xavier M"] = CrearRangoFechas(new DateOnly(2026, 5, 6), new DateOnly(2026, 5, 14))
        };
    }

    private static HashSet<DateOnly> CrearRangoFechas(DateOnly inicio, DateOnly fin)
    {
        var fechas = new HashSet<DateOnly>();
        for (var fecha = inicio; fecha <= fin; fecha = fecha.AddDays(1))
        {
            fechas.Add(fecha);
        }

        return fechas;
    }

    private static ConfiguracionVisibilidadFeriado CrearConfiguracionVisibilidadFeriadoBatch()
    {
        return new ConfiguracionVisibilidadFeriado
        {
            EquipoId = "Batch",
            Coberturas =
            [
                new CoberturaVisibilidadFeriado
                {
                    Id = "MANANA",
                    TiposTurnoIds = new HashSet<string>(["TT03"], StringComparer.OrdinalIgnoreCase),
                    GruposIncluidos = new HashSet<string>(["Batch", "Servidores"], StringComparer.OrdinalIgnoreCase),
                    PersonasVisibles = 1
                },
                new CoberturaVisibilidadFeriado
                {
                    Id = "TARDE",
                    TiposTurnoIds = new HashSet<string>(["TT01"], StringComparer.OrdinalIgnoreCase),
                    GruposIncluidos = new HashSet<string>(["Batch", "Servidores"], StringComparer.OrdinalIgnoreCase),
                    PersonasVisibles = 1
                },
                new CoberturaVisibilidadFeriado
                {
                    Id = "NOCHE",
                    TiposTurnoIds = new HashSet<string>(["TT02"], StringComparer.OrdinalIgnoreCase),
                    GruposIncluidos = new HashSet<string>(["Batch", "Servidores"], StringComparer.OrdinalIgnoreCase),
                    PersonasVisibles = 1
                }
            ]
        };
    }

    private static Plantilla CrearPlantillaFeriadoLunesConAuxiliares()
    {
        var turnosBase = new List<Turno>();
        var diasBase = new[] { "martes", "miercoles", "jueves", "viernes" };
        var numeroTurno = 1;

        foreach (var dia in diasBase)
        {
            turnosBase.Add(CrearTurno(numeroTurno++, "Batch", dia, "TT03", "BATCH_1", 7, 0, 16, 0, 1, 1, false));
            turnosBase.Add(CrearTurno(numeroTurno++, "Batch", dia, "TT04", "BATCH_2", 8, 0, 17, 0, 1, 1, false));
            turnosBase.Add(CrearTurno(numeroTurno++, "Servidores", dia, "TT05", "SERV_1", 9, 0, 18, 0, 1, 1, false));
        }

        var auxiliares = new List<Turno>();
        var diasAuxiliares = new[] { "jueves", "viernes", "sabado", "domingo" };
        for (var indice = 0; indice < diasAuxiliares.Length; indice++)
        {
            auxiliares.Add(CrearTurno(
                1000 + indice,
                string.Empty,
                diasAuxiliares[indice],
                "TT99",
                "AUX_COMPARTIDO",
                18,
                0,
                2,
                0,
                minimoPersonas: 0,
                capacidadPlanificada: 1,
                esAuxiliar: true,
                auxiliarSharedKey: "BATCH_SERVIDORES_AUX",
                auxiliarMaxCompartido: 1));
        }

        return new Plantilla
        {
            Nombre = "FeriadoConAuxiliares",
            GrupoId = "Batch",
            PersonaTurno =
            [
                CrearPersona("P1", 1, "P1", "Batch", "Servidores"),
                CrearPersona("P2", 2, "P2", "Servidores", "Batch"),
                CrearPersona("P3", 3, "P3", "Batch", "Servidores"),
                CrearPersona("P4", 4, "P4", "Servidores", "Batch"),
                CrearPersona("P5", 5, "P5", "Batch", "Servidores")
            ],
            Turnos = turnosBase,
            TurnosAuxiliares = auxiliares
        };
    }

    private static Plantilla CrearPlantillaBatchConControlM()
    {
        return new Plantilla
        {
            Nombre = "BatchControlM",
            GrupoId = "Batch",
            PersonaTurno = CrearPersonasEscenarioReal(),
            Turnos = CrearTurnosPrincipales().Concat(CrearTurnosControlM()).ToList(),
            TurnosAuxiliares = CrearTurnosAuxiliares().ToList()
        };
    }

    private static List<PersonaTurno> CrearPersonasEscenarioReal()
    {
        return
        [
            CrearPersona("Amilcar", 1, "Amilcar", "Batch", "Servidores"),
            CrearPersona("Andrea", 2, "Andrea", "Batch", "Servidores"),
            CrearPersona("Carlos", 3, "Carlos", "Servidores"),
            CrearPersona("Cesar", 4, "Cesar", "Batch", "Servidores"),
            CrearPersona("Dario", 5, "Dario", "Servidores"),
            CrearPersona("Giselle", 6, "Giselle", "Servidores"),
            CrearPersona("Lizbeth", 7, "Lizbeth", "Servidores"),
            CrearPersona("Luis", 8, "Luis", "Batch"),
            CrearPersona("Javier R", 9, "Javier R", "Control M", "Batch"),
            CrearPersona("Xavier M", 10, "Xavier M", "Control M", "Batch")
        ];
    }

    private static Plantilla CrearPlantillaBatchControlada()
    {
        return new Plantilla
        {
            Nombre = "Batch",
            GrupoId = "Batch",
            PersonaTurno = CrearPersonasBatchYServidores(),
            Turnos = CrearTurnosPrincipales(),
            TurnosAuxiliares = CrearTurnosAuxiliares()
        };
    }

    private static List<PersonaTurno> CrearPersonasBatchYServidores()
    {
        return
        [
            CrearPersona("Amilcar", 1, "Amilcar", "Batch", "Servidores"),
            CrearPersona("Andrea", 2, "Andrea", "Batch", "Servidores"),
            CrearPersona("Carlos", 3, "Carlos", "Servidores"),
            CrearPersona("Cesar", 4, "Cesar", "Batch", "Servidores"),
            CrearPersona("Dario", 5, "Dario", "Servidores"),
            CrearPersona("Giselle", 6, "Giselle", "Servidores"),
            CrearPersona("Lizbeth", 7, "Lizbeth", "Servidores"),
            CrearPersona("Luis", 8, "Luis", "Batch")
        ];
    }

    private static PersonaTurno CrearPersona(string id, int numero, string nombre, string grupoPrimario, params string[] gruposPermitidos)
    {
        return new PersonaTurno
        {
            PersonaId = id,
            Nombre = nombre,
            Numero = numero,
            Grupo = grupoPrimario,
            GrupoId = grupoPrimario,
            GruposSecundarios = gruposPermitidos
                .Where(grupo => !string.Equals(grupo, grupoPrimario, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static List<Turno> CrearTurnosPrincipales()
    {
        var turnos = new List<Turno>();
        var diasSemana = new[] { "lunes", "martes", "miercoles", "jueves", "viernes" };
        var diasFinSemana = new[] { "sabado", "domingo" };
        var turnosPrincipales = new[]
        {
            new { TipoTurnoId = "TT03", TipoHorario = "MANANA", Inicio = (7, 0), Fin = (16, 0), GrupoId = "Batch" },
            new { TipoTurnoId = "TT03", TipoHorario = "MANANA", Inicio = (7, 0), Fin = (16, 0), GrupoId = "Servidores" },
            new { TipoTurnoId = "TT01", TipoHorario = "TARDE", Inicio = (15, 0), Fin = (0, 0), GrupoId = "Batch" },
            new { TipoTurnoId = "TT01", TipoHorario = "TARDE", Inicio = (15, 0), Fin = (0, 0), GrupoId = "Servidores" },
            new { TipoTurnoId = "TT02", TipoHorario = "NOCHE", Inicio = (23, 0), Fin = (8, 0), GrupoId = "Batch" },
            new { TipoTurnoId = "TT02", TipoHorario = "NOCHE", Inicio = (23, 0), Fin = (8, 0), GrupoId = "Servidores" }
        };

        var numeroTurno = 1;
        foreach (var dia in diasSemana)
        {
            foreach (var definicion in turnosPrincipales)
            {
                turnos.Add(CrearTurno(
                    numeroTurno++,
                    definicion.GrupoId,
                    dia,
                    definicion.TipoTurnoId,
                    definicion.TipoHorario,
                    definicion.Inicio.Item1,
                    definicion.Inicio.Item2,
                    definicion.Fin.Item1,
                    definicion.Fin.Item2,
                    minimoPersonas: 1,
                    capacidadPlanificada: 1,
                    esAuxiliar: false));
            }
        }

        foreach (var dia in diasFinSemana)
        {
            turnos.Add(CrearTurno(numeroTurno++, string.Empty, dia, "TT03", "MANANA", 7, 0, 16, 0, 1, 1, false));
            turnos.Add(CrearTurno(numeroTurno++, string.Empty, dia, "TT01", "TARDE", 15, 0, 0, 0, 1, 1, false));
            turnos.Add(CrearTurno(numeroTurno++, string.Empty, dia, "TT02", "NOCHE", 23, 0, 8, 0, 1, 1, false));
        }

        return turnos;
    }

    private static IEnumerable<Turno> CrearTurnosControlM()
    {
        var turnos = new List<Turno>();
        var dias = new[] { "lunes", "martes", "miercoles", "jueves", "viernes" };
        var numeroTurno = 500;

        foreach (var dia in dias)
        {
            turnos.Add(CrearTurno(
                numeroTurno++,
                "Control M",
                dia,
                "TT09",
                "CONTROL_MANANA",
                9,
                0,
                18,
                0,
                minimoPersonas: 1,
                capacidadPlanificada: 1,
                esAuxiliar: false,
                maximoApoyoCedible: 1));

            turnos.Add(CrearTurno(
                numeroTurno++,
                "Control M",
                dia,
                "TT01",
                "CONTROL_TARDE",
                15,
                0,
                0,
                0,
                minimoPersonas: 1,
                capacidadPlanificada: 1,
                esAuxiliar: false,
                puedeOmitirsePorVacacion: true));
        }

        return turnos;
    }

    private static List<Turno> CrearTurnosAuxiliares()
    {
        var turnos = new List<Turno>();
        var dias = new[] { "lunes", "martes", "miercoles", "jueves", "viernes" };

        for (var indice = 0; indice < dias.Length; indice++)
        {
            turnos.Add(CrearTurno(
                1000 + indice,
                string.Empty,
                dias[indice],
                "TT05",
                "NOCHE_MADRUGADA",
                18,
                0,
                3,
                0,
                minimoPersonas: 0,
                capacidadPlanificada: 1,
                esAuxiliar: true,
                auxiliarSharedKey: "BATCH_TT05",
                auxiliarMaxCompartido: 1));
        }

        return turnos;
    }

    private static Turno CrearTurno(
        int numeroTurno,
        string grupoId,
        string dia,
        string tipoTurnoId,
        string tipoHorario,
        int horaInicio,
        int minutoInicio,
        int horaFin,
        int minutoFin,
        int minimoPersonas,
        int capacidadPlanificada,
        bool esAuxiliar,
        string auxiliarSharedKey = "",
        int auxiliarMaxCompartido = 0,
        int maximoApoyoCedible = 0,
        bool puedeOmitirsePorVacacion = false)
    {
        return new Turno
        {
            NumeroTurno = numeroTurno,
            GrupoId = grupoId,
            Dia = dia,
            TipoHorario = tipoHorario,
            TipoTurnoId = tipoTurnoId,
            Inicio = new DateTime(2000, 1, 1, horaInicio, minutoInicio, 0, DateTimeKind.Unspecified),
            Fin = new DateTime(2000, 1, 1, horaFin, minutoFin, 0, DateTimeKind.Unspecified),
            PersonaTurnoTurno = [],
            MinimoPersTurno = minimoPersonas,
            CapacidadPlanificada = capacidadPlanificada,
            MaximoApoyoCedible = maximoApoyoCedible,
            IsAuxiliar = esAuxiliar,
            EsReemplazoVacacion = false,
            PuedeOmitirsePorVacacion = esAuxiliar || puedeOmitirsePorVacacion,
            AuxiliarSharedKey = esAuxiliar ? auxiliarSharedKey : string.Empty,
            AuxiliarMaxCompartido = esAuxiliar ? auxiliarMaxCompartido : 0,
            EsOpcional = esAuxiliar,
            MinutosTrabajoComputables = 8 * 60
        };
    }

    private static string[] ObtenerIdsEmpleados()
    {
        return ["Amilcar", "Andrea", "Carlos", "Cesar", "Dario", "Giselle", "Lizbeth", "Luis"];
    }

    private sealed record EscenarioPruebaFeriadosBatch
    {
        public required ProblemaRotacion Problema { get; init; }
        public required SolucionRotacionCp Solucion { get; init; }
        public required ResultadoVisibilidadFeriados VisibilidadFeriados { get; init; }
        public required string AgendaSemanal { get; init; }
    }
}
