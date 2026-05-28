using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Impone el límite máximo de turnos que un empleado puede cubrir en un mismo día calendario.
/// El límite se extrae de <see cref="Domain.PoliticasConfigurablesEquipo.MaximoTurnosPorDia"/>
/// y se aplica como restricción de suma sobre todas las variables binarias del empleado en esa fecha.
/// </summary>
public static class RestriccionUnTurnoPorDia
{
    /// <summary>
    /// Registra en el modelo la restricción de máximo de turnos por día para cada empleado.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        var maximoTurnosPorDia = Math.Max(1, contexto.Problema.Reglas.Configurables.MaximoTurnosPorDia);

        foreach (var empleado in contexto.Problema.Empleados)
        {
            foreach (var fecha in contexto.Problema.Slots.Select(slot => slot.Fecha).Distinct())
            {
                var variablesDia = contexto.Variables.ObtenerAsignacionesPorEmpleadoYFecha(contexto.Problema, empleado.Id, fecha);
                contexto.Modelo.Add(LinearExpr.Sum(variablesDia) <= maximoTurnosPorDia);
            }
        }
    }
}
