using TurneroTcs.Models;
using TurneroTcs.Records;

namespace TurneroTcs.Services.Interfaces;

public interface IExcepcionTurnoPersonaService
{
    Task<Result<IReadOnlyList<ExcepcionTurnoPersona>>> GetAllAsync(string equipoId, string currentUserRole, string? currentUserEquipoId);

    Task<Result> CreateAsync(string equipoId, ExcepcionTurnoPersonaCreateRequest request, string currentUserRole, string? currentUserEquipoId);

    Task<Result> PatchAsync(string equipoId, string excepcionId, ExcepcionTurnoPersonaPatchRequest request, string currentUserRole, string? currentUserEquipoId);

    Task<Result> DeleteAsync(string equipoId, string excepcionId, string currentUserRole, string? currentUserEquipoId);
}
