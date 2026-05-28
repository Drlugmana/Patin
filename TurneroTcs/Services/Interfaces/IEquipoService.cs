using TurneroTcs.Models;
using TurneroTcs.Records;

namespace TurneroTcs.Services.Interfaces;

public interface IEquipoService
{
    Task<Result> CreateEquipoAsync(Equipo equipo);

    Task<IReadOnlyList<Equipo>> GetAllAsync();

    Task<Result> PatchAsync(string equipoId, EquipoPatchRequest request, string currentUserRole);

    Task<Result> DeleteAsync(string equipoId, string currentUserRole);
}
