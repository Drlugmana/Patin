using TurneroTcs.Models;
using TurneroTcs.Records;

namespace TurneroTcs.Services.Interfaces;

public interface IFeriadoService
{
    Task<IReadOnlyList<Feriado>> GetAllAsync();

    Task<Result> CreateAsync(FeriadoCreateRequest request);

    Task<Result> PatchAsync(string feriadoId, FeriadoPatchRequest request, string currentUserRole);

    Task<Result> DeleteAsync(string feriadoId, string currentUserRole);
}
