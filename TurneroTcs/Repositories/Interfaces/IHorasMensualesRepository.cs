using TurneroTcs.Models;

namespace TurneroTcs.Repositories.Interfaces;

public interface IHorasMensualesRepository
{
    Task<IReadOnlyList<Feriado>> GetFeriadosAsync();

    Task<Persona?> GetPersonaByUserIdAsync(string userId);

    Task<IReadOnlyList<Equipo>> GetEquiposAsync(string? equipoIdFilter = null);

    Task<IReadOnlyList<Grupo>> GetActiveGruposByEquipoAsync(string equipoId);

    Task<IReadOnlyList<Persona>> GetActivePersonasByEquipoAsync(string? equipoId = null);

    Task<IReadOnlyList<HorasMensualesPersonaGrupoRow>> GetPersonaGruposAsync(IReadOnlyCollection<string> personaIds);

    Task<IReadOnlyList<HorasMensualesTurnoRow>> GetTurnosAsync(
        IReadOnlyCollection<string> personaIds,
        DateOnly fromDate,
        DateOnly toDate);

    Task<IReadOnlyList<HorasMensualesCambioTurnoRow>> GetCambiosTurnoAsync(IReadOnlyCollection<string> turnoIds);
}
