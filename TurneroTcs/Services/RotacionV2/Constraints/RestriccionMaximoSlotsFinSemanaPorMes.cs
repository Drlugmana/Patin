using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Limita el número de slots de fin de semana (sábado y domingo) que cada empleado
/// puede acumular en un mes calendario, usando el límite efectivo calculado por
/// <see cref="CalculadoraLimiteSlotsFinSemanaMensual"/> que eleva el tope cuando
/// el mes tiene cinco fines de semana completos.
/// La restricción solo se aplica si está configurada en las políticas del equipo.
/// </summary>
public static class RestriccionMaximoSlotsFinSemanaPorMes
{
    /// <summary>
    /// Registra en el modelo la restricción mensual de slots de fin de semana por empleado.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        if (contexto.Problema.Reglas.Configurables.MaximoSlotsFinSemanaPorMes is null or <= 0)
        {
            return;
        }

        foreach (var empleado in contexto.Problema.Empleados)
        {
            var gruposMes = contexto.Problema.Slots
                .Where(slot => slot.IndiceDia is 5 or 6)
                .GroupBy(slot => new { slot.Fecha.Year, slot.Fecha.Month });

            foreach (var grupoMes in gruposMes)
            {
                var maximoSlotsFinSemanaMes = CalculadoraLimiteSlotsFinSemanaMensual.ObtenerMaximoSlotsFinSemanaPorMes(
                    contexto.Problema,
                    grupoMes.Key.Year,
                    grupoMes.Key.Month);
                if (maximoSlotsFinSemanaMes is null or <= 0)
                {
                    continue;
                }

                var variablesMes = grupoMes
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                if (variablesMes.Length > 0)
                {
                    contexto.Modelo.Add(LinearExpr.Sum(variablesMes) <= maximoSlotsFinSemanaMes.Value);
                }
            }
        }
    }
}
