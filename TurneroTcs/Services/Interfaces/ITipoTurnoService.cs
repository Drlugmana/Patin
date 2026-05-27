using TurneroTcs.Models;
using TurneroTcs.Records;

namespace TurneroTcs.Services.Interfaces;

public interface ITipoTurnoService {

    Task<Result> CreateTurno(TipoTurno tipoTurno);

    Task<IReadOnlyList<TipoTurno>> GetAllAsync();

    Task<Result> PatchAsync(string tipoTurnoId, TipoTurnoPatchRequest request, string currentUserRole);

    Task<Result> DeleteAsync(string tipoTurnoId, string currentUserRole);
}
