using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Impide que un empleado sea asignado a un slot de un grupo al que no pertenece,
/// ya sea como miembro primario o como miembro secundario autorizado.
/// Para cada par (empleado, slot) donde el empleado no puede cubrir el grupo del slot,
/// la variable de asignación se fija en cero.
/// </summary>
public static class RestriccionGruposPermitidos
{
    /// <summary>
    /// Registra en el modelo la restricción de elegibilidad por grupo para cada par (empleado, slot).
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        foreach (var empleado in contexto.Problema.Empleados)
        {
            foreach (var slot in contexto.Problema.Slots)
            {
                // Si el slot es auxiliar, solo los miembros primarios pueden cubrirlo.
                if (slot.EsAuxiliar)
                {
                    if (!string.Equals(empleado.GrupoPrimarioId, slot.GrupoId, StringComparison.OrdinalIgnoreCase))
                    {
                        contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id) == 0);
                    }
                    continue;
                }

                if (PuedeCubrirGrupo(empleado, slot.GrupoId))
                {
                    continue;
                }

                contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id) == 0);
            }
        }
    }

    private static bool PuedeCubrirGrupo(Domain.Empleado empleado, string grupoId)
    {
        if (string.IsNullOrWhiteSpace(grupoId))
        {
            return true;
        }

        if (string.Equals(empleado.GrupoPrimarioId, grupoId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return empleado.GruposSecundariosIds.Contains(grupoId);
    }
}
