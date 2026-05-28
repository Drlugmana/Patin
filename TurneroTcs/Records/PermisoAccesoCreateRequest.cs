namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para registrar un nuevo permiso de acceso en el sistema de autorización.
/// Los permisos de acceso controlan qué operaciones pueden realizar los usuarios dentro de un módulo.
/// </summary>
/// <param name="CodigoPermiso">Código único e inmutable que identifica el permiso (por ejemplo, <c>"turnos.ver"</c>).</param>
/// <param name="NombrePermiso">Nombre legible del permiso para mostrar en la interfaz.</param>
/// <param name="Descripcion">Descripción detallada del permiso; <see langword="null"/> si no aplica.</param>
/// <param name="Modulo">Módulo del sistema al que pertenece el permiso (por ejemplo, <c>"Turnos"</c>, <c>"Solicitudes"</c>).</param>
public sealed record PermisoAccesoCreateRequest(
    string CodigoPermiso,
    string NombrePermiso,
    string? Descripcion,
    string Modulo);
