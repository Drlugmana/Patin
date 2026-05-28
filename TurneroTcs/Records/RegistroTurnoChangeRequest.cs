namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para cambiar un turno registrado. Actúa como un tipo discriminado
/// que puede representar una operación de movimiento (<see cref="RegistroTurnoMoveRequest"/>)
/// o de intercambio (<see cref="RegistroTurnoSwapRequest"/>) según el valor de <see cref="Type"/>.
/// </summary>
/// <param name="Type">
/// Tipo de cambio a realizar. Los valores esperados son <c>"move"</c> para mover
/// el turno a otra fecha o tipo, y <c>"swap"</c> para intercambiar dos turnos entre personas.
/// </param>
/// <param name="Move">Datos del movimiento cuando <see cref="Type"/> es <c>"move"</c>; de lo contrario <see langword="null"/>.</param>
/// <param name="Swap">Datos del intercambio cuando <see cref="Type"/> es <c>"swap"</c>; de lo contrario <see langword="null"/>.</param>
public sealed record RegistroTurnoChangeRequest(
    string Type,
    RegistroTurnoMoveRequest? Move,
    RegistroTurnoSwapRequest? Swap);
