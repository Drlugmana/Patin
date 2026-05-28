using TurneroTcs.Services.RotacionV2.Domain;

namespace TurneroTcs.Services.RotacionV2.Constraints;

internal static class CalculadoraDescansoPosteriorVacacionSemanal
{
    public static IReadOnlyList<DescansoPosteriorVacacion> ObtenerDescansosSemana(
        ProblemaRotacion problema,
        string empleadoId,
        int indiceSemana)
    {
        var fechaInicioSemana = problema.FechaInicio.AddDays(indiceSemana * 7);
        var fechaFinSemana = fechaInicioSemana.AddDays(6);

        return problema.DescansosPosterioresVacacion
            .Where(descanso =>
                string.Equals(descanso.EmpleadoId, empleadoId, StringComparison.OrdinalIgnoreCase) &&
                descanso.FechaRegreso >= fechaInicioSemana &&
                descanso.FechaRegreso <= fechaFinSemana)
            .OrderBy(descanso => descanso.FechaRegreso)
            .ToArray();
    }

    public static IReadOnlyList<(DateOnly Inicio, DateOnly Fin)> ObtenerParesCandidatos(
        ProblemaRotacion problema,
        DescansoPosteriorVacacion descanso)
    {
        var indiceSemana = ObtenerIndiceSemana(problema, descanso.FechaRegreso);
        if (indiceSemana < 0 || indiceSemana >= problema.CantidadSemanas)
        {
            return [];
        }

        var fechaInicioSemana = problema.FechaInicio.AddDays(indiceSemana * 7);
        var fechaFinSemana = fechaInicioSemana.AddDays(6);
        var pares = new List<(DateOnly Inicio, DateOnly Fin)>();

        for (var fecha = descanso.FechaRegreso; fecha < fechaFinSemana; fecha = fecha.AddDays(1))
        {
            pares.Add((fecha, fecha.AddDays(1)));
        }

        return pares;
    }

    public static bool CumpleDescansoSemana(
        ProblemaRotacion problema,
        string empleadoId,
        int indiceSemana,
        IReadOnlySet<DateOnly> fechasTrabajadas)
    {
        foreach (var descanso in ObtenerDescansosSemana(problema, empleadoId, indiceSemana))
        {
            var pares = ObtenerParesCandidatos(problema, descanso);
            if (pares.Count == 0)
            {
                continue;
            }

            var cumpleAlguno = pares.Any(par =>
                !fechasTrabajadas.Contains(par.Inicio) &&
                !fechasTrabajadas.Contains(par.Fin));

            if (!cumpleAlguno)
            {
                return false;
            }
        }

        return true;
    }

    private static int ObtenerIndiceSemana(ProblemaRotacion problema, DateOnly fecha)
    {
        var diasDesdeInicio = fecha.DayNumber - problema.FechaInicio.DayNumber;
        if (diasDesdeInicio < 0)
        {
            return -1;
        }

        return diasDesdeInicio / 7;
    }
}
