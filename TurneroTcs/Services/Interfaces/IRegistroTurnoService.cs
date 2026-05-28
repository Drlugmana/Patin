using TurneroTcs.Records;

namespace TurneroTcs.Services.Interfaces;

public interface IRegistroTurnoService
{
    Task<TurnoGenerationResult> GenerateAsync(
        string personaId,
        IReadOnlyList<string> tipoTurnoIds,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        string? grupoId,
        string currentUserRole,
        string currentUserId);

    Task<TurnoPreviewSaveResult> SavePreviewAsync(
        IReadOnlyList<RegistroTurnoPreviewItem> items,
        string currentUserRole,
        string currentUserId);

    Task<TurnoChangeResult> ConfirmChangeAsync(
        RegistroTurnoChangeRequest request,
        string currentUserRole,
        string currentUserId);

    Task<TurnoChangeResult> DeleteAsync(
        string turnoId,
        string currentUserRole,
        string currentUserId);

    Task<TurnoChangeResult> SetTurnoExtraAsync(
        string turnoId,
        bool esTurnoExtra,
        string currentUserRole,
        string currentUserId);
}
