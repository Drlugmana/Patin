using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Crea una variable de penalización que castiga los faltantes de cobertura en slots no auxiliares
/// cuando hay vacaciones activas en el grupo y la fecha.
/// <para>
/// Los slots de fin de semana tienen un peso de penalización mucho mayor (1 000 000) que los de lunes
/// (500 000) y el resto de días laborables (100 000), para que el motor priorice cubrir
/// los turnos de fin de semana de vacación antes que los de entre semana.
/// Solo se activa cuando existen slots con variables de faltante de cobertura registradas
/// por <see cref="RestriccionCobertura"/>.
/// </para>
/// </summary>
public static class ObjetivoCoberturaPrioritariaVacaciones
{
    /// <summary>
    /// Crea la variable de penalización de faltante de cobertura ponderada por día.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    /// <returns>
    /// Variable entera de penalización, o <see langword="null"/> si no hay faltantes de cobertura aplicables.
    /// </returns>
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        var faltantes = contexto.Problema.Slots
            .Where(slot => !slot.EsAuxiliar)
            .Select(slot => new
            {
                Slot = slot,
                Variable = contexto.Variables.ObtenerFaltanteCoberturaOpcion(slot.Id)
            })
            .Where(item => item.Variable is not null)
            .ToArray();

        if (faltantes.Length == 0)
        {
            return null;
        }

        var variables = faltantes
            .Select(item => item.Variable!)
            .ToArray();

        var coeficientes = faltantes
            .Select(item => PesoCobertura(item.Slot))
            .ToArray();

        var limiteSuperior = coeficientes
            .Zip(faltantes, (peso, item) => peso * Math.Max(1, item.Slot.EmpleadosRequeridos))
            .Sum();

        var penalizacion = contexto.Modelo.NewIntVar(0, limiteSuperior, "penalizacion_cobertura_vacaciones");
        contexto.Modelo.Add(penalizacion == LinearExpr.WeightedSum(variables, coeficientes));
        return penalizacion;
    }

    private static long PesoCobertura(Domain.SlotTurno slot)
    {
        if (slot.IndiceDia is 5 or 6)
        {
            return 1_000_000L;
        }

        if (slot.IndiceDia == 0)
        {
            return 500_000L;
        }

        return 100_000L;
    }
}
