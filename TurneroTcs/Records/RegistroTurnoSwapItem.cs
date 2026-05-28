namespace TurneroTcs.Records;

/// <summary>
/// Representa un extremo de un intercambio de turnos, identificando el turno
/// y la persona involucrada en la operación.
/// </summary>
/// <param name="TurnoId">Identificador del turno participante en el intercambio.</param>
/// <param name="PersonaId">Identificador de la persona propietaria del turno.</param>
public sealed record RegistroTurnoSwapItem(
    string TurnoId,
    string PersonaId);
