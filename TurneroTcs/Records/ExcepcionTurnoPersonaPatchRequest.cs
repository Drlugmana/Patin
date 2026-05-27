namespace TurneroTcs.Records;

/// <summary>
/// Solicitud de actualizacion parcial para una excepcion de turnos.
/// </summary>
/// <param name="PersonaId">Nuevo identificador de persona; <see langword="null"/> para conservar el actual.</param>
/// <param name="TipoTurnoId">Nuevo tipo de turno; <see langword="null"/> para conservar el actual.</param>
/// <param name="MotivoExcepcion">Nuevo motivo; <see langword="null"/> para conservar el actual.</param>
/// <param name="FechaInicio">Nueva fecha de inicio; <see langword="null"/> para conservar la actual.</param>
/// <param name="FechaFin">Nueva fecha de fin; <see langword="null"/> para conservar la actual.</param>
public sealed record ExcepcionTurnoPersonaPatchRequest(
    string? PersonaId,
    string? TipoTurnoId,
    string? MotivoExcepcion,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    IEnumerable<int>? DiasSemana
);
