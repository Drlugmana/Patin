using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Crea una variable de penalización que mide el déficit de minutos trabajados
/// respecto al objetivo semanal, sumado sobre todos los empleados y semanas.
/// A mayor déficit, mayor penalización, incentivando al motor a acercar las horas
/// trabajadas de cada empleado al objetivo semanal configurado.
/// Solo se activa si la política <see cref="Domain.PoliticasConfigurablesEquipo.BalancearHorasSemanales"/>
/// está habilitada.
/// </summary>
public static class ObjetivoCumplimientoHorasSemanales
{
    /// <summary>
    /// Crea la variable de penalización de déficit de horas semanales y la registra en el modelo.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    /// <returns>
    /// Variable entera de penalización, o <see langword="null"/> si la política no está habilitada
    /// o no hay empleados/slots aplicables.
    /// </returns>
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        if (!contexto.Problema.Reglas.Configurables.BalancearHorasSemanales)
        {
            return null;
        }

        var minutosObjetivo = contexto.Problema.Reglas.Obligatorias.MinutosObjetivoSemanales;
        var deficits = new List<IntVar>();

        foreach (var empleado in contexto.Problema.Empleados)
        {
            for (var indiceSemana = 0; indiceSemana < contexto.Problema.CantidadSemanas; indiceSemana++)
            {
                var slotsSemana = contexto.Problema.Slots
                    .Where(slot => slot.IndiceSemana == indiceSemana)
                    .ToArray();

                var variables = slotsSemana
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                var coeficientes = slotsSemana
                    .Select(slot => (long)slot.MinutosTrabajoComputables)
                    .ToArray();

                var minutosTrabajados = contexto.Modelo.NewIntVar(0, minutosObjetivo, $"minutos_trabajados_{empleado.Numero}_{indiceSemana}");
                contexto.Modelo.Add(minutosTrabajados == LinearExpr.WeightedSum(variables, coeficientes));

                var deficit = contexto.Modelo.NewIntVar(0, minutosObjetivo, $"deficit_horas_{empleado.Numero}_{indiceSemana}");
                contexto.Modelo.Add(deficit == minutosObjetivo - minutosTrabajados);
                deficits.Add(deficit);
            }
        }

        if (deficits.Count == 0)
        {
            return null;
        }

        var penalizacionTotal = contexto.Modelo.NewIntVar(0, minutosObjetivo * deficits.Count, "penalizacion_total_deficit_horas");
        contexto.Modelo.Add(penalizacionTotal == LinearExpr.Sum(deficits));
        return penalizacionTotal;
    }
}
