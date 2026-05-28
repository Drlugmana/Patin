namespace TurneroTcs.Services.RotacionV2.Domain;

/// <summary>
/// Registra las fechas en que un empleado no está disponible para trabajar,
/// ya sea por vacaciones, calamidad u otro tipo de ausencia autorizada.
/// Las fechas bloqueadas excluyen al empleado de ser asignado a cualquier slot en esos días.
/// </summary>
public sealed record AusenciaEmpleado
{
    /// <summary>Identificador del empleado ausente.</summary>
    public required string EmpleadoId { get; init; }

    /// <summary>Descripción o categoría de la ausencia (por ejemplo, <c>"Vacaciones"</c>, <c>"Calamidad"</c>).</summary>
    public required string Motivo { get; init; }

    /// <summary>Conjunto de fechas en las que el empleado no puede ser asignado a ningún turno.</summary>
    public HashSet<DateOnly> Fechas { get; init; } = [];
}
