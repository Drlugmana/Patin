using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Crea una variable de penalización que representa el rango (máximo − mínimo) de minutos
/// trabajados en días feriados acumulados por los empleados elegibles.
/// Minimizar este rango promueve que la carga de trabajo en feriados se distribuya
/// equitativamente entre todos los empleados que pueden cubrir esos slots.
/// El cálculo incorpora los minutos de feriado de semanas anteriores para garantizar
/// equidad a nivel del horizonte completo.
/// Solo se activa si la política <see cref="Domain.PoliticasConfigurablesEquipo.BalancearCargaFeriados"/>
/// está habilitada y existen al menos dos empleados elegibles con slots en días feriados.
/// </summary>
public static class ObjetivoBalanceFeriados
{
    /// <summary>
    /// Crea la variable de penalización de rango de minutos en feriados entre empleados.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema, las variables de decisión y el estado acumulado.</param>
    /// <returns>
    /// Variable entera de penalización (rango de minutos en feriados), o <see langword="null"/>
    /// si la política no está habilitada, no hay slots en feriados o hay menos de dos empleados elegibles.
    /// </returns>
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        if (!contexto.Problema.Reglas.Configurables.BalancearCargaFeriados)
        {
            return null;
        }

        if (contexto.Problema.Excepciones.Count > 0)
        {
            return null;
        }

        var slotsFeriado = contexto.Problema.Slots
            .Where(slot => contexto.Problema.Feriados.Contains(slot.Fecha))
            .ToArray();

        if (slotsFeriado.Length == 0 || contexto.Problema.Empleados.Count == 0)
        {
            return null;
        }

        var empleadosElegibles = contexto.Problema.Empleados
            .Where(empleado => slotsFeriado.Any(slot => PuedeCubrirGrupo(empleado, slot.GrupoId)))
            .ToArray();

        if (empleadosElegibles.Length <= 1)
        {
            return null;
        }

        var estadoAcumulado = contexto.EstadoSemanalAcumulado;
        var minutosFeriadoMaximosVentana = slotsFeriado
            .Sum(slot => (long)Math.Max(0, slot.MinutosTrabajoComputables));

        var conteosPorEmpleado = new List<IntVar>();
        foreach (var empleado in empleadosElegibles)
        {
            var minutosAcumuladosPrevios = estadoAcumulado is not null &&
                                           estadoAcumulado.MinutosFeriadoAcumuladosPorEmpleado.TryGetValue(empleado.Id, out var previos)
                ? Math.Max(0, previos)
                : 0;

            var variables = slotsFeriado
                .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                .ToArray();

            var coeficientes = slotsFeriado
                .Select(slot => (long)Math.Max(0, slot.MinutosTrabajoComputables))
                .ToArray();

            var conteo = contexto.Modelo.NewIntVar(
                minutosAcumuladosPrevios,
                minutosAcumuladosPrevios + minutosFeriadoMaximosVentana,
                $"minutos_feriado_balance_{empleado.Numero}");
            contexto.Modelo.Add(conteo == minutosAcumuladosPrevios + LinearExpr.WeightedSum(variables, coeficientes));
            conteosPorEmpleado.Add(conteo);
        }

        var minutosPreviosPorEmpleado = empleadosElegibles
            .Select(empleado => estadoAcumulado is not null &&
                                estadoAcumulado.MinutosFeriadoAcumuladosPorEmpleado.TryGetValue(empleado.Id, out var previos)
                ? Math.Max(0, previos)
                : 0)
            .ToArray();
        var minimoPosible = (long)minutosPreviosPorEmpleado.Min();
        var maximoPosible = (long)minutosPreviosPorEmpleado.Max() + minutosFeriadoMaximosVentana;

        var maximoFeriados = contexto.Modelo.NewIntVar(minimoPosible, maximoPosible, "maximo_minutos_feriado");
        var minimoFeriados = contexto.Modelo.NewIntVar(minimoPosible, maximoPosible, "minimo_minutos_feriado");
        var rangoFeriados = contexto.Modelo.NewIntVar(0, maximoPosible - minimoPosible, "rango_minutos_feriado");

        contexto.Modelo.AddMaxEquality(maximoFeriados, conteosPorEmpleado);
        contexto.Modelo.AddMinEquality(minimoFeriados, conteosPorEmpleado);
        contexto.Modelo.Add(rangoFeriados == maximoFeriados - minimoFeriados);

        return rangoFeriados;
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
