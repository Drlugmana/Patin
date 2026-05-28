namespace TurneroTcs.Records;

/// <summary>
/// Solicitud de actualización parcial (PATCH) para modificar uno o más atributos de un grupo existente.
/// Solo los campos con valor distinto de <see langword="null"/> serán actualizados.
/// </summary>
/// <param name="NombreGrupo">Nuevo nombre del grupo; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="EquipoId">
/// Identificador del nuevo equipo al que pertenece el grupo;
/// <see langword="null"/> para conservar el valor actual.
/// </param>
/// <param name="Activo">
/// Nuevo estado de actividad del grupo; <see langword="null"/> para conservar el valor actual.
/// </param>
public sealed record GrupoPatchRequest(
    string? NombreGrupo = null,
    string? EquipoId = null,
    bool? Activo = null
);
