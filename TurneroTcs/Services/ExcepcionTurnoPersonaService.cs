using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Services.Interfaces;

namespace TurneroTcs.Services;

public class ExcepcionTurnoPersonaService : IExcepcionTurnoPersonaService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ExcepcionTurnoPersonaService> _logger;

    public ExcepcionTurnoPersonaService(ApplicationDbContext db, ILogger<ExcepcionTurnoPersonaService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<ExcepcionTurnoPersona>>> GetAllAsync(string equipoId, string currentUserRole, string? currentUserEquipoId)
    {
        var access = ValidateAccess(equipoId, currentUserRole, currentUserEquipoId);
        if (!access.Succeeded)
        {
            return Result<IReadOnlyList<ExcepcionTurnoPersona>>.Fail(access.Error ?? "No autorizado.");
        }

        var list = await _db.ExcepcionesTurnoPersonas
            .AsNoTracking()
            .Include(e => e.Persona)
            .Include(e => e.TipoTurno)
            .Where(e => e.Persona != null && e.Persona.EquipoId == equipoId)
            .OrderByDescending(e => e.FechaInicio)
            .ThenByDescending(e => e.FechaFin)
            .ThenBy(e => e.Persona!.Nombre)
            .ThenBy(e => e.TipoTurno!.NombreTurno)
            .ToListAsync();

        _logger.LogDebug("Excepciones cargadas para equipo {EquipoId}: {Count}", equipoId, list.Count);
        return Result<IReadOnlyList<ExcepcionTurnoPersona>>.Ok(list);
    }

    public async Task<Result> CreateAsync(string equipoId, ExcepcionTurnoPersonaCreateRequest request, string currentUserRole, string? currentUserEquipoId)
    {
        var access = ValidateAccess(equipoId, currentUserRole, currentUserEquipoId);
        if (!access.Succeeded)
        {
            return access;
        }

        var validation = await ValidateRequestAsync(equipoId, request.PersonaId, request.TipoTurnoId, request.MotivoExcepcion, request.FechaInicio, request.FechaFin);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var diasSolicitados = (request.DiasSemana ?? Array.Empty<int>()).ToHashSet();
        var candidatos = await _db.ExcepcionesTurnoPersonas
            .AsNoTracking()
            .Where(e => e.PersonaId == request.PersonaId
                        && e.TipoTurnoId == request.TipoTurnoId
                        && e.FechaInicio <= request.FechaFin
                        && e.FechaFin >= request.FechaInicio)
            .ToListAsync();

        if (candidatos.Any(c => IntersectanDias(c.DiasSemana, diasSolicitados)))
        {
            return Result.Fail("Ya existe una excepcion solapada para esa persona y tipo de turno en los dias seleccionados.");
        }

        var entity = new ExcepcionTurnoPersona
        {
            PersonaId = request.PersonaId,
            TipoTurnoId = request.TipoTurnoId,
            MotivoExcepcion = request.MotivoExcepcion.Trim(),
            FechaInicio = request.FechaInicio,
            FechaFin = request.FechaFin,
            FechaCreacion = DateTime.UtcNow,
            DiasSemana = string.Join(',', (request.DiasSemana ?? Array.Empty<int>()).Select(i => i.ToString()))
        };

        _db.ExcepcionesTurnoPersonas.Add(entity);
        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> PatchAsync(string equipoId, string excepcionId, ExcepcionTurnoPersonaPatchRequest request, string currentUserRole, string? currentUserEquipoId)
    {
        var access = ValidateAccess(equipoId, currentUserRole, currentUserEquipoId);
        if (!access.Succeeded)
        {
            return access;
        }

        var entity = await _db.ExcepcionesTurnoPersonas
            .Include(e => e.Persona)
            .FirstOrDefaultAsync(e => e.ExcepcionTurnoPersonaId == excepcionId);
        if (entity == null)
        {
            return Result.Fail("La excepcion no existe.");
        }

        if (entity.Persona == null || !string.Equals(entity.Persona.EquipoId, equipoId, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail("La excepcion no pertenece al equipo indicado.");
        }

        var personaId = request.PersonaId ?? entity.PersonaId;
        var tipoTurnoId = request.TipoTurnoId ?? entity.TipoTurnoId;
        var motivo = request.MotivoExcepcion ?? entity.MotivoExcepcion;
        var fechaInicio = request.FechaInicio ?? entity.FechaInicio;
        var fechaFin = request.FechaFin ?? entity.FechaFin;
        var dias = request.DiasSemana ?? (string.IsNullOrWhiteSpace(entity.DiasSemana) ? null : entity.DiasSemana.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));

        var validation = await ValidateRequestAsync(equipoId, personaId, tipoTurnoId, motivo, fechaInicio, fechaFin);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var candidatosPatch = await _db.ExcepcionesTurnoPersonas
            .AsNoTracking()
            .Where(e => e.ExcepcionTurnoPersonaId != excepcionId
                        && e.PersonaId == personaId
                        && e.TipoTurnoId == tipoTurnoId
                        && e.FechaInicio <= fechaFin
                        && e.FechaFin >= fechaInicio)
            .ToListAsync();

        var diasSolicitadosPatch = dias?.ToHashSet() ?? new HashSet<int>();
        if (candidatosPatch.Any(c => IntersectanDias(c.DiasSemana, diasSolicitadosPatch)))
        {
            return Result.Fail("Ya existe una excepcion solapada para esa persona y tipo de turno en los dias seleccionados.");
        }

        entity.PersonaId = personaId;
        entity.TipoTurnoId = tipoTurnoId;
        entity.MotivoExcepcion = motivo.Trim();
        entity.FechaInicio = fechaInicio;
        entity.FechaFin = fechaFin;
        if (request.DiasSemana != null)
        {
            entity.DiasSemana = string.Join(',', request.DiasSemana.Select(i => i.ToString()));
        }
        entity.FechaUltimaActualizacion = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(string equipoId, string excepcionId, string currentUserRole, string? currentUserEquipoId)
    {
        var access = ValidateAccess(equipoId, currentUserRole, currentUserEquipoId);
        if (!access.Succeeded)
        {
            return access;
        }

        var entity = await _db.ExcepcionesTurnoPersonas
            .Include(e => e.Persona)
            .FirstOrDefaultAsync(e => e.ExcepcionTurnoPersonaId == excepcionId);
        if (entity == null)
        {
            return Result.Fail("La excepcion no existe.");
        }

        if (entity.Persona == null || !string.Equals(entity.Persona.EquipoId, equipoId, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail("La excepcion no pertenece al equipo indicado.");
        }

        _db.ExcepcionesTurnoPersonas.Remove(entity);
        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    private static Result ValidateAccess(string equipoId, string currentUserRole, string? currentUserEquipoId)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return Result.Fail("Equipo invalido.");
        }

        if (string.IsNullOrWhiteSpace(currentUserRole))
        {
            return Result.Fail("No se ha podido identificar el rol del usuario.");
        }

        if (currentUserRole == "Lider")
        {
            if (string.IsNullOrWhiteSpace(currentUserEquipoId) || !string.Equals(currentUserEquipoId, equipoId, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Fail("No tiene permiso para administrar excepciones de este equipo.");
            }
        }

        return Result.Ok();
    }

    private async Task<Result> ValidateRequestAsync(string equipoId, string personaId, string tipoTurnoId, string motivoExcepcion, DateOnly fechaInicio, DateOnly fechaFin)
    {
        if (string.IsNullOrWhiteSpace(personaId))
        {
            return Result.Fail("La persona es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(tipoTurnoId))
        {
            return Result.Fail("El tipo de turno es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(motivoExcepcion))
        {
            return Result.Fail("El motivo de la excepcion es obligatorio.");
        }

        if (motivoExcepcion.Trim().Length > 250)
        {
            return Result.Fail("El motivo de la excepcion no puede exceder 250 caracteres.");
        }

        if (fechaFin < fechaInicio)
        {
            return Result.Fail("La fecha fin no puede ser menor a la fecha inicio.");
        }

        var persona = await _db.Personas
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonaId == personaId && !p.Borrado);
        if (persona == null)
        {
            return Result.Fail("La persona no existe.");
        }

        if (!string.Equals(persona.EquipoId, equipoId, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail("La persona no pertenece al equipo indicado.");
        }

        var tipoTurnoExiste = await _db.TipoTurnos
            .AsNoTracking()
            .AnyAsync(t => t.TipoTurnoId == tipoTurnoId);
        if (!tipoTurnoExiste)
        {
            return Result.Fail("El tipo de turno no existe.");
        }

        return Result.Ok();
    }

    private static bool IntersectanDias(string diasAlmacenadosCsv, HashSet<int> diasSolicitados)
    {
        if (diasSolicitados == null) diasSolicitados = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(diasAlmacenadosCsv))
        {
            // almacenado vacio == aplica a todos los dias -> intersecta si el solicitante no especifica dias (equivale a todos) o si solicita alguno
            return true;
        }

        var partes = diasAlmacenadosCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var almacenados = new HashSet<int>();
        foreach (var p in partes)
        {
            if (int.TryParse(p, out var v)) almacenados.Add(v);
        }

        if (almacenados.Count == 0 && diasSolicitados.Count == 0) return true;
        if (almacenados.Count == 0 && diasSolicitados.Count > 0) return true; // stored empty = all days -> intersects

        return almacenados.Overlaps(diasSolicitados);
    }
}
