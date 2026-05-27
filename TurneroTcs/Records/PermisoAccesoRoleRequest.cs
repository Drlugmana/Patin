namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para asociar o desasociar un rol de un permiso de acceso.
/// Se utiliza para gestionar qué roles heredan automáticamente un permiso determinado.
/// </summary>
/// <param name="RoleId">Identificador del rol a asociar o desasociar del permiso.</param>
public sealed record PermisoAccesoRoleRequest(string RoleId);
