using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Services.Interfaces;

namespace TurneroTcs.Services;

public class GrupoService : IGrupoService
{
    private readonly ApplicationDbContext _db;

    private readonly ILogger<GrupoService> _logger;

    public GrupoService(ApplicationDbContext db, ILogger<GrupoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Grupo>> GetAllAsync()
    {
        try
        {
            var grupos = await _db.Grupos
                .Include(g => g.Equipo)
                .AsNoTracking()
                .OrderBy(g => g.NombreGrupo)
                .ToListAsync();

            _logger.LogDebug("Lista de {Count} grupos traidos exitosamente", grupos.Count);
            return grupos;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la lista de grupos");
            throw;
        }
    }

    public async Task<Result> CreateGrupoAsync(Grupo grupo)
    {
        if (grupo == null)
        {
            return Result.Fail("No se ha ingresado ningun grupo");
        }

        if (string.IsNullOrWhiteSpace(grupo.NombreGrupo))
        {
            return Result.Fail("No se ha ingresado nombre del grupo");
        }

        if (string.IsNullOrWhiteSpace(grupo.EquipoId))
        {
            return Result.Fail("No se ha ingresado ningun equipo");
        }

        var equipo = await _db.Equipos.FirstOrDefaultAsync(e => e.EquipoId == grupo.EquipoId);

        if (equipo == null)
        {
            return Result.Fail("El equipo seleccionado no existe");
        }

        if (!equipo.Activo)
        {
            return Result.Fail("El equipo seleccionado no esta activo");
        }

        var exists = await _db.Grupos
                            .AnyAsync(g => g.EquipoId == grupo.EquipoId && g.NombreGrupo == grupo.NombreGrupo);

        if (exists)
        {
            return Result.Fail("Ya existe un grupo con ese nombre en este equipo");
        }

        try
        {
            _db.Grupos.Add(grupo);
            await _db.SaveChangesAsync();
            _logger.LogInformation("El grupo {Grupo} ha sido creado satisfactoriamente", grupo.NombreGrupo);
            
            return Result.Ok();
        }
        catch (Exception ex)
        {

            _logger.LogError(ex, "Error al crear el grupo {Grupo}", grupo.NombreGrupo);
            return Result.Fail("Ha ocurrido un error al crear el grupo");
        }
    }

    public async Task<IReadOnlyList<Grupo>> GetByPersonaAsync(string personaId)
    {
        if (string.IsNullOrWhiteSpace(personaId))
        {
            return Array.Empty<Grupo>();
        }

        try
        {
            var grupos = await (
                from pg in _db.PersonaGrupos.AsNoTracking()
                join g in _db.Grupos.AsNoTracking() on pg.GrupoId equals g.GrupoId
                where pg.PersonaId == personaId && g.Activo
                select g)
                .OrderBy(g => g.NombreGrupo)
                .ToListAsync();

            return grupos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener grupos para persona {PersonaId}.", personaId);
            throw;
        }
    }

    public async Task<Result> PatchAsync(string grupoId, GrupoPatchRequest request, string currentUserRole)
    {
        if (string.IsNullOrWhiteSpace(currentUserRole))
        {
            return Result.Fail("No se ha podido identificar el rol del usuario.");
        }

        if (currentUserRole != "SuperAdmin" && currentUserRole != "Admin")
        {
            return Result.Fail("No tiene permiso para realizar esta accion.");
        }

        var grupo = await _db.Grupos.SingleOrDefaultAsync(g => g.GrupoId == grupoId);
        if (grupo == null)
        {
            return Result.Fail("Grupo no existe.");
        }

        if (request.NombreGrupo != null)
        {
            if (string.IsNullOrWhiteSpace(request.NombreGrupo))
            {
                return Result.Fail("El nombre del grupo es requerido.");
            }

            var exists = await _db.Grupos
                .AnyAsync(g => g.EquipoId == grupo.EquipoId && g.NombreGrupo == request.NombreGrupo && g.GrupoId != grupoId);
            if (exists)
            {
                return Result.Fail("Ya existe un grupo con ese nombre en este equipo.");
            }

            grupo.NombreGrupo = request.NombreGrupo;
        }

        if (request.EquipoId != null)
        {
            if (string.IsNullOrWhiteSpace(request.EquipoId))
            {
                return Result.Fail("El equipo es requerido.");
            }

            var equipo = await _db.Equipos.SingleOrDefaultAsync(e => e.EquipoId == request.EquipoId);
            if (equipo == null)
            {
                return Result.Fail("El equipo seleccionado no existe.");
            }

            if (!equipo.Activo)
            {
                return Result.Fail("El equipo seleccionado no esta activo.");
            }

            var exists = await _db.Grupos
                .AnyAsync(g => g.EquipoId == request.EquipoId && g.NombreGrupo == grupo.NombreGrupo && g.GrupoId != grupoId);
            if (exists)
            {
                return Result.Fail("Ya existe un grupo con ese nombre en este equipo.");
            }

            grupo.EquipoId = request.EquipoId;
        }

        if (request.Activo.HasValue)
        {
            grupo.Activo = request.Activo.Value;
        }

        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(string grupoId, string currentUserRole)
    {
        if (string.IsNullOrWhiteSpace(grupoId))
        {
            return Result.Fail("Grupo no especificado.");
        }

        if (string.IsNullOrWhiteSpace(currentUserRole))
        {
            return Result.Fail("No se ha podido identificar el rol del usuario.");
        }

        if (currentUserRole != "SuperAdmin" && currentUserRole != "Admin")
        {
            return Result.Fail("No tiene permiso para realizar esta accion.");
        }

        var grupo = await _db.Grupos.SingleOrDefaultAsync(g => g.GrupoId == grupoId);
        if (grupo == null)
        {
            return Result.Fail("Grupo no existe.");
        }

        if (!grupo.Activo)
        {
            return Result.Ok();
        }

        try
        {
            grupo.Activo = false;
            await _db.SaveChangesAsync();

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteAsync fallo inesperadamente para GrupoId {GrupoId}.", grupoId);
            return Result.Fail("Eliminar grupo fallo debido a un error inesperado.");
        }
    }
}
