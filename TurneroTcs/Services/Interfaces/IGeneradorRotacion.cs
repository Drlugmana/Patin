
using TurneroTcs.Records;

namespace TurneroTcs.Services.Interfaces;

public interface IGeneradorRotacion
{
    Task<Result> GenerarAsync(List<string> gruposIds, int numeroSemanas);
}