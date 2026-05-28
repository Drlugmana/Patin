using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Impide que un empleado trabaje un turno nocturno el domingo de una semana
/// y otro turno nocturno el lunes de la semana siguiente.
/// <para>
/// Dos turnos nocturnos en días consecutivos en el cruce de semana generan un descanso insuficiente
/// que la restricción de descanso mínimo intra-semana no puede capturar cuando el horizonte
/// se resuelve como una única ventana que abarca varias semanas.
/// Esta restricción cierra ese hueco para horizontes de más de una semana.
/// </para>
/// </summary>
public static class RestriccionVeladasConsecutivasEntreSemanas
{
    /// <summary>
    /// Registra en el modelo la restricción que impide la velada nocturna consecutiva
    /// en el cruce domingo–lunes entre semanas adyacentes.
    /// No aplica si el horizonte tiene una sola semana.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        if (contexto.Problema.CantidadSemanas <= 1)
        {
            return;
        }

        foreach (var empleado in contexto.Problema.Empleados)
        {
            for (var indiceSemana = 0; indiceSemana < contexto.Problema.CantidadSemanas - 1; indiceSemana++)
            {
                var variablesDomingoNocturno = contexto.Problema.Slots
                    .Where(slot =>
                        slot.IndiceSemana == indiceSemana &&
                        slot.IndiceDia == 6 &&
                        slot.EsTurnoNocturno)
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                var variablesLunesNocturno = contexto.Problema.Slots
                    .Where(slot =>
                        slot.IndiceSemana == indiceSemana + 1 &&
                        slot.IndiceDia == 0 &&
                        slot.EsTurnoNocturno)
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                if (variablesDomingoNocturno.Length == 0 || variablesLunesNocturno.Length == 0)
                {
                    continue;
                }

                contexto.Modelo.Add(LinearExpr.Sum(variablesDomingoNocturno) + LinearExpr.Sum(variablesLunesNocturno) <= 1);
            }
        }
    }
}
