using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TurneroTcs.Records;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.ViewModels;
namespace TurneroTcs.Services;

public class PersonaService : IPersonaService
{
    private sealed record PersonaManagementContext(Persona Persona, string? LiderEquipoId);

    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<PersonaService> _logger;

    public PersonaService(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<PersonaService> logger)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PersonaListViewModel>> GetAllAsync()
    {
        try
        {
            var personas = await (
                from p in _db.Personas.AsNoTracking()
                join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
                join ur in _db.UserRoles.AsNoTracking() on u.Id equals ur.UserId
                join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                join e in _db.Equipos.AsNoTracking() on p.EquipoId equals e.EquipoId
                select new PersonaListViewModel
                {
                    PersonaId = p.PersonaId,
                    UserId = p.UserId,
                    Nombre = p.Nombre,
                    SegundoNombre = p.SegundoNombre,
                    Apellido = p.Apellido,
                    SegundoApellido = p.SegundoApellido,
                    Ultimatix = p.Ultimatix,
                    ColorUsuario = p.ColorUsuario,
                    NombreRol = r.Name,
                    NombreEquipo = e.NombreEquipo,
                    EquipoId = e.EquipoId,
                    CreadoEn = p.CreadoEn,
                    Activo = !p.Borrado,
                    Borrado = p.Borrado,
                    BorradoPor = p.BorradoPor,
                    BorradoEn = p.BorradoEn
                })
                .OrderBy(p => p.Apellido)
                .ThenBy(p => p.Nombre)
                .ToListAsync();

            var personaIds = personas.Select(p => p.PersonaId).ToList();
            var borradoPorUserIds = personas
                .Where(p => p.Borrado && !string.IsNullOrWhiteSpace(p.BorradoPor))
                .Select(p => p.BorradoPor!)
                .Distinct()
                .ToList();

            if (borradoPorUserIds.Count > 0)
            {
                var borradoPorLookup = await _db.Personas
                    .AsNoTracking()
                    .Where(p => borradoPorUserIds.Contains(p.UserId))
                    .Select(p => new
                    {
                        p.UserId,
                        p.Nombre,
                        p.SegundoNombre,
                        p.Apellido,
                        p.SegundoApellido
                    })
                    .ToDictionaryAsync(
                        p => p.UserId,
                        p => BuildPersonaName(p.Nombre, p.SegundoNombre, p.Apellido, p.SegundoApellido));

                foreach (var persona in personas.Where(p => p.Borrado && !string.IsNullOrWhiteSpace(p.BorradoPor)))
                {
                    if (borradoPorLookup.TryGetValue(persona.BorradoPor!, out var borradoPorNombre))
                    {
                        persona.BorradoPor = borradoPorNombre;
                    }
                }
            }

            if (personaIds.Count > 0)
            {
                var grupos = await (
                    from pg in _db.PersonaGrupos.AsNoTracking()
                    join g in _db.Grupos.AsNoTracking() on pg.GrupoId equals g.GrupoId
                    where personaIds.Contains(pg.PersonaId)
                    select new { pg.PersonaId, pg.GrupoId, pg.EsPrincipal, g.NombreGrupo })
                    .ToListAsync();

                var gruposLookup = grupos
                    .GroupBy(x => x.PersonaId)
                    .ToDictionary(
                        g => g.Key,
                        g => new
                        {
                            PrimariosNombres = string.Join(", ", g.Where(x => x.EsPrincipal)
                                .Select(x => x.NombreGrupo)
                                .OrderBy(n => n)),
                            PrimariosIds = g.Where(x => x.EsPrincipal)
                                .Select(x => x.GrupoId)
                                .Distinct()
                                .ToList(),
                            SecundariosNombres = string.Join(", ", g.Where(x => !x.EsPrincipal)
                                .Select(x => x.NombreGrupo)
                                .OrderBy(n => n)),
                            SecundariosIds = g.Where(x => !x.EsPrincipal)
                                .Select(x => x.GrupoId)
                                .Distinct()
                                .ToList()
                        });

                foreach (var persona in personas)
                {
                    if (gruposLookup.TryGetValue(persona.PersonaId, out var data))
                    {
                        persona.GruposNombres = string.IsNullOrWhiteSpace(data.PrimariosNombres) ? null : data.PrimariosNombres;
                        persona.GrupoIds = data.PrimariosIds;
                        persona.GruposSecundariosNombres = string.IsNullOrWhiteSpace(data.SecundariosNombres) ? null : data.SecundariosNombres;
                        persona.GrupoIdsSecundarios = data.SecundariosIds;
                    }
                }
            }

            _logger.LogDebug("Lista de {Count} personas traidas exitosamente.", personas.Count);
            return personas;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la lista de personas.");
            throw;
        }
    }

