namespace TurneroTcs.Services.RotacionV2.Domain;

/// <summary>
/// Representa la semana en la que un empleado regresa de vacaciones y, por lo tanto,
/// debe conservar un descanso de dos dias consecutivos en algun punto de esa misma semana.
/// </summary>
public sealed record DescansoPosteriorVacacion
{
    /// <summary>Identificador del empleado que regresa de vacaciones.</summary>
    public required string EmpleadoId { get; init; }

    /// <summary>
    /// Primera fecha laborable potencial despues del ultimo dia del bloque continuo de vacaciones.
    /// </summary>
    public required DateOnly FechaRegreso { get; init; }
}
