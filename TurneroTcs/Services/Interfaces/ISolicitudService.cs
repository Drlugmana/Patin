using TurneroTcs.Records;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Services.Interfaces;

public interface ISolicitudService
{
    Task<Result> CreateAsync(
        string solicitanteId,
        string creatorRole,
        SolicitudCreateRequest request);

    Task<Result> ApproveAsync(string solicitudId, string approverId, string approverRole, string approverUserId);

    Task<Result> RejectAsync(string solicitudId, string approverId, string approverRole, string approverUserId);

    Task<Result> CancelAsync(string solicitudId, string actorPersonaId, string actorRole, string actorUserId);

    Task<IReadOnlyList<SolicitudListItemViewModel>> GetAllAsync(
        string? personaId,
        string? equipoId,
        string role,
        bool canReviewEquipoSolicitudes = false);
}
