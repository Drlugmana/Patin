using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Crea una variable de penalización que representa el rango (máximo − mínimo) de minutos
/// de ventana nocturna acumulados por los empleados elegibles a lo largo del horizonte resuelto.
/// Minimizar este rango promueve que todos los empleados que pueden cubrir turnos nocturnos
/// acumulen una carga similar de trabajo en horario nocturno.
/// El cálculo incorpora los minutos nocturnos de semanas anteriores (estado acumulado)
/// para garantizar equidad a nivel del horizonte completo.
/// Solo se activa si la política <see cref="Domain.PoliticasConfigurablesEquipo.BalancearTurnosNocturnos"/>
/// está habilitada y existen al menos dos empleados elegibles con slots nocturnos.
/// </summary>
public static class ObjetivoBalanceNocturno
{
    /// <summary>
    /// Crea la variable de penalización de rango de minutos nocturnos entre empleados.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema, las variables de decisión y el estado acumulado.</param>
    /// <returns>
    /// Variable entera de penalización (rango de minutos nocturnos), o <see langword="null"/>
    /// si la política no está habilitada, no hay slots nocturnos o hay menos de dos empleados elegibles.
    /// </returns>
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        if (!contexto.Problema.Reglas.Configurables.BalancearTurnosNocturnos)
        {
            return null;
        }

        if (contexto.Problema.Excepciones.Count > 0)
        {
            return null;
        }

        var slotsNocturnos = contexto.Problema.Slots
            .Where(slot => slot.EsTurnoNocturno)
            .ToArray();

        if (slotsNocturnos.Length == 0 || contexto.Problema.Empleados.Count == 0)
        {
            return null;
        }

        var empleadosElegibles = contexto.Problema.Empleados
            .Where(empleado => slotsNocturnos.Any(slot => PuedeCubrirGrupo(empleado, slot.GrupoId)))
            .ToArray();

        if (empleadosElegibles.Length <= 1)
        {
            return null;
        }

        var estadoAcumulado = contexto.EstadoSemanalAcumulado;
        var minutosNocturnosMaximosVentana = slotsNocturnos
            .Sum(slot => (long)Math.Max(0, slot.MinutosVentanaNocturna));

        var conteosPorEmpleado = new List<IntVar>();
        foreach (var empleado in empleadosElegibles)
        {
            var minutosAcumuladosPrevios = estadoAcumulado is not null &&
                                           estadoAcumulado.MinutosNocturnosAcumuladosPorEmpleado.TryGetValue(empleado.Id, out var previos)
                ? Math.Max(0, previos)
                : 0;

            var variables = slotsNocturnos
                .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                .ToArray();

            var coeficientes = slotsNocturnos
                .Select(slot => (long)Math.Max(0, slot.MinutosVentanaNocturna))
                .ToArray();

            var conteo = contexto.Modelo.NewIntVar(
                minutosAcumuladosPrevios,
                minutosAcumuladosPrevios + minutosNocturnosMaximosVentana,
                $"minutos_nocturnos_balance_{empleado.Numero}");
            contexto.Modelo.Add(conteo == minutosAcumuladosPrevios + LinearExpr.WeightedSum(variables, coeficientes));
            conteosPorEmpleado.Add(conteo);
        }

        var minutosPreviosPorEmpleado = empleadosElegibles
            .Select(empleado => estadoAcumulado is not null &&
                                estadoAcumulado.MinutosNocturnosAcumuladosPorEmpleado.TryGetValue(empleado.Id, out var previos)
                ? Math.Max(0, previos)
                : 0)
            .ToArray();
        var minimoPosible = (long)minutosPreviosPorEmpleado.Min();
        var maximoPosible = (long)minutosPreviosPorEmpleado.Max() + minutosNocturnosMaximosVentana;

        var maximoNocturnos = contexto.Modelo.NewIntVar(minimoPosible, maximoPosible, "maximo_minutos_nocturnos");
        var minimoNocturnos = contexto.Modelo.NewIntVar(minimoPosible, maximoPosible, "minimo_minutos_nocturnos");
        var rangoNocturnos = contexto.Modelo.NewIntVar(0, maximoPosible - minimoPosible, "rango_minutos_nocturnos");

        contexto.Modelo.AddMaxEquality(maximoNocturnos, conteosPorEmpleado);
        contexto.Modelo.AddMinEquality(minimoNocturnos, conteosPorEmpleado);
        contexto.Modelo.Add(rangoNocturnos == maximoNocturnos - minimoNocturnos);

        return rangoNocturnos;
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
