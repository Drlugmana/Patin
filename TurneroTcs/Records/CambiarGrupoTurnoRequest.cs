namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para reasignar el grupo de un turno específico perteneciente a una persona.
/// Permite mover un turno a un grupo diferente o dejarlo sin grupo asignado.
/// </summary>
/// <param name="TurnoId">Identificador del turno cuyo grupo se desea cambiar.</param>
/// <param name="PersonaId">Identificador de la persona propietaria del turno.</param>
/// <param name="NuevoGrupoId">
/// Identificador del nuevo grupo al que se asignará el turno.
/// <see langword="null"/> para desvincular el turno de cualquier grupo.
/// </param>
public sealed record CambiarGrupoTurnoRequest(string TurnoId, string PersonaId, string? NuevoGrupoId);
