using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Crea una variable de penalización que representa el rango (máximo − mínimo) de minutos
/// trabajados en fin de semana (sábado y domingo) acumulados por los empleados elegibles.
/// Minimizar este rango promueve que la carga de trabajo en fin de semana se distribuya
/// de forma equitativa entre todos los empleados que pueden cubrir esos slots.
/// El cálculo incorpora los minutos acumulados en semanas anteriores para garantizar
/// equidad a nivel del horizonte completo.
/// Se activa siempre que existan al menos dos empleados elegibles con slots de fin de semana.
/// </summary>
public static class ObjetivoBalanceFinDeSemana
{
    /// <summary>
    /// Crea la variable de penalización de rango de minutos de fin de semana entre empleados.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema, las variables de decisión y el estado acumulado.</param>
    /// <returns>
    /// Variable entera de penalización (rango de minutos de fin de semana), o <see langword="null"/>
    /// si no hay slots de fin de semana o hay menos de dos empleados elegibles.
    /// </returns>
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        if (contexto.Problema.Excepciones.Count > 0)
        {
            return null;
        }

        var slotsFinDeSemana = contexto.Problema.Slots
            .Where(slot => slot.IndiceDia is 5 or 6)
            .ToArray();

        if (slotsFinDeSemana.Length == 0 || contexto.Problema.Empleados.Count == 0)
        {
            return null;
        }

        var empleadosElegibles = contexto.Problema.Empleados
            .Where(empleado => slotsFinDeSemana.Any(slot => PuedeCubrirGrupo(empleado, slot.GrupoId)))
            .ToArray();

        if (empleadosElegibles.Length <= 1)
        {
            return null;
        }

        var estadoAcumulado = contexto.EstadoSemanalAcumulado;
        var minutosFinSemanaMaximosVentana = slotsFinDeSemana
            .Sum(slot => (long)Math.Max(0, slot.MinutosTrabajoComputables));

        var minutosPorEmpleado = new List<IntVar>();
        foreach (var empleado in empleadosElegibles)
        {
            var minutosAcumuladosPrevios = estadoAcumulado is not null &&
                                           estadoAcumulado.MinutosFinSemanaAcumuladosPorEmpleado.TryGetValue(empleado.Id, out var previos)
                ? Math.Max(0, previos)
                : 0;

            var variables = slotsFinDeSemana
                .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                .ToArray();

            var coeficientes = slotsFinDeSemana
                .Select(slot => (long)Math.Max(0, slot.MinutosTrabajoComputables))
                .ToArray();

            var minutosEmpleado = contexto.Modelo.NewIntVar(
                minutosAcumuladosPrevios,
                minutosAcumuladosPrevios + minutosFinSemanaMaximosVentana,
                $"minutos_fin_semana_balance_{empleado.Numero}");
            contexto.Modelo.Add(minutosEmpleado == minutosAcumuladosPrevios + LinearExpr.WeightedSum(variables, coeficientes));
            minutosPorEmpleado.Add(minutosEmpleado);
        }

        var minutosPreviosPorEmpleado = empleadosElegibles
            .Select(empleado => estadoAcumulado is not null &&
                                estadoAcumulado.MinutosFinSemanaAcumuladosPorEmpleado.TryGetValue(empleado.Id, out var previos)
                ? Math.Max(0, previos)
                : 0)
            .ToArray();
        var minimoPosible = (long)minutosPreviosPorEmpleado.Min();
        var maximoPosible = (long)minutosPreviosPorEmpleado.Max() + minutosFinSemanaMaximosVentana;

        var maximo = contexto.Modelo.NewIntVar(minimoPosible, maximoPosible, "maximo_minutos_fin_semana");
        var minimo = contexto.Modelo.NewIntVar(minimoPosible, maximoPosible, "minimo_minutos_fin_semana");
        var rango = contexto.Modelo.NewIntVar(0, maximoPosible - minimoPosible, "rango_minutos_fin_semana");

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
