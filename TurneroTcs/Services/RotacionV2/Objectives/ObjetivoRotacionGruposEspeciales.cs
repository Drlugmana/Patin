using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Penaliza la asignación de empleados a grupos especiales en semanas en las que ese
/// mismo empleado ya fue asignado al grupo especial en semanas anteriores del horizonte.
/// La penalización es proporcional al número de usos previos, incentivando al motor
/// a rotar entre los empleados elegibles y evitar que siempre recaiga sobre la misma persona.
/// Solo aplica cuando existen grupos especiales de persona única configurados y hay estado acumulado.
/// </summary>
public static class ObjetivoRotacionGruposEspeciales
{
    /// <summary>
    /// Crea la variable de penalización de reincidencia en grupos especiales.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema, las variables de decisión y el estado acumulado.</param>
    /// <returns>
    /// Variable entera de penalización ponderada por usos previos, o <see langword="null"/> si no aplica.
    /// </returns>
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        var reglas = contexto.Problema.Reglas.Configurables;
        var estado = contexto.EstadoSemanalAcumulado;
        if (estado is null ||
            reglas.GrupoFuentePorGrupoEspecial.Count == 0 ||
            reglas.GruposEspecialesPersonaUnicaPorSemana.Count == 0)
        {
            return null;
        }

        var indicadores = new List<BoolVar>();
        var pesos = new List<long>();

        foreach (var grupoEspecialId in reglas.GruposEspecialesPersonaUnicaPorSemana)
        {
            if (!reglas.GrupoFuentePorGrupoEspecial.TryGetValue(grupoEspecialId, out var grupoFuenteId))
            {
                continue;
            }

            var empleadosElegibles = contexto.Problema.Empleados
                .Where(empleado => string.Equals(empleado.GrupoPrimarioId, grupoFuenteId, StringComparison.OrdinalIgnoreCase) &&
                                   empleado.GruposSecundariosIds.Contains(grupoEspecialId))
                .ToArray();

            for (var indiceSemana = 0; indiceSemana < contexto.Problema.CantidadSemanas; indiceSemana++)
            {
                var slotsEspeciales = contexto.Problema.Slots
                    .Where(slot => slot.IndiceSemana == indiceSemana &&
                                   slot.EmpleadosRequeridos > 0 &&
                                   string.Equals(slot.GrupoId, grupoEspecialId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (slotsEspeciales.Length == 0)
                {
                    continue;
                }

                foreach (var empleado in empleadosElegibles)
                {
                    var usosPrevios = estado.UsosGrupoEspecialPorEmpleado.TryGetValue((grupoEspecialId, empleado.Id), out var usos)
                        ? usos
                        : 0;
                    if (usosPrevios <= 0)
                    {
                        continue;
                    }

                    var indicador = contexto.Modelo.NewBoolVar($"obj_grupo_especial_{grupoEspecialId}_{empleado.Numero}_{indiceSemana}");
                    var variables = slotsEspeciales
                        .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                        .ToArray();

                    contexto.Modelo.Add(LinearExpr.Sum(variables) >= 1).OnlyEnforceIf(indicador);
                    contexto.Modelo.Add(LinearExpr.Sum(variables) == 0).OnlyEnforceIf(indicador.Not());

                    indicadores.Add(indicador);
                    pesos.Add(usosPrevios);
                }
            }
        }

        if (indicadores.Count == 0)
        {
            return null;
        }

        var maximo = pesos.Sum();
        var penalizacion = contexto.Modelo.NewIntVar(0, maximo, "penalizacion_rotacion_grupos_especiales");
        contexto.Modelo.Add(penalizacion == LinearExpr.WeightedSum(indicadores.ToArray(), pesos.ToArray()));
        return penalizacion;
    }
}
