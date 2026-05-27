using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Limita el número de turnos nocturnos que cada empleado puede acumular
/// en un mes calendario, según el valor configurado en las políticas del equipo.
/// Un turno es considerado nocturno cuando más del 70 % de su duración
/// cae dentro de la ventana nocturna (18:00–07:00).
/// La restricción no se aplica si el límite mensual no está configurado.
/// </summary>
public static class RestriccionMaximoTurnosNocturnosPorMes
{
    /// <summary>
    /// Registra en el modelo la restricción mensual de turnos nocturnos por empleado.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        var maximoTurnosNocturnosPorMes = contexto.Problema.Reglas.Configurables.MaximoTurnosNocturnosPorMes;
        if (maximoTurnosNocturnosPorMes is null || maximoTurnosNocturnosPorMes <= 0)
        {
            return;
        }

        foreach (var empleado in contexto.Problema.Empleados)
        {
            var gruposMes = contexto.Problema.Slots
                .Where(slot => slot.EsTurnoNocturno)
                .GroupBy(slot => new { slot.Fecha.Year, slot.Fecha.Month });

            foreach (var grupoMes in gruposMes)
            {
                var variablesMes = grupoMes
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                if (variablesMes.Length > 0)
                {
                    contexto.Modelo.Add(LinearExpr.Sum(variablesMes) <= maximoTurnosNocturnosPorMes.Value);
                }
            }
        }
    }
}
