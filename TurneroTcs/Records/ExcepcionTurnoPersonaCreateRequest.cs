namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para registrar una excepcion temporal de turnos para una persona.
/// </summary>
/// <param name="PersonaId">Identificador de la persona afectada.</param>
/// <param name="TipoTurnoId">Identificador del tipo de turno que no puede realizar.</param>
/// <param name="MotivoExcepcion">Motivo de la excepcion.</param>
/// <param name="FechaInicio">Fecha de inicio de la restriccion.</param>
/// <param name="FechaFin">Fecha de fin de la restriccion.</param>
public sealed record ExcepcionTurnoPersonaCreateRequest(
    string PersonaId,
    string TipoTurnoId,
    string MotivoExcepcion,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    IEnumerable<int> DiasSemana
);