    private static string BuildPersonaName(string nombre, string? segundoNombre, string apellido, string? segundoApellido)
    {
        return string.Join(" ", new[] { nombre, segundoNombre, apellido, segundoApellido }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static bool CanManagePersonasByRole(string currentUserRole) =>
        currentUserRole == "SuperAdmin" || currentUserRole == "Admin" || currentUserRole == "Lider";

    private async Task<Result<PersonaManagementContext>> GetPersonaManagementContextAsync(
        string personaId,
        string currentUserRole,
        string currentUserId)
    {
        if (string.IsNullOrWhiteSpace(currentUserRole))
        {
            return Result<PersonaManagementContext>.Fail("No se ha podido identificar el rol del usuario.");
        }

        if (!CanManagePersonasByRole(currentUserRole))
        {
            return Result<PersonaManagementContext>.Fail("No tiene permiso para realizar esta accion.");
        }

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Result<PersonaManagementContext>.Fail("No se ha podido identificar el usuario autenticado.");
        }

        if (string.IsNullOrWhiteSpace(personaId))
        {
            return Result<PersonaManagementContext>.Fail("Usuario invalido.");
        }

        var persona = await _db.Personas.SingleOrDefaultAsync(p => p.PersonaId == personaId);
        if (persona == null)
        {
            return Result<PersonaManagementContext>.Fail("Usuario no existe.");
        }

        string? liderEquipoId = null;
        if (currentUserRole == "Lider")
        {
            liderEquipoId = await _db.Personas
                .AsNoTracking()
                .Where(p => p.UserId == currentUserId && !p.Borrado)
                .Select(p => p.EquipoId)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(liderEquipoId))
            {
                return Result<PersonaManagementContext>.Fail("Lider no tiene equipo asignado.");
            }

            if (!string.Equals(persona.EquipoId, liderEquipoId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Lider {UserId} intento administrar PersonaId {PersonaId} fuera de su equipo.",
                    currentUserId,
                    personaId);
                return Result<PersonaManagementContext>.Fail("No tiene permiso para administrar este usuario.");
            }
        }

        return Result<PersonaManagementContext>.Ok(new PersonaManagementContext(persona, liderEquipoId));
    }

