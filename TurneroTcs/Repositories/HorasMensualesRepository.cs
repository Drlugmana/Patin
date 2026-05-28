using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Repositories.Interfaces;

namespace TurneroTcs.Repositories;

public class HorasMensualesRepository : IHorasMensualesRepository
{
    private readonly ApplicationDbContext _db;

    public HorasMensualesRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Feriado>> GetFeriadosAsync()
    {
        return await _db.Feriados
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Persona?> GetPersonaByUserIdAsync(string userId)
    {
        return await _db.Personas
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId && !p.Borrado);
    }

    public async Task<IReadOnlyList<Equipo>> GetEquiposAsync(string? equipoIdFilter = null)
    {
        var query = _db.Equipos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(equipoIdFilter))
        {
            query = query.Where(e => e.EquipoId == equipoIdFilter);
        }

        return await query
            .OrderBy(e => e.NombreEquipo)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Grupo>> GetActiveGruposByEquipoAsync(string equipoId)
    {
        return await _db.Grupos
            .AsNoTracking()
            .Where(g => g.EquipoId == equipoId && g.Activo)
            .OrderBy(g => g.NombreGrupo)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Persona>> GetActivePersonasByEquipoAsync(string? equipoId = null)
    {
        var query = _db.Personas
            .AsNoTracking()
            .Where(p => !p.Borrado);

        if (!string.IsNullOrWhiteSpace(equipoId))
        {
            query = query.Where(p => p.EquipoId == equipoId);
        }

        return await query
            .OrderBy(p => p.Apellido)
            .ThenBy(p => p.Nombre)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<HorasMensualesPersonaGrupoRow>> GetPersonaGruposAsync(IReadOnlyCollection<string> personaIds)
    {
        if (personaIds.Count == 0)
        {
            return Array.Empty<HorasMensualesPersonaGrupoRow>();
        }

        return await (
            from pg in _db.PersonaGrupos.AsNoTracking()
            join g in _db.Grupos.AsNoTracking() on pg.GrupoId equals g.GrupoId
            where personaIds.Contains(pg.PersonaId)
            select new HorasMensualesPersonaGrupoRow(
                pg.PersonaId,
                pg.GrupoId,
                g.NombreGrupo))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<HorasMensualesTurnoRow>> GetTurnosAsync(
        IReadOnlyCollection<string> personaIds,
        DateOnly fromDate,
        DateOnly toDate)
    {
        if (personaIds.Count == 0)
        {
            return Array.Empty<HorasMensualesTurnoRow>();
        }

        return await (
            from rt in _db.RegistroTurnos.AsNoTracking()
            join p in _db.Personas.AsNoTracking() on rt.PersonaId equals p.PersonaId
            join tt in _db.TipoTurnos.AsNoTracking() on rt.TipoTurnoId equals tt.TipoTurnoId
            join g in _db.Grupos.AsNoTracking() on rt.GrupoId equals g.GrupoId into grupoJoin
            from g in grupoJoin.DefaultIfEmpty()
            where personaIds.Contains(rt.PersonaId)
                && rt.FechaTurno >= fromDate
                && rt.FechaTurno <= toDate
            select new HorasMensualesTurnoRow
            {
                TurnoId = rt.TurnoId,
                PersonaId = rt.PersonaId,
                Nombre = BuildPersonaName(p.Nombre, p.SegundoNombre, p.Apellido, p.SegundoApellido),
                Equipo = p.EquipoId ?? string.Empty,
                EquipoNombre = string.Empty,
                GrupoNombre = g != null ? g.NombreGrupo : "-",
                Fecha = rt.FechaTurno,
                EsFeriado = rt.EsFeriado,
                NoLaboradoPorFeriado = rt.NoLaboradoPorFeriado,
                HoraInicio = tt.HoraInicio,
                HoraFin = tt.HoraFin
            })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<HorasMensualesCambioTurnoRow>> GetCambiosTurnoAsync(IReadOnlyCollection<string> turnoIds)
    {
        if (turnoIds.Count == 0)
        {
            return Array.Empty<HorasMensualesCambioTurnoRow>();
        }

        return await (
            from c in _db.CambiosTurno.AsNoTracking()
            join s in _db.Solicitudes.AsNoTracking() on c.SolicitudId equals s.SolicitudId
            where turnoIds.Contains(c.TurnoOrigenId) || turnoIds.Contains(c.TurnoDestinoId)
            select new HorasMensualesCambioTurnoRow(
                c.TurnoOrigenId,
                c.TurnoDestinoId,
                s.EstadoSolicitud,
                s.ActualizadoEn))
            .ToListAsync();
    }

    private static string BuildPersonaName(string? nombre, string? segundoNombre, string? apellido, string? segundoApellido)
    {
        return string.Join(" ", new[] { nombre, segundoNombre, apellido, segundoApellido }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
