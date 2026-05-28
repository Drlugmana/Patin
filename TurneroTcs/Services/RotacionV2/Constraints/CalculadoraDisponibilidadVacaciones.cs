using TurneroTcs.Services.RotacionV2.Domain;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Calcula las fechas en las que un empleado no puede ser asignado a ningun turno
/// por ausencias registradas dentro del horizonte actual.
/// </summary>
internal static class CalculadoraDisponibilidadVacaciones
{
    /// <summary>
    /// Obtiene el conjunto completo de fechas bloqueadas para un empleado a partir de
    /// sus ausencias registradas en el problema.
    /// </summary>
    public static HashSet<DateOnly> ObtenerFechasBloqueadas(ProblemaRotacion problema, string empleadoId)
    {
        return problema.Ausencias
            .Where(ausencia => string.Equals(ausencia.EmpleadoId, empleadoId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(ausencia => ausencia.Fechas)
            .ToHashSet();
    }

    /// <summary>
    /// Indica si un empleado esta bloqueado en una fecha especifica por alguna ausencia.
    /// </summary>
    public static bool EstaBloqueado(ProblemaRotacion problema, string empleadoId, DateOnly fecha)
    {
        return ObtenerFechasBloqueadas(problema, empleadoId).Contains(fecha);
    }
}