    public async Task<Result> PatchAsync(string personaId,
                                        PersonaPatchRequest request,
                                        string currentUserRole,
                                        string currentUserId){

        var contextResult = await GetPersonaManagementContextAsync(personaId, currentUserRole, currentUserId);
        if (!contextResult.Succeeded || contextResult.Value == null)
        {
            return Result.Fail(contextResult.Error ?? "No tiene permiso para administrar este usuario.");
        }

        var persona = contextResult.Value.Persona;
        var liderEquipoId = contextResult.Value.LiderEquipoId;

        if (persona.Borrado)
        {
            return Result.Fail("Usuario no esta disponible.");
        }

        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var originalUltimatix = persona.Ultimatix;
            string? updatedUltimatix = null;

            if (currentUserRole == "Lider" &&
                request.EquipoId != null &&
                !string.IsNullOrWhiteSpace(request.EquipoId) &&
                !string.Equals(request.EquipoId, liderEquipoId, StringComparison.Ordinal))
            {
                return Result.Fail("No tiene permiso para mover usuarios fuera de su equipo.");
            }

            if (request.Nombre != null){
                if(string.IsNullOrWhiteSpace(request.Nombre)){
                    return Result.Fail("El nombre es requerido");
                }
                persona.Nombre = request.Nombre;
            }

            if (request.SegundoNombre != null){
                persona.SegundoNombre = request.SegundoNombre;
            }

            if (request.Ultimatix != null){
                if(string.IsNullOrWhiteSpace(request.Ultimatix)){
                    return Result.Fail("Numero Ultimatix es requerido");
                }
                updatedUltimatix = request.Ultimatix.Trim();
                persona.Ultimatix = updatedUltimatix;
            }

            if (request.Apellido != null){
                if(string.IsNullOrWhiteSpace(request.Apellido)){
                    return Result.Fail("El apellido es requerido");
                }
                persona.Apellido = request.Apellido;
            }

            if (request.SegundoApellido != null){
                persona.SegundoApellido = request.SegundoApellido;
            }

            if (request.ColorUsuario != null){
                persona.ColorUsuario = request.ColorUsuario;
            }

            if (request.EquipoId != null){
                if (string.IsNullOrWhiteSpace(request.EquipoId))
                {
                    return Result.Fail("El equipo es requerido.");
                }

                var equipoExists = await _db.Equipos.AnyAsync(e => e.EquipoId == request.EquipoId);
                if (!equipoExists)
                {
                    return Result.Fail("Equipo no existe.");
                }

                persona.EquipoId = request.EquipoId;
            }

            if (request.GrupoIds != null || request.GrupoIdsSecundarios != null)
            {
                var targetEquipo = persona.EquipoId;
                if (string.IsNullOrWhiteSpace(targetEquipo))
                {
                    return Result.Fail("El equipo es requerido para asignar grupos.");
                }

                var idsToValidate = new HashSet<string>();
                if (request.GrupoIds != null)
                {
                    foreach (var id in request.GrupoIds)
                    {
                        idsToValidate.Add(id);
                    }
                }

                if (request.GrupoIdsSecundarios != null)
                {
                    foreach (var id in request.GrupoIdsSecundarios)
                    {
                        idsToValidate.Add(id);
                    }
                }

                if (idsToValidate.Count > 0)
                {
                    var grupos = await _db.Grupos
                        .Where(g => idsToValidate.Contains(g.GrupoId))
                        .ToListAsync();

                    if (grupos.Count != idsToValidate.Count)
                    {
                        return Result.Fail("Uno o mas grupos no existen.");
                    }

                    if (grupos.Any(g => g.EquipoId != targetEquipo))
                    {
                        return Result.Fail("Todos los grupos deben pertenecer al mismo equipo.");
                    }
                }

                var currentLinks = await _db.PersonaGrupos
                    .Where(pg => pg.PersonaId == persona.PersonaId)
                    .ToListAsync();

                var primaryIdsBefore = currentLinks
                    .Where(pg => pg.EsPrincipal)
                    .Select(pg => pg.GrupoId)
                    .ToHashSet();

                var primaryIdsAfter = request.GrupoIds != null
                    ? new HashSet<string>(request.GrupoIds)
                    : primaryIdsBefore;

                if (request.GrupoIds != null)
                {
                    var primaryLinks = currentLinks.Where(pg => pg.EsPrincipal).ToList();
                    if (primaryLinks.Count > 0)
                    {
                        _db.PersonaGrupos.RemoveRange(primaryLinks);
                    }

                    var secondaryToRemove = currentLinks
                        .Where(pg => !pg.EsPrincipal && primaryIdsAfter.Contains(pg.GrupoId))
                        .ToList();
                    if (secondaryToRemove.Count > 0)
                    {
                        _db.PersonaGrupos.RemoveRange(secondaryToRemove);
                    }

                    var newLinks = primaryIdsAfter.Select(grupoId => new PersonaGrupo
                    {
                        PersonaId = persona.PersonaId,
                        GrupoId = grupoId,
                        EsPrincipal = true
                    }).ToList();

                    if (newLinks.Count > 0)
                    {
                        _db.PersonaGrupos.AddRange(newLinks);
                    }
                }

                if (request.GrupoIdsSecundarios != null)
                {
                    var secondaryIdsAfter = new HashSet<string>(request.GrupoIdsSecundarios);
                    secondaryIdsAfter.ExceptWith(primaryIdsAfter);

                    var existingSecondary = currentLinks.Where(pg => !pg.EsPrincipal).ToList();
                    var secondaryToRemove = existingSecondary
                        .Where(pg => !secondaryIdsAfter.Contains(pg.GrupoId) || primaryIdsAfter.Contains(pg.GrupoId))
                        .ToList();
                    if (secondaryToRemove.Count > 0)
                    {
                        _db.PersonaGrupos.RemoveRange(secondaryToRemove);
                    }

                    var existingSecondaryIds = existingSecondary
                        .Where(pg => secondaryIdsAfter.Contains(pg.GrupoId))
                        .Select(pg => pg.GrupoId)
                        .ToHashSet();

                    var secondaryToAdd = secondaryIdsAfter
                        .Where(grupoId => !existingSecondaryIds.Contains(grupoId))
                        .Select(grupoId => new PersonaGrupo
                        {
                            PersonaId = persona.PersonaId,
                            GrupoId = grupoId,
                            EsPrincipal = false
                        })
                        .ToList();

                    if (secondaryToAdd.Count > 0)
                    {
                        _db.PersonaGrupos.AddRange(secondaryToAdd);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(updatedUltimatix)
                && !string.Equals(originalUltimatix, updatedUltimatix, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(persona.UserId))
                {
                    await transaction.RollbackAsync();
                    return Result.Fail("El usuario autenticado vinculado a la persona no existe.");
                }

                var user = await _userManager.FindByIdAsync(persona.UserId);
                if (user == null)
                {
                    await transaction.RollbackAsync();
                    return Result.Fail("El usuario autenticado vinculado a la persona no existe.");
                }

                user.UserName = updatedUltimatix;
                user.NormalizedUserName = _userManager.NormalizeName(updatedUltimatix);

                var updateUserResult = await _userManager.UpdateAsync(user);
                if (!updateUserResult.Succeeded)
                {
                    var errors = string.Join("; ", updateUserResult.Errors.Select(e => e.Description));
                    await transaction.RollbackAsync();
                    _logger.LogError("PatchAsync fallo al actualizar credenciales para PersonaId {PersonaId}: {Errors}", personaId, errors);
                    return Result.Fail($"No se pudo actualizar el usuario autenticado: {errors}");
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Result.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "PatchAsync fallo inesperadamente para PersonaId {PersonaId}.", personaId);
            return Result.Fail("Actualizar usuario fallo debido a un error inesperado.");
        }
    }

    public async Task<Result> CreateAsync(Persona persona, 
                                        string rawPassword, 
                                        string roleName, 
                                        string? equipoId, 
                                        string currentUserRole,
                                        string currentUserId,
                                        IReadOnlyCollection<string> grupoIds)
    {
        if (string.IsNullOrEmpty(currentUserRole))
        {
            return Result.Fail("No tiene rol asignado.");
        }

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Result.Fail("Usuario actual no identificado.");
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        IdentityUser? user = null;

        try
        {
            user = new IdentityUser
            {
                UserName = persona.Ultimatix
            };

            IReadOnlyCollection<string> allowedRoles = currentUserRole switch
            {
                "SuperAdmin" => new[] { "SuperAdmin", "Admin", "Lider", "Usuario" },
                "Admin"      => new[] { "Lider", "Usuario" },
                "Lider"      => new[] { "Usuario" },
                _            => Array.Empty<string>()
            };

            if (!allowedRoles.Contains(roleName))
            {
                _logger.LogWarning("{RoleName} no tiene permiso para asignar el rol {Rol}", currentUserRole ,roleName );
                return Result.Fail("No tiene permisos para asignar ese rol.");
            }

            var roleExists = await _roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                _logger.LogWarning("CreateAsync fallo: rol {Role} no existe.", roleName);
                return Result.Fail("Rol no existe.");
            }

            if (currentUserRole == "Lider")
            {
                var liderPersona = await _db.Personas
                    .AsNoTracking()
                    .SingleOrDefaultAsync(p => p.UserId == currentUserId && !p.Borrado);

                if (liderPersona == null || string.IsNullOrWhiteSpace(liderPersona.EquipoId))
                {
                    return Result.Fail("Lider no tiene equipo asignado.");
                }

                equipoId = liderPersona.EquipoId;
            }

            if (!string.IsNullOrEmpty(equipoId))
            {
                var equipoExists = await _db.Equipos.AnyAsync(e => e.EquipoId == equipoId);

                if (!equipoExists)
                {
                    _logger.LogWarning("CreateAsync fallo: equipo {Equipo} no existe.", equipoId);
                    return Result.Fail("Equipo no existe.");
                }
            }

            var createUserResult = await _userManager.CreateAsync(user, rawPassword);
            if (!createUserResult.Succeeded)
            {
                var errors = string.Join("; ", createUserResult.Errors.Select(e => e.Description));
                _logger.LogError("CreateAsync fallo: error al crear el usuario.");
                return Result.Fail($"Error al crear usuario: {errors}");
            }

            persona.UserId = user.Id;
            persona.EquipoId = equipoId;

            var addToRoleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!addToRoleResult.Succeeded)
            {
                var errors = string.Join("; ", addToRoleResult.Errors.Select(e => e.Description));
                _logger.LogError("CreateAsync fallo: error al asignar el rol.");
                return Result.Fail($"Error al crear usuario: {errors}");
            }

            _db.Personas.Add(persona);
            await _db.SaveChangesAsync();

            if (grupoIds.Count > 0)
            {
                var grupos = await _db.Grupos
                    .Where(g => grupoIds.Contains(g.GrupoId))
                    .ToListAsync();

                if (grupos.Count != grupoIds.Count)
                {
                    return Result.Fail("Uno o mas grupos no existen.");
                }

                if (grupos.Any(g => g.EquipoId != equipoId))
                {
                    return Result.Fail("Todos los grupos deben pertenecer al mismo equipo de la persona.");
                }

                var personaGrupos = grupos.Select(g => new PersonaGrupo
                {
                    PersonaId = persona.PersonaId,
                    GrupoId = g.GrupoId,
                    EsPrincipal = true
                }).ToList();

                _db.PersonaGrupos.AddRange(personaGrupos);
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return Result.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
            }

            _logger.LogError(ex, "CreateAsync fallo inesperadamente para Ultimatix {Ultimatix}.", persona.Ultimatix);
            return Result.Fail("Crear usuario fallo debido a un error inesperado.");
        }
    }

