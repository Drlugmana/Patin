using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Restringe el número exacto de turnos que cada empleado debe trabajar en una semana,
/// expresando el objetivo en conteo de turnos en lugar de minutos cuando todos los slots
/// de la semana tienen la misma duración computable y el objetivo es divisible por ella.
/// <para>
/// Esta restricción complementa a <see cref="RestriccionHorasSemanales"/>:
/// cuando es aplicable, impone directamente la cardinalidad de turnos asignados
/// a cada empleado, que es una formulación más compacta para el motor de optimización.
/// Si hay semanas con feriado laborable y la política de sobrecupo está activa, la restricción
/// se relaja para permitir más turnos de los normales durante esa semana.
/// </para>
/// </summary>
public static class RestriccionConteoTurnosSemanalExacto
{
    /// <summary>
    /// Registra en el modelo la restricción de conteo exacto de turnos semanales por empleado.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
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

                var minutosDistintos = slotsSemana
                    .Select(slot => slot.MinutosTrabajoComputables)
                    .Distinct()
                    .ToArray();

                if (minutosDistintos.Length != 1)
                {
                    continue;
                }

                var minutosPorTurno = minutosDistintos[0];
                if (minutosPorTurno <= 0 || contexto.Problema.Reglas.Obligatorias.MinutosObjetivoSemanales % minutosPorTurno != 0)
                {
                    continue;
                }

                var turnosExactos = contexto.Problema.Reglas.Obligatorias.MinutosObjetivoSemanales / minutosPorTurno;
                var turnosObjetivoSemana = CalculadoraObjetivoSemanal.CalcularTurnosObjetivo(
                    contexto.Problema,
                    empleado,
                    indiceSemana,
                    turnosExactos);
                var variables = slotsSemana
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                if (DebePermitirSobrecupoFeriado(contexto, indiceSemana))
                {
                    var variablesSemanaCompleta = contexto.Problema.Slots
                        .Where(slot => slot.IndiceSemana == indiceSemana)
                        .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                        .ToArray();

                    contexto.Modelo.Add(LinearExpr.Sum(variables) >= turnosObjetivoSemana);
                    contexto.Modelo.Add(LinearExpr.Sum(variablesSemanaCompleta) <= turnosExactos);
                    continue;
                }

                contexto.Modelo.Add(LinearExpr.Sum(variables) == turnosObjetivoSemana);
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
