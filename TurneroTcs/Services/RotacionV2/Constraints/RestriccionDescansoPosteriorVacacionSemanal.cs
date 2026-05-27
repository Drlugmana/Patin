using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Obliga a que, en la semana del regreso de vacaciones, el empleado conserve
/// al menos un par de dias consecutivos sin asignacion a partir de su fecha de regreso.
/// </summary>
public static class RestriccionDescansoPosteriorVacacionSemanal
{
    public static void Aplicar(ContextoModeloCp contexto)
    {
        foreach (var empleado in contexto.Problema.Empleados)
        {
            for (var indiceSemana = 0; indiceSemana < contexto.Problema.CantidadSemanas; indiceSemana++)
            {
                var descansosSemana = CalculadoraDescansoPosteriorVacacionSemanal.ObtenerDescansosSemana(
                    contexto.Problema,
                    empleado.Id,
                    indiceSemana);

                foreach (var descanso in descansosSemana)
                {
                    var paresCandidatos = CalculadoraDescansoPosteriorVacacionSemanal.ObtenerParesCandidatos(
                        contexto.Problema,
                        descanso);

                    if (paresCandidatos.Count == 0)
                    {
                        continue;
                    }

                    var indicadores = new List<BoolVar>();
                    for (var indicePar = 0; indicePar < paresCandidatos.Count; indicePar++)
                    {
                        var par = paresCandidatos[indicePar];
                        var indicador = contexto.Modelo.NewBoolVar(
                            $"descanso_post_vac_{empleado.Numero}_{indiceSemana}_{indicePar}");
                        var variablesPar = contexto.Problema.Slots
                            .Where(slot => slot.Fecha == par.Inicio || slot.Fecha == par.Fin)
                            .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                            .ToArray();

                        if (variablesPar.Length == 0)
                        {
                            contexto.Modelo.Add(indicador == 1);
                            indicadores.Add(indicador);
                            continue;
                        }

                        contexto.Modelo.Add(LinearExpr.Sum(variablesPar) == 0).OnlyEnforceIf(indicador);
                        contexto.Modelo.Add(LinearExpr.Sum(variablesPar) >= 1).OnlyEnforceIf(indicador.Not());
                        indicadores.Add(indicador);
                    }

                    contexto.Modelo.Add(LinearExpr.Sum(indicadores) >= 1);
                }
            }
        }
    }
}
