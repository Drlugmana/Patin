using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Aplica la restricción de cobertura mínima a cada slot del problema de rotación.
/// <para>
/// Para cada slot se distinguen tres casos:
/// <list type="bullet">
///   <item><b>Auxiliar</b>: se fija un máximo de empleados igual a la capacidad planificada.</item>
///   <item><b>Opcional por vacación</b>: si hay una vacación primaria activa en el grupo y la fecha,
///   el slot también se acota a su capacidad máxima (puede quedar con menos del requerido).</item>
///   <item><b>Regular con apoyo cedible</b>: se crea una variable de apoyo cedido y se exige que
///   la suma de asignaciones más el apoyo cedido iguale exactamente la demanda del slot.
///   El apoyo cedido solo se permite si existen empleados del grupo que estén trabajando
///   en otro slot de la misma fecha.</item>
/// </list>
/// </para>
/// </summary>
public static class RestriccionCobertura
{
    /// <summary>
    /// Registra en el modelo de optimización la restricción de cobertura para todos los slots del problema.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        var apoyoCediblePorGrupoFecha = new Dictionary<(string GrupoId, DateOnly Fecha), List<IntVar>>();

        foreach (var slot in contexto.Problema.Slots)
        {
            foreach (var empleado in contexto.Problema.Empleados)
            {
                if (TieneExcepcionActiva(contexto, empleado.Id, slot))
                {
                    contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id) == 0);
                }
            }

            var variablesSlot = contexto.Variables.ObtenerAsignacionesPorSlot(contexto.Problema, slot.Id);

            if (slot.EsAuxiliar)
            {
                var capacidadMaxima = slot.CapacidadPlanificada > 0 ? slot.CapacidadPlanificada : slot.EmpleadosRequeridos;
                contexto.Modelo.Add(LinearExpr.Sum(variablesSlot) <= capacidadMaxima);
                continue;
            }

            // Slot marcado flexible: siempre puede reducirse hasta min_personas (MinimoFlexible).
            // Si el slot cae en feriado, AplicarCoberturaReducidaFeriados ya actualizo
            // CapacidadPlanificada con el valor del feriado, que opera como techo.
            if (slot.PuedeOmitirsePorVacacion)
            {
                var capacidadMaxima = slot.CapacidadPlanificada > 0 ? slot.CapacidadPlanificada : slot.EmpleadosRequeridos;
                contexto.Modelo.Add(LinearExpr.Sum(variablesSlot) >= slot.MinimoFlexible);
                contexto.Modelo.Add(LinearExpr.Sum(variablesSlot) <= capacidadMaxima);
                continue;
            }

            var maximoApoyoCedible = Math.Min(slot.EmpleadosRequeridos, Math.Max(0, slot.MaximoApoyoCedible));
            if (maximoApoyoCedible == 0 || string.IsNullOrWhiteSpace(slot.GrupoId))
            {
                contexto.Modelo.Add(LinearExpr.Sum(variablesSlot) == slot.EmpleadosRequeridos);
                continue;
            }

            var apoyoCedible = contexto.Modelo.NewIntVar(0, maximoApoyoCedible, $"apoyo_cedido_{slot.Id}");
            contexto.Variables.RegistrarApoyoCedido(slot.Id, apoyoCedible);
            contexto.Modelo.Add(LinearExpr.Sum(variablesSlot) + apoyoCedible == slot.EmpleadosRequeridos);

            var clave = (slot.GrupoId.Trim(), slot.Fecha);
            if (!apoyoCediblePorGrupoFecha.TryGetValue(clave, out var variablesApoyo))
            {
                variablesApoyo = [];
                apoyoCediblePorGrupoFecha[clave] = variablesApoyo;
            }

            variablesApoyo.Add(apoyoCedible);
        }

        foreach (var (clave, variablesApoyo) in apoyoCediblePorGrupoFecha)
        {
            var variablesAyudaExterna = contexto.Problema.Empleados
                .Where(empleado => string.Equals(empleado.GrupoPrimarioId, clave.GrupoId, StringComparison.OrdinalIgnoreCase))
                .SelectMany(empleado => contexto.Problema.Slots
                    .Where(slot =>
                        slot.Fecha == clave.Fecha &&
                        !string.Equals(slot.GrupoId, clave.GrupoId, StringComparison.OrdinalIgnoreCase))
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id)))
                .ToArray();

            if (variablesAyudaExterna.Length == 0)
            {
                contexto.Modelo.Add(LinearExpr.Sum(variablesApoyo) == 0);
                continue;
            }

            var grupoNormalizado = clave.GrupoId.Replace(' ', '_');
            var ayudaExternaActiva = contexto.Modelo.NewBoolVar($"ayuda_externa_{grupoNormalizado}_{clave.Fecha:yyyyMMdd}");

            contexto.Modelo.Add(LinearExpr.Sum(variablesAyudaExterna) >= 1).OnlyEnforceIf(ayudaExternaActiva);
            contexto.Modelo.Add(LinearExpr.Sum(variablesAyudaExterna) == 0).OnlyEnforceIf(ayudaExternaActiva.Not());

            foreach (var variableApoyo in variablesApoyo)
            {
                contexto.Modelo.Add(variableApoyo == 0).OnlyEnforceIf(ayudaExternaActiva.Not());
            }
        }
    }

    private static bool TieneExcepcionActiva(ContextoModeloCp contexto, string empleadoId, Domain.SlotTurno slot)
    {
        return contexto.Problema.Excepciones.Any(excepcion =>
            string.Equals(excepcion.EmpleadoId, empleadoId, StringComparison.OrdinalIgnoreCase) &&
            excepcion.AplicaA(slot.Fecha, slot.TipoTurnoId) &&
            contexto.EmpleadoPorId.ContainsKey(empleadoId));
    }
}
