using TurneroTcs.Models;
using TurneroTcs.Records;

namespace TurneroTcs.Services.Interfaces;

public interface IGrupoService
{
    Task<Result> CreateGrupoAsync(Grupo grupo);
    Task<IReadOnlyList<Grupo>> GetAllAsync();
    Task<IReadOnlyList<Grupo>> GetByPersonaAsync(string personaId);
    Task<Result> PatchAsync(string grupoId, GrupoPatchRequest request, string currentUserRole);
    Task<Result> DeleteAsync(string grupoId, string currentUserRole);
}
