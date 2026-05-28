namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para eliminar un turno registrado del sistema.
/// </summary>
/// <param name="TurnoId">Identificador del turno que se desea eliminar.</param>
public sealed record RegistroTurnoDeleteRequest(string TurnoId);

/// <summary>
/// Solicitud para marcar o desmarcar un turno como turno extra.
/// </summary>
/// <param name="TurnoId">Identificador del turno a modificar.</param>
/// <param name="EsTurnoExtra">
/// <see langword="true"/> para marcar el turno como extra;
/// <see langword="false"/> para revertir la marca de turno extra.
/// </param>
public sealed record RegistroTurnoExtraRequest(string TurnoId, bool EsTurnoExtra);
