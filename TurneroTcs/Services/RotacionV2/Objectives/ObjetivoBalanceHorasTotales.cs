using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Crea una variable de penalización que representa el rango (máximo − mínimo) de minutos
/// totales trabajados acumulados por los empleados elegibles a lo largo del horizonte.
/// Minimizar este rango promueve que el total de horas de trabajo se distribuya
/// de forma equitativa entre todos los empleados que pueden cubrir slots computables.
/// El cálculo incorpora los minutos acumulados en semanas anteriores para garantizar
/// equidad a nivel del horizonte completo.
/// </summary>
public static class ObjetivoBalanceHorasTotales
{
    /// <summary>
    /// Crea la variable de penalización de rango de horas totales trabajadas entre empleados.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema, las variables de decisión y el estado acumulado.</param>
    /// <returns>
    /// Variable entera de penalización (rango de minutos totales), o <see langword="null"/>
    /// si no hay slots computables o hay menos de dos empleados elegibles.
    /// </returns>
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        var slotsComputables = contexto.Problema.Slots
            .Where(slot => slot.MinutosTrabajoComputables > 0)
            .ToArray();

        if (slotsComputables.Length == 0 || contexto.Problema.Empleados.Count == 0)
        {
            return null;
        }

        var empleadosElegibles = contexto.Problema.Empleados
            .Where(empleado => slotsComputables.Any(slot => PuedeCubrirGrupo(empleado, slot.GrupoId)))
            .ToArray();

        if (empleadosElegibles.Length <= 1)
        {
            return null;
        }

        var estadoAcumulado = contexto.EstadoSemanalAcumulado;
        var minutosMaximosVentana = slotsComputables
            .Sum(slot => (long)Math.Max(0, slot.MinutosTrabajoComputables));

        var minutosPorEmpleado = new List<IntVar>();
        foreach (var empleado in empleadosElegibles)
        {
            var minutosAcumuladosPrevios = estadoAcumulado is not null &&
                                           estadoAcumulado.MinutosTotalesAcumuladosPorEmpleado.TryGetValue(empleado.Id, out var previos)
                ? Math.Max(0, previos)
                : 0;

            var variables = slotsComputables
                .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                .ToArray();

            var coeficientes = slotsComputables
                .Select(slot => (long)Math.Max(0, slot.MinutosTrabajoComputables))
                .ToArray();

            var minutosEmpleado = contexto.Modelo.NewIntVar(
                minutosAcumuladosPrevios,
                minutosAcumuladosPrevios + minutosMaximosVentana,
                $"minutos_totales_balance_{empleado.Numero}");
            contexto.Modelo.Add(minutosEmpleado == minutosAcumuladosPrevios + LinearExpr.WeightedSum(variables, coeficientes));
            minutosPorEmpleado.Add(minutosEmpleado);
        }

        var minutosPreviosPorEmpleado = empleadosElegibles
            .Select(empleado => estadoAcumulado is not null &&
                                estadoAcumulado.MinutosTotalesAcumuladosPorEmpleado.TryGetValue(empleado.Id, out var previos)
                ? Math.Max(0, previos)
                : 0)
            .ToArray();
        var minimoPosible = (long)minutosPreviosPorEmpleado.Min();
        var maximoPosible = (long)minutosPreviosPorEmpleado.Max() + minutosMaximosVentana;

        var maximo = contexto.Modelo.NewIntVar(minimoPosible, maximoPosible, "maximo_minutos_totales");
        var minimo = contexto.Modelo.NewIntVar(minimoPosible, maximoPosible, "minimo_minutos_totales");
        var rango = contexto.Modelo.NewIntVar(0, maximoPosible - minimoPosible, "rango_minutos_totales");

        contexto.Modelo.AddMaxEquality(maximo, minutosPorEmpleado);
        contexto.Modelo.AddMinEquality(minimo, minutosPorEmpleado);
        contexto.Modelo.Add(rango == maximo - minimo);

        return rango;
    }

    private static bool PuedeCubrirGrupo(Domain.Empleado empleado, string grupoId)
    {
        if (string.IsNullOrWhiteSpace(grupoId))
        {
            return true;
        }

        return string.Equals(empleado.GrupoPrimarioId, grupoId, StringComparison.OrdinalIgnoreCase)
               || empleado.GruposSecundariosIds.Contains(grupoId);
    }
}
