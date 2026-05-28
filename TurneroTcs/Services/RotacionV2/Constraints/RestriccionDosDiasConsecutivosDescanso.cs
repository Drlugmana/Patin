using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Garantiza que cada empleado tenga al menos un par de días consecutivos de descanso por semana.
/// <para>
/// Para cada semana del horizonte, se crea una variable indicadora por cada par de días consecutivos
/// (lunes-martes, martes-miércoles, …, sábado-domingo) que vale 1 si ambos días son de descanso.
/// Luego se exige que al menos uno de los seis pares sea un descanso doble.
/// Esta restricción solo se aplica cuando el mínimo configurado es mayor a 1 día.
/// </para>
/// </summary>
public static class RestriccionDosDiasConsecutivosDescanso
{
    /// <summary>
    /// Registra en el modelo la restricción de descanso consecutivo mínimo por semana para cada empleado.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        var minimoDiasConsecutivos = contexto.Problema.Reglas.Obligatorias.MinimoDiasDescansoConsecutivosPorSemana;
        if (minimoDiasConsecutivos <= 1)
        {
            return;
        }

        foreach (var empleado in contexto.Problema.Empleados)
        {
            for (var indiceSemana = 0; indiceSemana < contexto.Problema.CantidadSemanas; indiceSemana++)
            {
                var slotsSemana = contexto.Problema.Slots
                    .Where(slot => slot.IndiceSemana == indiceSemana)
                    .ToArray();

                var trabajoPorDia = Enumerable.Range(0, 7)
                    .Select(indiceDia => slotsSemana
                        .Where(slot => slot.IndiceDia == indiceDia)
                        .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                        .ToArray())
                    .ToArray();

                var paresDescanso = new List<BoolVar>();
                for (var indiceDia = 0; indiceDia < 6; indiceDia++)
                {
                    var parDescanso = contexto.Modelo.NewBoolVar($"descanso_consecutivo_{empleado.Numero}_{indiceSemana}_{indiceDia}");
                    var variablesDiaActual = trabajoPorDia[indiceDia];
                    var variablesDiaSiguiente = trabajoPorDia[indiceDia + 1];
                    var variablesPar = variablesDiaActual.Concat(variablesDiaSiguiente).ToArray();

                    if (variablesPar.Length == 0)
                    {
                        contexto.Modelo.Add(parDescanso == 1);
                        paresDescanso.Add(parDescanso);
                        continue;
                    }

                    contexto.Modelo.Add(LinearExpr.Sum(variablesPar) == 0).OnlyEnforceIf(parDescanso);
                    contexto.Modelo.Add(LinearExpr.Sum(variablesPar) >= 1).OnlyEnforceIf(parDescanso.Not());
                    paresDescanso.Add(parDescanso);
                }

                contexto.Modelo.Add(LinearExpr.Sum(paresDescanso) >= 1);
            }
        }
    }
}
