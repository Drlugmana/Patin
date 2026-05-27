using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Penaliza asignaciones donde el empleado cubre un slot de un grupo
/// para el que solo está habilitado como secundario.
/// Esto fuerza al solver a preferir empleados primarios cuando sea posible,
/// usando secundarios como fallback solo cuando sea necesario.
/// </summary>
public static class ObjetivoPenalizarSecundarios
{
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        var modelo = contexto.Modelo;
        var variables = new List<IntVar>();

        foreach (var slot in contexto.Problema.Slots)
        {
            // Excluir slots auxiliares: no tiene sentido usar personales secundarios
            if (slot.EsAuxiliar)
            {
                continue;
            }

            foreach (var empleado in contexto.Problema.Empleados)
            {
                // Es secundario para este grupo (y no es su grupo primario)
                if (!string.Equals(empleado.GrupoPrimarioId, slot.GrupoId, StringComparison.OrdinalIgnoreCase)
                    && empleado.GruposSecundariosIds.Contains(slot.GrupoId))
                {
                    var varAsign = contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id);
                    variables.Add(varAsign);
                }
            }
        }

        if (variables.Count == 0)
        {
            return null;
        }

        var penalizacion = modelo.NewIntVar(0, variables.Count, "penalizacion_secundarios");
        modelo.Add(penalizacion == LinearExpr.Sum(variables));
        return penalizacion;
    }
}
