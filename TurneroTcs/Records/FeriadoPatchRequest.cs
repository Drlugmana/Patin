namespace TurneroTcs.Records;

/// <summary>
/// Solicitud de actualizacion parcial (PATCH) para modificar uno o mas atributos de un feriado existente.
/// Solo los campos con valor distinto de <see langword="null"/> seran actualizados.
/// </summary>
/// <param name="NombreFeriado">Nuevo nombre del feriado; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="InicioFeriado">Nueva fecha de inicio; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="FinFeriado">Nueva fecha de fin; <see langword="null"/> para conservar el valor actual.</param>
public record FeriadoPatchRequest(
    string? NombreFeriado,
    DateOnly? InicioFeriado,
    DateOnly? FinFeriado
);
