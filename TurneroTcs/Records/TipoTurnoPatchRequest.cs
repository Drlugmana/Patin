namespace TurneroTcs.Records;

/// <summary>
/// Solicitud de actualización parcial (PATCH) para modificar uno o más atributos de un tipo de turno existente.
/// Solo los campos con valor distinto de <see langword="null"/> serán actualizados.
/// </summary>
/// <param name="NombreTurno">Nuevo nombre del tipo de turno; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="HoraInicio">Nueva hora de inicio del turno; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="HoraFin">Nueva hora de fin del turno; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="Activo">
/// Nuevo estado de actividad del tipo de turno; <see langword="null"/> para conservar el valor actual.
/// </param>
public sealed record TipoTurnoPatchRequest(
    string? NombreTurno = null,
    TimeOnly? HoraInicio = null,
    TimeOnly? HoraFin = null,
    bool? Activo = null
);
