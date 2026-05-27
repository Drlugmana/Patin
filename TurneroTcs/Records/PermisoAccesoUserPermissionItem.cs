using System.Collections.Generic;

namespace TurneroTcs.Records;

/// <summary>
/// Detalle completo de un permiso de acceso en el contexto de un usuario específico.
/// Muestra el origen del permiso (rol o asignación directa) y su efecto final sobre el usuario.
/// </summary>
/// <param name="PermisoAccesoId">Identificador único del permiso de acceso.</param>
/// <param name="CodigoPermiso">Código único del permiso (por ejemplo, <c>"turnos.ver"</c>).</param>
/// <param name="NombrePermiso">Nombre legible del permiso.</param>
/// <param name="Modulo">Módulo del sistema al que pertenece el permiso.</param>
/// <param name="EsSistema">
/// <see langword="true"/> si el permiso es de sistema y no puede ser modificado manualmente.
/// </param>
/// <param name="GrantedByRole">
/// <see langword="true"/> si el usuario obtiene este permiso a través de al menos un rol asignado.
/// </param>
/// <param name="DirectAssignment">
/// Estado de la asignación directa del permiso sobre el usuario.
/// Los valores posibles son <c>"Allow"</c>, <c>"Deny"</c> o <c>"None"</c>.
/// </param>
/// <param name="EffectiveAssignment">
/// Resultado efectivo del permiso luego de evaluar roles y asignaciones directas.
/// Los valores posibles son <c>"Allow"</c> o <c>"Deny"</c>.
/// </param>
/// <param name="RoleNames">Nombres de los roles que otorgan este permiso al usuario.</param>
public sealed record PermisoAccesoUserPermissionItem(
    string PermisoAccesoId,
    string CodigoPermiso,
    string NombrePermiso,
    string Modulo,
    bool EsSistema,
    bool GrantedByRole,
    string DirectAssignment,
    string EffectiveAssignment,
    IReadOnlyList<string> RoleNames);
