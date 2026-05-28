using TurneroTcs.Services.RotacionV2.Domain;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Calcula el numero exacto de turnos que debe trabajar un empleado en una semana dada,
/// ajustando el objetivo base segun feriados, ausencias y reglas de descanso del regreso de vacaciones.
/// </summary>
internal static class CalculadoraObjetivoSemanal
{
    public static int CalcularTurnosObjetivo(
        ProblemaRotacion problema,
        Empleado empleado,
        int indiceSemana,
        int turnosObjetivoBase)
    {
        if (turnosObjetivoBase <= 0)
        {
            return 0;
        }

        var creditoFeriado = CalculadoraCreditoFeriado.CalcularDiasCreditoSemana(problema, indiceSemana);
        var turnosObjetivoAjustado = Math.Max(0, turnosObjetivoBase - creditoFeriado);
        if (turnosObjetivoAjustado == 0)
        {
            return 0;
        }

        var fechasDisponibles = ObtenerFechasDisponiblesNoFeriado(problema, empleado, indiceSemana);
        if (DebeOmitirObjetivoPorSoloFinDeSemana(problema, empleado, indiceSemana, fechasDisponibles))
        {
            return 0;
        }

        var maximoFechasTrabajables = CalcularMaximoFechasTrabajables(
            problema,
            empleado,
            indiceSemana,
            fechasDisponibles);

        return Math.Min(turnosObjetivoAjustado, maximoFechasTrabajables);
    }

    private static HashSet<DateOnly> ObtenerFechasDisponiblesNoFeriado(ProblemaRotacion problema, Empleado empleado, int indiceSemana)
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

            if (!CalculadoraDescansoPosteriorVacacionSemanal.CumpleDescansoSemana(
                    problema,
                    empleado.Id,
                    indiceSemana,
                    fechasTrabajadas))
            {
                continue;
            }

            mejor = cantidadTrabajada;
        }

        return mejor;
    }

    private static bool DebeOmitirObjetivoPorSoloFinDeSemana(
        ProblemaRotacion problema,
        Empleado empleado,
        int indiceSemana,
        IReadOnlyCollection<DateOnly> fechasDisponibles)
    {
        if (fechasDisponibles.Count == 0)
        {
            return false;
        }

        var fechasBloqueadasSemana = CalculadoraDisponibilidadVacaciones.ObtenerFechasBloqueadas(problema, empleado.Id)
            .Where(fecha => problema.Slots.Any(slot => slot.IndiceSemana == indiceSemana && slot.Fecha == fecha))
            .ToArray();

        if (fechasBloqueadasSemana.Length == 0)
        {
            return false;
        }

        return fechasDisponibles.All(fecha =>
            problema.Slots.Any(slot =>
                slot.IndiceSemana == indiceSemana &&
                slot.Fecha == fecha &&
                slot.IndiceDia is 5 or 6));
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
}
