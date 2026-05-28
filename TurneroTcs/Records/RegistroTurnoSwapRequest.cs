namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para intercambiar los turnos entre dos personas.
/// Cada elemento de la operación identifica un turno y la persona asociada.
/// </summary>
/// <param name="First">Primer elemento del intercambio (turno y persona origen).</param>
/// <param name="Second">Segundo elemento del intercambio (turno y persona destino).</param>
public sealed record RegistroTurnoSwapRequest(
    RegistroTurnoSwapItem First,
    RegistroTurnoSwapItem Second);
