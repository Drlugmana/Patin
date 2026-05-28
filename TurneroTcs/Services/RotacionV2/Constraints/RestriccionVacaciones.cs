using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Bloquea la asignacion de empleados en los dias en que tienen ausencias registradas.
/// Esta restriccion solo se aplica si la politica <see cref="Domain.PoliticasConfigurablesEquipo.AplicarVacaciones"/>
/// esta habilitada en las reglas del equipo.
/// </summary>
public static class RestriccionVacaciones
{
    /// <summary>
    /// Registra en el modelo la restriccion que fija en cero las variables de asignacion
    /// de cada empleado en sus fechas bloqueadas.
    /// </summary>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        if (!contexto.Problema.Reglas.Configurables.AplicarVacaciones)
        {
            return;
        }

        foreach (var empleado in contexto.Problema.Empleados)
        {
            var fechasBloqueadas = CalculadoraDisponibilidadVacaciones.ObtenerFechasBloqueadas(contexto.Problema, empleado.Id);
            foreach (var fecha in fechasBloqueadas)
            {
                var slotsFecha = contexto.Problema.Slots.Where(slot => slot.Fecha == fecha);
                foreach (var slot in slotsFecha)
                {
                    contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id) == 0);
                }
            }
        }
    }
}