    public async Task<Result<PersonaDeleteImpactSummary>> GetDeleteImpactAsync(string personaId, string currentUserRole, string currentUserId)
    {
        var contextResult = await GetPersonaManagementContextAsync(personaId, currentUserRole, currentUserId);
        if (!contextResult.Succeeded || contextResult.Value == null)
        {
            return Result<PersonaDeleteImpactSummary>.Fail(contextResult.Error ?? "No tiene permiso para administrar este usuario.");
        }

        var persona = contextResult.Value.Persona;

        if (persona.Borrado)
        {
            return Result<PersonaDeleteImpactSummary>.Fail("Usuario ya fue borrado.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var fechas = await _db.RegistroTurnos
            .AsNoTracking()
            .Where(rt => rt.PersonaId == personaId && rt.FechaTurno >= today)
            .Select(rt => new { rt.TurnoId, rt.FechaTurno })
            .ToListAsync();

        var futureTurnoIds = fechas
            .Select(rt => rt.TurnoId)
            .ToList();

        var hasLinkedFutureTurnos = false;
        if (futureTurnoIds.Count > 0)
        {
            var hasPermiso = await (
                from permiso in _db.Permisos.AsNoTracking()
                join solicitud in _db.Solicitudes.AsNoTracking() on permiso.SolicitudId equals solicitud.SolicitudId
                where futureTurnoIds.Contains(permiso.RegistroTurnoId)
                    && solicitud.EstadoSolicitud != SolicitudEstado.Rechazado
                    && solicitud.EstadoSolicitud != SolicitudEstado.Cancelado
                select permiso.PermisoId)
                .AnyAsync();

            var hasCambio = await (
                from cambio in _db.CambiosTurno.AsNoTracking()
                join solicitud in _db.Solicitudes.AsNoTracking() on cambio.SolicitudId equals solicitud.SolicitudId
                where (futureTurnoIds.Contains(cambio.TurnoOrigenId) || futureTurnoIds.Contains(cambio.TurnoDestinoId))
                    && solicitud.EstadoSolicitud != SolicitudEstado.Rechazado
                    && solicitud.EstadoSolicitud != SolicitudEstado.Cancelado
                select cambio.CambioTurnoId)
                .AnyAsync();

            var hasCalamidad = await (
                from reemplazo in _db.CalamidadReemplazos.AsNoTracking()
                join solicitud in _db.Solicitudes.AsNoTracking() on reemplazo.SolicitudId equals solicitud.SolicitudId
                where (futureTurnoIds.Contains(reemplazo.TurnoAusenteId) || futureTurnoIds.Contains(reemplazo.TurnoReemplazoId))
                    && solicitud.EstadoSolicitud != SolicitudEstado.Rechazado
                    && solicitud.EstadoSolicitud != SolicitudEstado.Cancelado
                select reemplazo.CalamidadReemplazoId)
                .AnyAsync();

            hasLinkedFutureTurnos = hasPermiso || hasCambio || hasCalamidad;
        }

        var summary = new PersonaDeleteImpactSummary(
            fechas.Count,
            fechas.Count == 0 ? null : fechas.Min(rt => rt.FechaTurno),
            fechas.Count == 0 ? null : fechas.Max(rt => rt.FechaTurno),
            hasLinkedFutureTurnos);

        return Result<PersonaDeleteImpactSummary>.Ok(summary);
    }

    public async Task<Result> DeleteAsync(string personaId, string currentUserRole, string currentUserId)
    {
        var contextResult = await GetPersonaManagementContextAsync(personaId, currentUserRole, currentUserId);
        if (!contextResult.Succeeded || contextResult.Value == null)
        {
            return Result.Fail(contextResult.Error ?? "No tiene permiso para administrar este usuario.");
        }

        var persona = contextResult.Value.Persona;

        if (persona.Borrado)
        {
            return Result.Fail("Usuario ya fue borrado.");
        }

        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var futureTurnos = await _db.RegistroTurnos
                .Where(rt => rt.PersonaId == personaId && rt.FechaTurno >= today)
                .ToListAsync();

            if (futureTurnos.Count > 0)
            {
                var futureTurnoIds = futureTurnos
                    .Select(rt => rt.TurnoId)
                    .ToList();

                var hasPermiso = await (
                    from permiso in _db.Permisos.AsNoTracking()
                    join solicitud in _db.Solicitudes.AsNoTracking() on permiso.SolicitudId equals solicitud.SolicitudId
                    where futureTurnoIds.Contains(permiso.RegistroTurnoId)
                        && solicitud.EstadoSolicitud != SolicitudEstado.Rechazado
                        && solicitud.EstadoSolicitud != SolicitudEstado.Cancelado
                    select permiso.PermisoId)
                    .AnyAsync();

                var hasCambio = await (
                    from cambio in _db.CambiosTurno.AsNoTracking()
                    join solicitud in _db.Solicitudes.AsNoTracking() on cambio.SolicitudId equals solicitud.SolicitudId
                    where (futureTurnoIds.Contains(cambio.TurnoOrigenId) || futureTurnoIds.Contains(cambio.TurnoDestinoId))
                        && solicitud.EstadoSolicitud != SolicitudEstado.Rechazado
                        && solicitud.EstadoSolicitud != SolicitudEstado.Cancelado
                    select cambio.CambioTurnoId)
                    .AnyAsync();

                var hasCalamidad = await (
                    from reemplazo in _db.CalamidadReemplazos.AsNoTracking()
                    join solicitud in _db.Solicitudes.AsNoTracking() on reemplazo.SolicitudId equals solicitud.SolicitudId
                    where (futureTurnoIds.Contains(reemplazo.TurnoAusenteId) || futureTurnoIds.Contains(reemplazo.TurnoReemplazoId))
                        && solicitud.EstadoSolicitud != SolicitudEstado.Rechazado
                        && solicitud.EstadoSolicitud != SolicitudEstado.Cancelado
                    select reemplazo.CalamidadReemplazoId)
                    .AnyAsync();

                if (hasPermiso || hasCambio || hasCalamidad)
                {
                    await transaction.RollbackAsync();
                    return Result.Fail("La persona tiene turnos futuros vinculados a solicitudes o reemplazos. Debes resolverlos antes de borrarla.");
                }
            }

            var now = DateTime.UtcNow;
            persona.Borrado = true;
            persona.BorradoPor = currentUserId;
            persona.BorradoEn = now;
            persona.ActualizadoEn = now;

            if (futureTurnos.Count > 0)
            {
                _db.RegistroTurnos.RemoveRange(futureTurnos);
            }

            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(persona.UserId))
            {
                var user = await _userManager.FindByIdAsync(persona.UserId);
                if (user != null)
                {
                    if (!user.LockoutEnabled)
                    {
                        var enableResult = await _userManager.SetLockoutEnabledAsync(user, true);
                        if (!enableResult.Succeeded)
                        {
                            var errors = string.Join("; ", enableResult.Errors.Select(e => e.Description));
                            await transaction.RollbackAsync();
                            _logger.LogError("DeleteAsync fallo al habilitar lockout para usuario {UserId}: {Errors}", persona.UserId, errors);
                            return Result.Fail("No se pudo deshabilitar el acceso del usuario.");
                        }
                    }

                    var lockoutResult = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                    if (!lockoutResult.Succeeded)
                    {
                        var errors = string.Join("; ", lockoutResult.Errors.Select(e => e.Description));
                        await transaction.RollbackAsync();
                        _logger.LogError("DeleteAsync fallo al bloquear usuario {UserId}: {Errors}", persona.UserId, errors);
                        return Result.Fail("No se pudo deshabilitar el acceso del usuario.");
                    }
                }
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Persona {PersonaId} borrada logicamente. Turnos futuros eliminados: {TurnosEliminados}.", personaId, futureTurnos.Count);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "DeleteAsync fallo inesperadamente para PersonaId {PersonaId}.", personaId);
            return Result.Fail("Eliminar usuario fallo debido a un error inesperado.");
        }
    }

    public async Task<Result> RestoreAsync(string personaId, string currentUserRole, string currentUserId)
    {
        var contextResult = await GetPersonaManagementContextAsync(personaId, currentUserRole, currentUserId);
        if (!contextResult.Succeeded || contextResult.Value == null)
        {
            return Result.Fail(contextResult.Error ?? "No tiene permiso para administrar este usuario.");
        }

        var persona = contextResult.Value.Persona;

        if (!persona.Borrado)
        {
            return Result.Fail("Usuario no esta borrado.");
        }

        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            persona.Borrado = false;
            persona.ActualizadoEn = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(persona.UserId))
            {
                var user = await _userManager.FindByIdAsync(persona.UserId);
                if (user != null)
                {
                    var unlockResult = await _userManager.SetLockoutEndDateAsync(user, null);
                    if (!unlockResult.Succeeded)
                    {
                        var errors = string.Join("; ", unlockResult.Errors.Select(e => e.Description));
                        await transaction.RollbackAsync();
                        _logger.LogError("RestoreAsync fallo al desbloquear usuario {UserId}: {Errors}", persona.UserId, errors);
                        return Result.Fail("No se pudo restaurar el acceso del usuario.");
                    }
                }
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Persona {PersonaId} restaurada.", personaId);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "RestoreAsync fallo inesperadamente para PersonaId {PersonaId}.", personaId);
            return Result.Fail("Recuperar usuario fallo debido a un error inesperado.");
        }
    }
}
