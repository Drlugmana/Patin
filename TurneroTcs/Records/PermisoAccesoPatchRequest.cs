namespace TurneroTcs.Records;

/// <summary>
/// Solicitud de actualización parcial (PATCH) para modificar uno o más atributos
/// de un permiso de acceso existente.
/// Solo los campos con valor distinto de <see langword="null"/> serán actualizados.
/// </summary>
/// <param name="CodigoPermiso">Nuevo código del permiso; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="NombrePermiso">Nuevo nombre del permiso; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="Descripcion">Nueva descripción del permiso; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="Modulo">Nuevo módulo al que pertenece el permiso; <see langword="null"/> para conservar el valor actual.</param>
public sealed record PermisoAccesoPatchRequest(
    string? CodigoPermiso,
    string? NombrePermiso,
    string? Descripcion,
    string? Modulo);
