using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Impone el objetivo de horas semanales de cada empleado como restricción dura del modelo.
/// <para>
/// Los feriados laborables generan un crédito que reduce el número de turnos exigidos.
/// Cuando todos los slots de la semana tienen la misma duración computable y el objetivo
/// es divisible por esa duración, la restricción se expresa en número de turnos (más eficiente);
/// en caso contrario, se expresa en minutos de trabajo ponderados.
/// Si el equipo tiene habilitada la política de sobrecupo en feriado, la restricción se flexibiliza
/// permitiendo que el total con feriados no supere el objetivo base, pero sin exigir un mínimo
/// exacto en los días de feriado.
/// </para>
/// </summary>
public static class RestriccionHorasSemanales
{
    /// <summary>
    /// Registra en el modelo la restricción de horas semanales para cada empleado y semana.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        var minutosObjetivo = contexto.Problema.Reglas.Obligatorias.MinutosObjetivoSemanales;
        foreach (var empleado in contexto.Problema.Empleados)
        {
            for (var indiceSemana = 0; indiceSemana < contexto.Problema.CantidadSemanas; indiceSemana++)
            {
                var slotsSemana = contexto.Problema.Slots
                    .Where(slot =>
                        slot.IndiceSemana == indiceSemana &&
                        !CalculadoraCreditoFeriado.EsFeriadoLaborable(contexto.Problema, slot.Fecha))
                    .ToArray();

                if (slotsSemana.Length == 0)
                {
                    continue;
                }

                var variables = slotsSemana
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                var coeficientes = slotsSemana
                    .Select(slot => (long)slot.MinutosTrabajoComputables)
                    .ToArray();

                var minutosDistintos = slotsSemana
                    .Select(slot => slot.MinutosTrabajoComputables)
                    .Distinct()
                    .ToArray();

                if (minutosDistintos.Length == 1)
                {
                    var minutosPorTurno = minutosDistintos[0];
                    if (minutosPorTurno > 0 && minutosObjetivo % minutosPorTurno == 0)
                    {
                        var turnosObjetivoBase = minutosObjetivo / minutosPorTurno;
                        var turnosObjetivoSemana = CalculadoraObjetivoSemanal.CalcularTurnosObjetivo(
                            contexto.Problema,
                            empleado,
                            indiceSemana,
                            turnosObjetivoBase);

                        if (DebePermitirSobrecupoFeriado(contexto, indiceSemana))
                        {
                            var slotsSemanaCompleta = contexto.Problema.Slots
                                .Where(slot => slot.IndiceSemana == indiceSemana)
                                .ToArray();
                            var variablesSemanaCompleta = slotsSemanaCompleta
                                .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                                .ToArray();
                            var coeficientesSemanaCompleta = slotsSemanaCompleta
                                .Select(slot => (long)slot.MinutosTrabajoComputables)
                                .ToArray();

                            contexto.Modelo.Add(LinearExpr.WeightedSum(variables, coeficientes) >= turnosObjetivoSemana * minutosPorTurno);
                            contexto.Modelo.Add(LinearExpr.WeightedSum(variablesSemanaCompleta, coeficientesSemanaCompleta) <= turnosObjetivoBase * minutosPorTurno);
                            continue;
                        }

                        contexto.Modelo.Add(LinearExpr.WeightedSum(variables, coeficientes) == turnosObjetivoSemana * minutosPorTurno);
                        continue;
                    }
                }

                if (CalculadoraCreditoFeriado.CalcularDiasCreditoSemana(contexto.Problema, indiceSemana) > 0)
                {
                    continue;
                }

                contexto.Modelo.Add(LinearExpr.WeightedSum(variables, coeficientes) == minutosObjetivo);
            }
        }
    }

    private static bool DebePermitirSobrecupoFeriado(ContextoModeloCp contexto, int indiceSemana)
    {
        return contexto.Problema.Reglas.Configurables.PermitirSobrecupoSemanalEnFeriado &&
               contexto.Problema.Feriados.Any(fecha =>
                   fecha >= contexto.Problema.FechaInicio.AddDays(indiceSemana * 7) &&
                   fecha <= contexto.Problema.FechaInicio.AddDays((indiceSemana * 7) + 6) &&
                   CalculadoraCreditoFeriado.EsFeriadoLaborable(contexto.Problema, fecha));
    }
}
