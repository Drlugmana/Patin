namespace TurneroTcs.Records;

/// <summary>
/// Solicitud de actualización parcial (PATCH) para modificar uno o más atributos de un equipo existente.
/// Solo los campos con valor distinto de <see langword="null"/> serán actualizados.
/// </summary>
/// <param name="NombreEquipo">Nuevo nombre del equipo; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="Activo">
/// Nuevo estado de actividad del equipo; <see langword="null"/> para conservar el valor actual.
/// </param>
/// <param name="TipoGeneracion">
/// Nuevo tipo de generación de turnos del equipo (por ejemplo, <c>"automatico"</c>, <c>"manual"</c>);
/// <see langword="null"/> para conservar el valor actual.
/// </param>
public sealed record EquipoPatchRequest(
    string? NombreEquipo = null,
    bool? Activo = null,
    string? TipoGeneracion = null
);
