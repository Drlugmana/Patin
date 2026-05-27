using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Services.Interfaces;
public interface IPersonaService
{
    Task<Result> CreateAsync(Persona persona, 
                            string rawPassword, 
                            string roleName, 
                            string? equipoId, 
                            string currentUserRole,
                            string currentUserId,
                            IReadOnlyCollection<string> grupoIds);

    Task<IReadOnlyList<PersonaListViewModel>> GetAllAsync();

    Task<Result> PatchAsync(string personaId, PersonaPatchRequest request, string currentUserRole, string currentUserId);

    Task<Result<PersonaDeleteImpactSummary>> GetDeleteImpactAsync(string personaId, string currentUserRole, string currentUserId);

    Task<Result> DeleteAsync(string personaId, string currentUserRole, string currentUserId);

    Task<Result> RestoreAsync(string personaId, string currentUserRole, string currentUserId);
}
