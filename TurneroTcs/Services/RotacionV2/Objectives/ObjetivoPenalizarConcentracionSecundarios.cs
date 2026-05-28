using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Penaliza asignaciones concentradas de empleados secundarios en una misma semana.
/// Si un empleado cubre múltiples turnos como secundario en la misma semana,
/// la penalización crece cuadráticamente para desalentar la saturación.
/// Esto fuerza al solver a distribuir el uso de secundarios entre múltiples personas.
/// </summary>
public static class ObjetivoPenalizarConcentracionSecundarios
{
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        var modelo = contexto.Modelo;
        var variablesObjetivo = new List<IntVar>();

        // Agrupar slots por semana
        var slotsPorSemana = contexto.Problema.Slots
            .Where(s => !s.EsAuxiliar)
            .GroupBy(s => s.IndiceSemana)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Para cada semana y cada empleado, contar asignaciones secundarias
        foreach (var (indiceSemana, slotsSemanales) in slotsPorSemana)
        {
            var gruposEnSemana = slotsSemanales.Select(s => s.GrupoId).Distinct().ToList();

            foreach (var empleado in contexto.Problema.Empleados)
            {
                // Identificar en qué grupos es secundario este empleado en esta semana
                var gruposSecundariosEnSemana = gruposEnSemana
                    .Where(gid => !string.Equals(empleado.GrupoPrimarioId, gid, StringComparison.OrdinalIgnoreCase)
                                  && empleado.GruposSecundariosIds.Contains(gid))
                    .ToList();

                if (gruposSecundariosEnSemana.Count == 0)
                {
                    continue;
                }

                // Contar cuántos slots como secundario cubre este empleado en esta semana
                var slotsCubiertosComoSecundario = slotsSemanales
                    .Where(s => gruposSecundariosEnSemana.Contains(s.GrupoId))
                    .Select(s => contexto.ObtenerVariableAsignacion(empleado.Id, s.Id))
                    .ToList();

                if (slotsCubiertosComoSecundario.Count == 0)
                {
                    continue;
                }

                // Crear variable que suma los turnos secundarios en esta semana para este empleado
                var conteoAsignacionesSecundarias = modelo.NewIntVar(
                    0,
                    slotsCubiertosComoSecundario.Count,
                    $"conteo_secundarios_E{empleado.Numero}_S{indiceSemana}");
                modelo.Add(conteoAsignacionesSecundarias == LinearExpr.Sum(slotsCubiertosComoSecundario));

                // Penalidad cuadrática: usar 2+ turnos como secundario en la misma semana es muy caro
                // Penalidad = conteo^2 * 100000 (cuadrática para desalentar concentración)
                for (int i = 2; i <= slotsCubiertosComoSecundario.Count; i++)
                {
                    var indicador = modelo.NewBoolVar($"indic_secundarios_E{empleado.Numero}_S{indiceSemana}_ge{i}");
                    modelo.Add(conteoAsignacionesSecundarias >= i).OnlyEnforceIf(indicador);
                    modelo.Add(conteoAsignacionesSecundarias < i).OnlyEnforceIf(indicador.Not());

                    var penalidad = modelo.NewIntVar(
                        0,
                        100_000 * i,
                        $"penalidad_concentracion_E{empleado.Numero}_S{indiceSemana}_#{i}");
                    modelo.Add(penalidad == 100_000 * i).OnlyEnforceIf(indicador);
                    modelo.Add(penalidad == 0).OnlyEnforceIf(indicador.Not());

                    variablesObjetivo.Add(penalidad);
                }
            }
        }

        if (variablesObjetivo.Count == 0)
        {
            return null;
        }

        var penalizacionTotal = modelo.NewIntVar(0, int.MaxValue, "penalizacion_concentracion_secundarios");
        modelo.Add(penalizacionTotal == LinearExpr.Sum(variablesObjetivo));
        return penalizacionTotal;
    }
}
