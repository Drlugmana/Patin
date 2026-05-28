using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Domain;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Proyecta el estado acumulado de semanas anteriores sobre el modelo de la semana actual,
/// cerrando el hueco de continuidad que existe entre semanas resueltas de forma independiente.
/// <para>
/// Aplica cinco tipos de restricciones cross-semana:
/// <list type="number">
///   <item>Bloqueo de fin de semana para empleados que ya alcanzaron el máximo de fines de semana consecutivos.</item>
///   <item>Bloqueo de turno nocturno el lunes para empleados que trabajaron nocturno el domingo anterior.</item>
///   <item>Respeto del descanso mínimo desde el último turno de la semana anterior.</item>
///   <item>Aplicación del límite mensual acumulado de slots de fin de semana.</item>
///   <item>Aplicación del límite mensual acumulado de turnos nocturnos.</item>
/// </list>
/// </para>
/// </summary>
public static class RestriccionEstadoSemanalAcumulado
{
    /// <summary>
    /// Aplica al modelo todas las restricciones derivadas del estado acumulado de semanas anteriores.
    /// </summary>
    /// <param name="contexto">Contexto del modelo de la semana actual.</param>
    /// <param name="estado">Estado acumulado desde el inicio del horizonte hasta la semana anterior.</param>
    public static void Aplicar(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        AplicarBloqueoFinSemanaConsecutivo(contexto, estado);
        // AplicarBloqueoNocturnoConsecutivoEntreSemanas: COMENTADO — ver método comentado más abajo.
        // Reemplazado por AplicarControlNocturnosConsecutivosEntreSemanas:
        //   los turnos de mañana/tarde/media-tarde en Lun/Mar son libres (solo descanso mínimo ≥ 8 h);
        //   los turnos nocturnos respetan el máximo de consecutivos configurado.
        AplicarNocturnasConsecutivasDesdeHistoria(contexto, estado);
        AplicarControlNocturnosConsecutivosEntreSemanas(contexto, estado);
        AplicarDescansoMinimoDesdeSemanaAnterior(contexto, estado);
        AplicarLimiteSlotsFinSemanaAcumulados(contexto, estado);
        AplicarLimiteTurnosNocturnosAcumulados(contexto, estado);
    }

