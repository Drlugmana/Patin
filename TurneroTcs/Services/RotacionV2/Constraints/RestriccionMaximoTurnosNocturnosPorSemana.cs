using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Limita el numero de turnos nocturnos que cada empleado puede acumular
/// en una semana de planificacion, segun el valor configurado para el equipo.
/// </summary>
public static class RestriccionMaximoTurnosNocturnosPorSemana
{
    /// <summary>
    /// Registra en el modelo la restriccion semanal de turnos nocturnos por empleado.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decision.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        var maximoTurnosNocturnosPorSemana = contexto.Problema.Reglas.Configurables.MaximoTurnosNocturnosPorSemana;
        if (maximoTurnosNocturnosPorSemana is null or <= 0)
        {
            return;
        }

        foreach (var empleado in contexto.Problema.Empleados)
        {
            var gruposSemana = contexto.Problema.Slots
                .Where(slot => slot.EsTurnoNocturno)
                .GroupBy(slot => slot.IndiceSemana);

            foreach (var grupoSemana in gruposSemana)
            {
                var variablesSemana = grupoSemana
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                if (variablesSemana.Length > 0)
                {
                    contexto.Modelo.Add(LinearExpr.Sum(variablesSemana) <= maximoTurnosNocturnosPorSemana.Value);
                }
            }
        }
    }
}
