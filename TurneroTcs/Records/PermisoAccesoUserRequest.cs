namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para establecer o modificar la asignación directa de un permiso de acceso a un usuario.
/// Permite conceder o denegar explícitamente un permiso, independientemente de los roles del usuario.
/// </summary>
/// <param name="UserId">Identificador del usuario al que se aplica la asignación del permiso.</param>
/// <param name="EsDenegado">
/// <see langword="true"/> para denegar explícitamente el permiso al usuario;
/// <see langword="false"/> para concederlo;
/// <see langword="null"/> para eliminar la asignación directa y depender únicamente de los roles.
/// </param>
public sealed record PermisoAccesoUserRequest(
    string UserId,
    bool? EsDenegado);
