namespace TurneroTcs.Records;

/// <summary>
/// Elemento de lista que representa a un usuario dentro del contexto de un permiso de acceso,
/// mostrando el estado de asignación directa del permiso para ese usuario.
/// </summary>
/// <param name="UserId">Identificador único del usuario.</param>
/// <param name="UserName">Nombre de usuario o correo electrónico del usuario.</param>
/// <param name="EstadoAsignacion">
/// Estado de la asignación directa del permiso para el usuario.
/// Los valores posibles son <c>"Allow"</c>, <c>"Deny"</c> o <c>"None"</c>.
/// </param>
public sealed record PermisoAccesoUserListItem(
    string UserId,
    string UserName,
    string EstadoAsignacion);
