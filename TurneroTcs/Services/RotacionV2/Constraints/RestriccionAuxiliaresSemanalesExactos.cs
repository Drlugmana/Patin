using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Ajusta el número total de asignaciones auxiliares de la semana para que sea coherente
/// con el objetivo de turnos del equipo completo, descontando los turnos no auxiliares
/// y compensando el apoyo cedido o los faltantes de cobertura cuando existan.
/// <para>
/// Esta restricción no se aplica en semanas que tienen feriados laborables o vacaciones activas,
/// ya que en esos casos el objetivo semanal es variable y la cardinalidad de auxiliares
/// no puede fijarse con precisión.
/// </para>
/// </summary>
public static class RestriccionAuxiliaresSemanalesExactos
{
    /// <summary>
    /// Registra en el modelo la restricción de conteo exacto de auxiliares semanales.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        for (var indiceSemana = 0; indiceSemana < contexto.Problema.CantidadSemanas; indiceSemana++)
        {
            if (TieneFeriadoLaborableEnSemana(contexto, indiceSemana))
            {
                continue;
            }

            if (HayVacacionesEnSemana(contexto, indiceSemana))
            {
                continue;
            }

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

            var turnosObjetivoBase = contexto.Problema.Reglas.Obligatorias.MinutosObjetivoSemanales / minutosPorTurno;
            var turnosTotalesEsperados = contexto.Problema.Empleados
                .Sum(empleado => CalculadoraObjetivoSemanal.CalcularTurnosObjetivo(
                    contexto.Problema,
                    empleado,
                    indiceSemana,
                    turnosObjetivoBase));

            var turnosNoAuxiliaresBase = slotsSemana
                .Where(slot => !slot.EsAuxiliar)
                .Sum(slot => slot.EmpleadosRequeridos);

            var variablesAuxiliares = slotsSemana
                .Where(slot => slot.EsAuxiliar)
                .SelectMany(slot => contexto.Problema.Empleados.Select(empleado => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id)))
                .ToArray();

            if (variablesAuxiliares.Length == 0)
            {
                continue;
            }

            var variablesApoyoCedido = slotsSemana
                .Where(slot => !slot.EsAuxiliar)
                .Select(slot => contexto.Variables.ObtenerApoyoCedidoOpcion(slot.Id))
                .Where(variable => variable is not null)
                .Cast<IntVar>()
                .ToArray();

            var variablesFaltanteCobertura = slotsSemana
                .Where(slot => !slot.EsAuxiliar)
                .Select(slot => contexto.Variables.ObtenerFaltanteCoberturaOpcion(slot.Id))
                .Where(variable => variable is not null)
                .Cast<IntVar>()
                .ToArray();

            if (variablesApoyoCedido.Length == 0 && variablesFaltanteCobertura.Length == 0)
            {
                var auxiliaresEsperados = turnosTotalesEsperados - turnosNoAuxiliaresBase;
                if (auxiliaresEsperados < 0)
                {
                    continue;
                }

                contexto.Modelo.Add(LinearExpr.Sum(variablesAuxiliares) == auxiliaresEsperados);
                continue;
            }

            contexto.Modelo.Add(
                LinearExpr.Sum(variablesAuxiliares) - LinearExpr.Sum(variablesApoyoCedido) - LinearExpr.Sum(variablesFaltanteCobertura)
                == turnosTotalesEsperados - turnosNoAuxiliaresBase);
        }
    }

    private static bool TieneFeriadoLaborableEnSemana(ContextoModeloCp contexto, int indiceSemana)
    {
        return contexto.Problema.Feriados.Any(fecha =>
            fecha >= contexto.Problema.FechaInicio.AddDays(indiceSemana * 7) &&
            fecha <= contexto.Problema.FechaInicio.AddDays((indiceSemana * 7) + 6) &&
            CalculadoraCreditoFeriado.EsFeriadoLaborable(contexto.Problema, fecha));
    }

    private static bool HayVacacionesEnSemana(ContextoModeloCp contexto, int indiceSemana)
    {
        return contexto.Problema.Empleados.Any(empleado =>
            CalculadoraDisponibilidadVacaciones.ObtenerFechasBloqueadas(contexto.Problema, empleado.Id)
                .Any(fecha => contexto.Problema.Slots.Any(slot => slot.IndiceSemana == indiceSemana && slot.Fecha == fecha)));
    }
}
