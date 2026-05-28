using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Services.Interfaces;

namespace TurneroTcs.Services;

public class EquipoService : IEquipoService
{
    private readonly ApplicationDbContext _db;

    private readonly ILogger<EquipoService> _logger;
    private readonly UserManager<IdentityUser> _userManager;

    public EquipoService (ApplicationDbContext db, ILogger<EquipoService> logger, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _logger = logger;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<Equipo>>GetAllAsync()
    {
        try
        {
            var equipos = await _db.Equipos
                .AsNoTracking()
                .OrderBy(p => p.NombreEquipo)
                .ToListAsync();
            
            _logger.LogDebug("Lista de {Count} equipos traidos exitosamente.", equipos.Count);
            return equipos;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la lista de equipos.");
            throw;
        }
    }

    public async Task<Result> CreateEquipoAsync(Equipo equipo)
    {
        if(equipo == null)
        {
            return Result.Fail("No se ha ingresado ningun equipo.");
        }

        if (string.IsNullOrWhiteSpace(equipo.NombreEquipo))
        {
            return Result.Fail("No se ha ingresado nombre de equipo.");
        }

        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            _db.Equipos.Add(equipo);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogDebug("El equipo {Equipo} ha sido creado satisfactoriamente", equipo.NombreEquipo);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            _logger.LogError(ex, "Error al crear el equipo {Equipo}.", equipo.NombreEquipo);
            return Result.Fail("Ha ocurrido un error al crear el equipo.");
        }
    }

    public async Task<Result> PatchAsync(string equipoId, EquipoPatchRequest request, string currentUserRole)
    {
        if (string.IsNullOrWhiteSpace(currentUserRole))
        {
            return Result.Fail("No se ha podido identificar el rol del usuario.");
        }

        if (currentUserRole != "SuperAdmin" && currentUserRole != "Admin")
        {
            return Result.Fail("No tiene permiso para realizar esta accion.");
        }

        var equipo = await _db.Equipos.SingleOrDefaultAsync(e => e.EquipoId == equipoId);
        if (equipo == null)
        {
            return Result.Fail("Equipo no existe.");
        }

        if (request.NombreEquipo != null)
        {
            if (string.IsNullOrWhiteSpace(request.NombreEquipo))
            {
                return Result.Fail("El nombre del equipo es requerido.");
            }

            equipo.NombreEquipo = request.NombreEquipo;
        }

        if (request.Activo.HasValue)
        {
            equipo.Activo = request.Activo.Value;
        }

        if (request.TipoGeneracion != null)
        {
            equipo.TipoGeneracion = "Rotacion";
        }

        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(string equipoId, string currentUserRole)
    {
        if (string.IsNullOrWhiteSpace(currentUserRole))
        {
            return Result.Fail("No se ha podido identificar el rol del usuario.");
        }

        if (currentUserRole != "SuperAdmin" && currentUserRole != "Admin")
        {
            return Result.Fail("No tiene permiso para realizar esta accion.");
        }

        var equipo = await _db.Equipos.SingleOrDefaultAsync(e => e.EquipoId == equipoId);
        if (equipo == null)
        {
            return Result.Fail("Equipo no existe.");
        }

        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var personaIds = await _db.Personas
                .Where(p => p.EquipoId == equipoId)
                .Select(p => p.PersonaId)
                .ToListAsync();

            var userIds = await _db.Personas
                .Where(p => p.EquipoId == equipoId)
                .Select(p => p.UserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToListAsync();

            var turnoIds = await _db.RegistroTurnos
                .Where(rt => personaIds.Contains(rt.PersonaId))
                .Select(rt => rt.TurnoId)
                .ToListAsync();

            var solicitudIds = await _db.Solicitudes
                .Where(s =>
                    personaIds.Contains(s.PersonaSolicitanteId) ||
                    (s.PersonaAprobador1Id != null && personaIds.Contains(s.PersonaAprobador1Id)) ||
                    (s.PersonaAprobador2Id != null && personaIds.Contains(s.PersonaAprobador2Id)))
                .Select(s => s.SolicitudId)
                .ToListAsync();

            var cambiosTurno = await _db.CambiosTurno
                .Where(c =>
                    solicitudIds.Contains(c.SolicitudId) ||
                    turnoIds.Contains(c.TurnoOrigenId) ||
                    turnoIds.Contains(c.TurnoDestinoId))
                .ToListAsync();
            _db.CambiosTurno.RemoveRange(cambiosTurno);

            var permisos = await _db.Permisos
                .Where(p =>
                    solicitudIds.Contains(p.SolicitudId) ||
                    turnoIds.Contains(p.RegistroTurnoId))
                .ToListAsync();
            _db.Permisos.RemoveRange(permisos);

            var vacaciones = await _db.Vacaciones
                .Where(v => solicitudIds.Contains(v.SolicitudId))
                .ToListAsync();
            _db.Vacaciones.RemoveRange(vacaciones);

            var solicitudes = await _db.Solicitudes
                .Where(s => solicitudIds.Contains(s.SolicitudId))
                .ToListAsync();
            _db.Solicitudes.RemoveRange(solicitudes);

            var personaGrupos = await _db.PersonaGrupos
                .Where(pg => personaIds.Contains(pg.PersonaId))
                .ToListAsync();
            _db.PersonaGrupos.RemoveRange(personaGrupos);

            var turnos = await _db.RegistroTurnos
                .Where(rt => personaIds.Contains(rt.PersonaId))
                .ToListAsync();
            _db.RegistroTurnos.RemoveRange(turnos);

            var personas = await _db.Personas
                .Where(p => personaIds.Contains(p.PersonaId))
                .ToListAsync();
            _db.Personas.RemoveRange(personas);

            var grupos = await _db.Grupos
                .Where(g => g.EquipoId == equipoId)
                .ToListAsync();
            _db.Grupos.RemoveRange(grupos);

            _db.Equipos.Remove(equipo);
            await _db.SaveChangesAsync();

            foreach (var userId in userIds)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    continue;
                }

                var userResult = await _userManager.DeleteAsync(user);
                if (!userResult.Succeeded)
                {
                    var errors = string.Join("; ", userResult.Errors.Select(e => e.Description));
                    await transaction.RollbackAsync();
                    _logger.LogError("DeleteAsync fallo al borrar usuario {UserId}: {Errors}", userId, errors);
                    return Result.Fail("No se pudo eliminar el usuario.");
                }
            }

            await transaction.CommitAsync();
            return Result.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "DeleteAsync fallo inesperadamente para EquipoId {EquipoId}.", equipoId);
            return Result.Fail("Eliminar equipo fallo debido a un error inesperado.");
        }
    }
}
