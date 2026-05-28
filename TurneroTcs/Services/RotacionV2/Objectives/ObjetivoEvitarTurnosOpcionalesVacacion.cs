using TurneroTcs.Services.RotacionV2.Constraints;
using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Penaliza cualquier asignación de empleados en slots marcados como opcionales por vacación
/// cuando hay una vacación primaria activa en el grupo y la fecha.
/// <para>
/// El propósito es evitar que el motor asigne innecesariamente personal a turnos que
/// podrían omitirse durante una semana de vacaciones, reduciendo la carga de trabajo
/// y mejorando la equidad. El peso muy alto (12 000) de este objetivo en la función
/// objetivo global asegura que se respete como cuasi-restricción.
/// </para>
/// </summary>
public static class ObjetivoEvitarTurnosOpcionalesVacacion
{
    /// <summary>
    /// Crea la variable de penalización que contabiliza las asignaciones en slots opcionales de vacación.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    /// <returns>
    /// Variable entera de penalización, o <see langword="null"/> si no hay slots opcionales
    /// con vacación primaria activa.
    /// </returns>
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        var variables = contexto.Problema.Slots
            .Where(slot =>
                !slot.EsAuxiliar &&
                slot.PuedeOmitirsePorVacacion &&
                HayVacacionPrimariaEnGrupoFecha(contexto, slot))
            .SelectMany(slot => contexto.Problema.Empleados.Select(empleado => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id)))
            .ToArray();

        if (variables.Length == 0)
        {
            return null;
        }

        var penalizacion = contexto.Modelo.NewIntVar(0, variables.Length, "penalizacion_turnos_opcionales_vacacion");
        contexto.Modelo.Add(penalizacion == LinearExpr.Sum(variables));
        return penalizacion;
    }

    private static bool HayVacacionPrimariaEnGrupoFecha(ContextoModeloCp contexto, Domain.SlotTurno slot)
    {
        if (string.IsNullOrWhiteSpace(slot.GrupoId))
        {
            return false;
        }

        return contexto.Problema.Ausencias.Any(ausencia =>
            CalculadoraDisponibilidadVacaciones.EstaBloqueado(contexto.Problema, ausencia.EmpleadoId, slot.Fecha) &&
            contexto.EmpleadoPorId.TryGetValue(ausencia.EmpleadoId, out var empleado) &&
            string.Equals(empleado.GrupoPrimarioId, slot.GrupoId, StringComparison.OrdinalIgnoreCase));
    }
}