    private static void AplicarBloqueoFinSemanaConsecutivo(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        if (!contexto.Problema.Reglas.Configurables.EvitarFinesSemanaConsecutivos)
        {
            return;
        }

        var maximoConsecutivos = Math.Max(1, contexto.Problema.Reglas.Configurables.MaximoFinesSemanaConsecutivos);
        var slotsFinSemana = contexto.Problema.Slots
            .Where(slot => slot.IndiceSemana == 0 && slot.IndiceDia is 5 or 6)
            .ToArray();

        foreach (var empleado in contexto.Problema.Empleados)
        {
            estado.RachaFinesSemanaConsecutivosPorEmpleado.TryGetValue(empleado.Id, out var rachaActual);
            if (rachaActual < maximoConsecutivos)
            {
                AplicarPrefijoRachaFinSemana(contexto, empleado.Id, empleado.Numero, rachaActual, maximoConsecutivos);
                continue;
            }

            foreach (var slot in slotsFinSemana)
            {
                contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id) == 0);
            }
        }
    }

    /*
     * AplicarBloqueoNocturnoConsecutivoEntreSemanas — COMENTADO.
     * Bloqueaba duramente los turnos nocturnos del lunes para empleados que trabajaron noche el domingo.
     * Reemplazado por AplicarControlNocturnosConsecutivosEntreSemanas, que:
     *   - No restringe mañana/tarde/media-tarde del lunes y martes.
     *   - Solo limita los NOCTURNOS según el máximo de consecutivos configurado.
     *
    private static void AplicarBloqueoNocturnoConsecutivoEntreSemanas(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        if (estado.EmpleadosConNocturnoUltimoDiaAnterior.Count == 0)
        {
            return;
        }

        if (contexto.Problema.Slots.Any(s => s.EsAuxiliar))
        {
            return;
        }

        var slotsNocturnosPrimerDia = contexto.Problema.Slots
            .Where(slot => slot.IndiceSemana == 0 && slot.IndiceDia == 0 && slot.EsTurnoNocturno)
            .ToArray();

        foreach (var empleadoId in estado.EmpleadosConNocturnoUltimoDiaAnterior)
        {
            foreach (var slot in slotsNocturnosPrimerDia)
            {
                contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleadoId, slot.Id) == 0);
            }
        }
    }
    */

    /// <summary>
    /// Controla las noches consecutivas al cruzar de una semana a la siguiente.
    /// <para>
    /// Los turnos de <b>mañana, tarde y media-tarde</b> del lunes y martes no tienen restricción adicional;
    /// solo respetan el descanso mínimo (≥ 8 h) gestionado por <see cref="AplicarDescansoMinimoDesdeSemanaAnterior"/>.
    /// </para>
    /// <para>
    /// Los <b>turnos nocturnos</b> del lunes y martes se permiten siempre que el total acumulado
    /// (racha histórica + noches nuevas) no supere
    /// <see cref="Domain.PoliticasConfigurablesEquipo.MaximoTurnosNocturnosPorSemana"/>.
    /// </para>
    /// Ejemplo: Sáb noche + Dom noche (racha=2), max=3 → Lun noche PERMITIDO (2+1=3 ≤ 3).<br/>
    /// Ejemplo: Sáb noche + Dom noche (racha=2), max=2 → Lun noche BLOQUEADO  (2+1=3 > 2).
    /// </summary>
    private static void AplicarControlNocturnosConsecutivosEntreSemanas(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        var maxConsecutivas = contexto.Problema.Reglas.Configurables.MaximoTurnosNocturnosPorSemana;
        if (maxConsecutivas is null or <= 0) return;
        if (estado.RachaNocturnaBordeHistoriaPorEmpleado.Count == 0) return;

        // Slots nocturnos de la primera semana agrupados por fecha, en orden cronológico.
        var slotsNocturnosPorFecha = contexto.Problema.Slots
            .Where(slot => slot.IndiceSemana == 0 && slot.EsTurnoNocturno)
            .GroupBy(slot => slot.Fecha)
            .OrderBy(g => g.Key)
            .ToList();

        if (slotsNocturnosPorFecha.Count == 0) return;

        var fechasOrdenadas = slotsNocturnosPorFecha.Select(g => g.Key).ToList();
        var slotsPorIndice = slotsNocturnosPorFecha.Select(g => g.ToList()).ToList();

        // Solo aplica si los nocturnos comienzan el mismo día que el horizonte;
        // si hay un hueco al inicio, la racha histórica ya está naturalmente rota.
        var fechaInicioHorizonte = contexto.Problema.Slots
            .Where(slot => slot.IndiceSemana == 0)
            .Min(slot => slot.Fecha);

        if (fechasOrdenadas[0] != fechaInicioHorizonte) return;

        foreach (var empleado in contexto.Problema.Empleados)
        {
            if (!estado.RachaNocturnaBordeHistoriaPorEmpleado.TryGetValue(empleado.Id, out var racha) || racha <= 0)
                continue;

            // Ventana deslizante: para cada solapamiento i (1 ≤ i ≤ min(racha, max)):
            //   i días del historial son noches consecutivas →
            //   los primeros (max-i) días de la nueva semana pueden tener como máximo (max-i) noches.
            for (int i = 1; i <= Math.Min(racha, maxConsecutivas.Value); i++)
            {
                var maxDayIndex = maxConsecutivas.Value - i;
                var limite = maxConsecutivas.Value - i;
                var vars = new List<BoolVar>();
                var sonConsecutivos = true;

                for (int j = 0; j <= maxDayIndex; j++)
                {
                    if (j >= fechasOrdenadas.Count) break;
                    if (j > 0 && fechasOrdenadas[j] != fechasOrdenadas[j - 1].AddDays(1))
                    {
                        sonConsecutivos = false;
                        break;
                    }
                    foreach (var slot in slotsPorIndice[j])
                        vars.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id));
                }

                if (!sonConsecutivos || vars.Count == 0) continue;
                contexto.Modelo.Add(LinearExpr.Sum(vars) <= limite);
            }
        }
    }

    private static void AplicarDescansoMinimoDesdeSemanaAnterior(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        var descansoMinimo = TimeSpan.FromMinutes(contexto.Problema.Reglas.Obligatorias.MinutosMinimosDescansoEntreTurnos);
        if (descansoMinimo <= TimeSpan.Zero)
        {
            return;
        }

        foreach (var (empleadoId, ultimoFinTurno) in estado.UltimoFinTurnoPorEmpleado)
        {
            foreach (var slot in contexto.Problema.Slots)
            {
                if (slot.InicioLocal - ultimoFinTurno < descansoMinimo)
                {
                    contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleadoId, slot.Id) == 0);
                }
            }
        }
    }

    private static void AplicarLimiteTurnosNocturnosAcumulados(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        var maximoTurnosNocturnosPorMes = contexto.Problema.Reglas.Configurables.MaximoTurnosNocturnosPorMes;
        if (maximoTurnosNocturnosPorMes is null || maximoTurnosNocturnosPorMes <= 0)
        {
            return;
        }

        foreach (var empleado in contexto.Problema.Empleados)
        {
            var slotsNocturnosPorMes = contexto.Problema.Slots
                .Where(slot => slot.EsTurnoNocturno)
                .GroupBy(slot => new { slot.Fecha.Year, slot.Fecha.Month });

            foreach (var grupoMes in slotsNocturnosPorMes)
            {
                var clave = (empleado.Id, grupoMes.Key.Year, grupoMes.Key.Month);
                estado.TurnosNocturnosPorEmpleadoMes.TryGetValue(clave, out var usados);

                if (usados >= maximoTurnosNocturnosPorMes)
                {
                    foreach (var slot in grupoMes)
                    {
                        contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id) == 0);
                    }

                    continue;
                }

                var restantes = maximoTurnosNocturnosPorMes.Value - usados;
                var variablesMes = grupoMes
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                if (variablesMes.Length > 0)
                {
                    contexto.Modelo.Add(LinearExpr.Sum(variablesMes) <= restantes);
                }
            }
        }
    }

    private static void AplicarLimiteSlotsFinSemanaAcumulados(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        if (contexto.Problema.Reglas.Configurables.MaximoSlotsFinSemanaPorMes is null or <= 0)
        {
            return;
        }

        foreach (var empleado in contexto.Problema.Empleados)
        {
            var slotsFinSemanaPorMes = contexto.Problema.Slots
                .Where(slot => slot.IndiceDia is 5 or 6)
                .GroupBy(slot => new { slot.Fecha.Year, slot.Fecha.Month });

            foreach (var grupoMes in slotsFinSemanaPorMes)
            {
                var maximoSlotsFinSemanaMes = CalculadoraLimiteSlotsFinSemanaMensual.ObtenerMaximoSlotsFinSemanaPorMes(
                    contexto.Problema,
                    grupoMes.Key.Year,
                    grupoMes.Key.Month);
                if (maximoSlotsFinSemanaMes is null or <= 0)
                {
                    continue;
                }

                var clave = (empleado.Id, grupoMes.Key.Year, grupoMes.Key.Month);
                estado.SlotsFinSemanaPorEmpleadoMes.TryGetValue(clave, out var usados);

                if (usados >= maximoSlotsFinSemanaMes)
                {
                    foreach (var slot in grupoMes)
                    {
                        contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id) == 0);
                    }

                    continue;
                }

                var restantes = maximoSlotsFinSemanaMes.Value - usados;
                var variablesMes = grupoMes
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                if (variablesMes.Length > 0)
                {
                    contexto.Modelo.Add(LinearExpr.Sum(variablesMes) <= restantes);
                }
            }
        }
    }

    private static void AplicarPrefijoRachaFinSemana(
        ContextoModeloCp contexto,
        string empleadoId,
        int numeroEmpleado,
        int rachaActual,
        int maximoConsecutivos)
    {
        if (rachaActual <= 0)
        {
            return;
        }

        var semanasAdicionalesPermitidas = maximoConsecutivos - rachaActual;
        var semanasPrefijo = Math.Min(contexto.Problema.CantidadSemanas, semanasAdicionalesPermitidas + 1);
        if (semanasPrefijo <= 1)
        {
            return;
        }

        var trabajoFinSemanaPrefijo = Enumerable.Range(0, semanasPrefijo)
            .Select(indiceSemana => CrearTrabajoFinSemana(contexto, empleadoId, numeroEmpleado, indiceSemana))
            .ToArray();

        contexto.Modelo.Add(LinearExpr.Sum(trabajoFinSemanaPrefijo) <= semanasAdicionalesPermitidas);
    }

    /// <summary>
    /// Cuando <see cref="Domain.PoliticasConfigurablesEquipo.NocturnosConsecutivos"/> está activo, aplica restricciones
    /// de ventana deslizante que cruzan el límite entre el historial previo y la primera semana generada.
    /// Evita que una racha de noches consecutivas acumulada en el historial continúe excediendo
    /// el máximo permitido en los primeros días del horizonte nuevo.
    /// </summary>
    private static void AplicarNocturnasConsecutivasDesdeHistoria(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        if (!contexto.Problema.Reglas.Configurables.NocturnosConsecutivos)
        {
            return;
        }

        var maxConsecutivas = contexto.Problema.Reglas.Configurables.MaximoTurnosNocturnosPorSemana;
        if (maxConsecutivas is null or <= 0)
        {
            return;
        }

        if (estado.RachaNocturnaBordeHistoriaPorEmpleado.Count == 0)
        {
            return;
        }

        var slotsNocturnosPorFecha = contexto.Problema.Slots
            .Where(slot => slot.IndiceSemana == 0 && slot.EsTurnoNocturno)
            .GroupBy(slot => slot.Fecha)
            .OrderBy(g => g.Key)
            .ToList();

        if (slotsNocturnosPorFecha.Count == 0)
        {
            return;
        }

        var fechasOrdenadas = slotsNocturnosPorFecha.Select(g => g.Key).ToList();
        var slotsPorIndice = slotsNocturnosPorFecha.Select(g => g.ToList()).ToList();

        // La restricción solo aplica si los slots nocturnos comienzan en el primer día del horizonte;
        // si hay un hueco, la racha del historial ya está naturalmente rota.
        var fechaInicioHorizonte = contexto.Problema.Slots
            .Where(slot => slot.IndiceSemana == 0)
            .Min(slot => slot.Fecha);

        if (fechasOrdenadas[0] != fechaInicioHorizonte)
        {
            return;
        }

        foreach (var empleado in contexto.Problema.Empleados)
        {
            if (!estado.RachaNocturnaBordeHistoriaPorEmpleado.TryGetValue(empleado.Id, out var racha) || racha <= 0)
            {
                continue;
            }

            // Para cada ventana parcial que solapa con el historial:
            // la ventana i contiene i días del historial (todos nocturnos) + (maxConsecutivas-i+1) días de la nueva semana.
            // Restricción: sum(noches nueva semana en días 0..maxConsecutivas-i) <= maxConsecutivas - i
            for (int i = 1; i <= Math.Min(racha, maxConsecutivas.Value); i++)
            {
                var maxDayIndex = maxConsecutivas.Value - i;
                var limite = maxConsecutivas.Value - i;
                var vars = new List<BoolVar>();
                var sonConsecutivos = true;

                for (int j = 0; j <= maxDayIndex; j++)
                {
                    if (j >= fechasOrdenadas.Count)
                    {
                        break;
                    }

                    if (j > 0 && fechasOrdenadas[j] != fechasOrdenadas[j - 1].AddDays(1))
                    {
                        sonConsecutivos = false;
                        break;
                    }

                    foreach (var slot in slotsPorIndice[j])
                    {
                        vars.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id));
                    }
                }

                if (!sonConsecutivos || vars.Count == 0)
                {
                    continue;
                }

                contexto.Modelo.Add(LinearExpr.Sum(vars) <= limite);
            }
        }
    }

    private static BoolVar CrearTrabajoFinSemana(
        ContextoModeloCp contexto,
        string empleadoId,
        int numeroEmpleado,
        int indiceSemana)
    {
        var slotsFinSemana = contexto.Problema.Slots
            .Where(slot => slot.IndiceSemana == indiceSemana && slot.IndiceDia is 5 or 6)
            .Select(slot => contexto.ObtenerVariableAsignacion(empleadoId, slot.Id))
            .ToArray();

        var trabajoFinSemana = contexto.Modelo.NewBoolVar($"trabajo_fin_semana_prefijo_{numeroEmpleado}_{indiceSemana}");
        if (slotsFinSemana.Length == 0)
        {
            contexto.Modelo.Add(trabajoFinSemana == 0);
            return trabajoFinSemana;
        }

        contexto.Modelo.Add(LinearExpr.Sum(slotsFinSemana) >= 1).OnlyEnforceIf(trabajoFinSemana);
        contexto.Modelo.Add(LinearExpr.Sum(slotsFinSemana) == 0).OnlyEnforceIf(trabajoFinSemana.Not());
        return trabajoFinSemana;
    }
}
