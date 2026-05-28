using System.Collections.Generic;

namespace TurneroTcs.Records;

/// <summary>
/// Elemento de lista que representa un permiso de acceso con información resumida
/// sobre los roles y usuarios que lo tienen asignado.
/// </summary>
/// <param name="PermisoAccesoId">Identificador único del permiso de acceso.</param>
/// <param name="CodigoPermiso">Código único del permiso (por ejemplo, <c>"turnos.ver"</c>).</param>
/// <param name="NombrePermiso">Nombre legible del permiso.</param>
/// <param name="Descripcion">Descripción del permiso; <see langword="null"/> si no fue definida.</param>
/// <param name="Modulo">Módulo del sistema al que pertenece el permiso.</param>
/// <param name="RoleIds">Colección de identificadores de roles que tienen este permiso asignado.</param>
/// <param name="RoleNames">Colección de nombres de roles que tienen este permiso asignado.</param>
/// <param name="UsuariosAllow">Número de usuarios con asignación directa de tipo <c>Allow</c>.</param>
/// <param name="UsuariosDeny">Número de usuarios con asignación directa de tipo <c>Deny</c>.</param>
public sealed record PermisoAccesoListItem(
    string PermisoAccesoId,
    string CodigoPermiso,
    string NombrePermiso,
    string? Descripcion,
    string Modulo,
    IReadOnlyList<string> RoleIds,
    IReadOnlyList<string> RoleNames,
    int UsuariosAllow,
    int UsuariosDeny);
