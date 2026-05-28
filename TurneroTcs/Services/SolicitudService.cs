using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Security;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Services;

public class SolicitudService : ISolicitudService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SolicitudService> _logger;
    private readonly IPermisoAccesoResolver _permisoAccesoResolver;

    public SolicitudService(
        ApplicationDbContext db,
        ILogger<SolicitudService> logger,
        IPermisoAccesoResolver permisoAccesoResolver)
    {
        _db = db;
        _logger = logger;
        _permisoAccesoResolver = permisoAccesoResolver;
    }

    public async Task<Result> CreateAsync(
        string solicitanteId,
        string creatorRole,
        SolicitudCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(solicitanteId))
        {
            return Result.Fail("Solicitante es requerido.");
        }

        if (request == null)
        {
            return Result.Fail("Solicitud invalida.");
        }

        if (string.IsNullOrWhiteSpace(request.TipoSolicitudId))
        {
            return Result.Fail("Tipo de solicitud es requerido.");
        }

        var payloadCount = (request.Vacacion is not null ? 1 : 0)
            + (request.Permiso is not null ? 1 : 0)
            + (request.CambioTurno is not null ? 1 : 0);

        if (payloadCount != 1)
        {
            return Result.Fail("Debe seleccionar un tipo de solicitud.");
        }

        var solicitante = await _db.Personas
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.PersonaId == solicitanteId && !p.Borrado);

        if (solicitante == null)
        {
            return Result.Fail("Solicitante no existe o fue borrado.");
        }

        var tipoSolicitudExists = await _db.TipoSolicitudes
            .AsNoTracking()
            .AnyAsync(t => t.TipoSolicitudId == request.TipoSolicitudId);

        if (!tipoSolicitudExists)
        {
            return Result.Fail("Tipo de solicitud no existe.");
        }

        var now = DateTime.UtcNow;
        var solicitud = new Solicitud
        {
            SolicitudId = Guid.NewGuid().ToString("N"),
            PersonaSolicitanteId = solicitanteId,
            TipoSolicitudId = request.TipoSolicitudId,
            FechaSolicitud = now,
            CreadoEn = now,
            ActualizadoEn = now
        };

        if (creatorRole == "SuperAdmin" || creatorRole == "Admin")
        {
            solicitud.EstadoSolicitud = SolicitudEstado.AprobadoFinal;
            solicitud.PersonaAprobador2Id = solicitanteId;
            solicitud.FechaAprobacion2 = now;
        }
        else if (creatorRole == "Lider")
        {
            solicitud.EstadoSolicitud = SolicitudEstado.AprobadoLider;
            solicitud.PersonaAprobador1Id = solicitanteId;
            solicitud.FechaAprobacion1 = now;
        }
        else
        {
            solicitud.EstadoSolicitud = SolicitudEstado.Pendiente;
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Solicitudes.Add(solicitud);

            if (request.Vacacion is not null)
            {
                if (request.Vacacion.FechaFin < request.Vacacion.FechaInicio)
                {
                    return Result.Fail("La fecha fin no puede ser menor que la fecha inicio.");
                }

                var vacacion = new Vacacion
                {
                    VacacionId = Guid.NewGuid().ToString("N"),
                    SolicitudId = solicitud.SolicitudId,
                    FechaInicio = request.Vacacion.FechaInicio,
                    FechaFin = request.Vacacion.FechaFin
                };
                _db.Vacaciones.Add(vacacion);
            }

            if (request.Permiso is not null)
            {
                if (string.IsNullOrWhiteSpace(request.Permiso.RegistroTurnoId))
                {
                    return Result.Fail("Registro de turno es requerido.");
                }

                if (request.Permiso.HoraFin <= request.Permiso.HoraInicio)
                {
                    return Result.Fail("La hora fin debe ser mayor que la hora inicio.");
                }

                var registro = await _db.RegistroTurnos
                    .AsNoTracking()
                    .AnyAsync(rt => rt.TurnoId == request.Permiso.RegistroTurnoId);

                if (!registro)
                {
                    return Result.Fail("Registro de turno no existe.");
                }

                var permiso = new Permiso
                {
                    PermisoId = Guid.NewGuid().ToString("N"),
                    SolicitudId = solicitud.SolicitudId,
                    RegistroTurnoId = request.Permiso.RegistroTurnoId,
                    HoraInicio = request.Permiso.HoraInicio,
                    HoraFin = request.Permiso.HoraFin,
                    Motivo = request.Permiso.Motivo ?? string.Empty
                };
                _db.Permisos.Add(permiso);
            }

            if (request.CambioTurno is not null)
            {
                if (string.IsNullOrWhiteSpace(request.CambioTurno.TurnoOrigenId) ||
                    string.IsNullOrWhiteSpace(request.CambioTurno.TurnoDestinoId))
                {
                    return Result.Fail("Debe seleccionar los turnos a intercambiar.");
                }

                if (request.CambioTurno.TurnoOrigenId == request.CambioTurno.TurnoDestinoId)
                {
                    return Result.Fail("El turno origen y destino no pueden ser iguales.");
                }

                var turnoIds = new[] { request.CambioTurno.TurnoOrigenId, request.CambioTurno.TurnoDestinoId }
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList();

                var persistedTurnoIds = await _db.RegistroTurnos
                    .AsNoTracking()
                    .Where(rt => turnoIds.Contains(rt.TurnoId))
                    .Select(rt => rt.TurnoId)
                    .ToListAsync();

                var trackedAddedTurnoIds = _db.ChangeTracker
                    .Entries<RegistroTurno>()
                    .Where(entry =>
                        entry.State == EntityState.Added &&
                        !string.IsNullOrWhiteSpace(entry.Entity.TurnoId))
                    .Select(entry => entry.Entity.TurnoId)
                    .Where(id => turnoIds.Contains(id))
                    .ToList();

                var turnosExistCount = persistedTurnoIds
                    .Concat(trackedAddedTurnoIds)
                    .Distinct()
                    .Count();

                if (turnosExistCount != turnoIds.Count)
                {
                    return Result.Fail("Los turnos seleccionados no existen.");
                }

                var cambio = new CambioTurno
                {
                    CambioTurnoId = Guid.NewGuid().ToString("N"),
                    SolicitudId = solicitud.SolicitudId,
                    TurnoOrigenId = request.CambioTurno.TurnoOrigenId,
                    TurnoDestinoId = request.CambioTurno.TurnoDestinoId,
                    Motivo = request.CambioTurno.Motivo ?? string.Empty
                };
                _db.CambiosTurno.Add(cambio);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogDebug("Solicitud {SolicitudId} creada para solicitante {PersonaId}.", solicitud.SolicitudId, solicitanteId);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al crear solicitud para {PersonaId}.", solicitanteId);
            return Result.Fail("Ha ocurrido un error al crear la solicitud.");
        }
    }

    public async Task<Result> ApproveAsync(string solicitudId, string approverId, string approverRole, string approverUserId)
    {
        if (string.IsNullOrWhiteSpace(solicitudId))
        {
            return Result.Fail("Solicitud es requerida.");
        }

        if (string.IsNullOrWhiteSpace(approverId))
        {
            return Result.Fail("Aprobador es requerido.");
        }

        if (string.IsNullOrWhiteSpace(approverUserId))
        {
            return Result.Fail("Usuario aprobador es requerido.");
        }

        var solicitud = await _db.Solicitudes
            .SingleOrDefaultAsync(s => s.SolicitudId == solicitudId);

        if (solicitud == null)
        {
            return Result.Fail("Solicitud no existe.");
        }

        var permisosDecision = await ResolveDecisionPermissionCodesAsync(solicitud);
        if (string.IsNullOrWhiteSpace(permisosDecision.Aprobar))
        {
            return Result.Fail("No se pudo determinar el tipo de solicitud para aprobacion.");
        }

        var puedeAprobar = await _permisoAccesoResolver.TienePermisoAsync(approverUserId, permisosDecision.Aprobar);
        if (!puedeAprobar)
        {
            return Result.Fail("No tienes permiso para aprobar este tipo de solicitud.");
        }

        var role = approverRole?.Trim() ?? "Usuario";
        var isAdminApproval =
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        var isLiderApproval = string.Equals(role, "Lider", StringComparison.OrdinalIgnoreCase);
        if (solicitud.EstadoSolicitud == SolicitudEstado.Rechazado ||
            solicitud.EstadoSolicitud == SolicitudEstado.Cancelado ||
            solicitud.EstadoSolicitud == SolicitudEstado.AprobadoFinal)
        {
            return Result.Fail("La solicitud ya fue resuelta.");
        }

        var now = DateTime.UtcNow;
        if (solicitud.EstadoSolicitud == SolicitudEstado.Pendiente)
        {
            if (isAdminApproval)
            {
                solicitud.EstadoSolicitud = SolicitudEstado.AprobadoFinal;
                solicitud.PersonaAprobador1Id = approverId;
                solicitud.FechaAprobacion1 = now;
                solicitud.PersonaAprobador2Id = approverId;
                solicitud.FechaAprobacion2 = now;
            }
            else if (isLiderApproval)
            {
                solicitud.EstadoSolicitud = SolicitudEstado.AprobadoLider;
                solicitud.PersonaAprobador1Id = approverId;
                solicitud.FechaAprobacion1 = now;
            }
            else
            {
                return Result.Fail("No tienes permisos para aprobar.");
            }
        }
        else if (solicitud.EstadoSolicitud == SolicitudEstado.AprobadoLider)
        {
            if (!isAdminApproval)
            {
                return Result.Fail("Solo Admin o SuperAdmin pueden completar la aprobacion final.");
            }

            solicitud.EstadoSolicitud = SolicitudEstado.AprobadoFinal;
            solicitud.PersonaAprobador2Id = approverId;
            solicitud.FechaAprobacion2 = now;
        }
        else
        {
            return Result.Fail("No tienes permisos para aprobar.");
        }

        solicitud.ActualizadoEn = now;
        await _db.SaveChangesAsync();

        return Result.Ok();
    }

    public async Task<Result> RejectAsync(string solicitudId, string approverId, string approverRole, string approverUserId)
    {
        if (string.IsNullOrWhiteSpace(solicitudId))
        {
            return Result.Fail("Solicitud es requerida.");
        }

        if (string.IsNullOrWhiteSpace(approverId))
        {
            return Result.Fail("Aprobador es requerido.");
        }

        if (string.IsNullOrWhiteSpace(approverUserId))
        {
            return Result.Fail("Usuario aprobador es requerido.");
        }

        var solicitud = await _db.Solicitudes
            .SingleOrDefaultAsync(s => s.SolicitudId == solicitudId);

        if (solicitud == null)
        {
            return Result.Fail("Solicitud no existe.");
        }

        var permisosDecision = await ResolveDecisionPermissionCodesAsync(solicitud);
        if (string.IsNullOrWhiteSpace(permisosDecision.Rechazar))
        {
            return Result.Fail("No se pudo determinar el tipo de solicitud para rechazo.");
        }

        var puedeRechazar = await _permisoAccesoResolver.TienePermisoAsync(approverUserId, permisosDecision.Rechazar);
        if (!puedeRechazar)
        {
            return Result.Fail("No tienes permiso para rechazar este tipo de solicitud.");
        }

        var role = approverRole?.Trim() ?? "Usuario";
        if (solicitud.EstadoSolicitud == SolicitudEstado.Rechazado ||
            solicitud.EstadoSolicitud == SolicitudEstado.Cancelado ||
            solicitud.EstadoSolicitud == SolicitudEstado.AprobadoFinal)
        {
            return Result.Fail("La solicitud ya fue resuelta.");
        }

        var now = DateTime.UtcNow;
        if (solicitud.EstadoSolicitud == SolicitudEstado.Pendiente)
        {
            solicitud.PersonaAprobador1Id = approverId;
            solicitud.FechaAprobacion1 = now;
            solicitud.EstadoSolicitud = SolicitudEstado.Rechazado;
        }
        else if (solicitud.EstadoSolicitud == SolicitudEstado.AprobadoLider)
        {
            if (!(string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)))
            {
                return Result.Fail("Solo Admin o SuperAdmin pueden rechazar en aprobacion final.");
            }

            solicitud.PersonaAprobador2Id = approverId;
            solicitud.FechaAprobacion2 = now;
            solicitud.EstadoSolicitud = SolicitudEstado.Rechazado;
        }
        else
        {
            return Result.Fail("No tienes permisos para rechazar.");
        }

        solicitud.ActualizadoEn = now;
        await _db.SaveChangesAsync();

        return Result.Ok();
    }

    public async Task<Result> CancelAsync(string solicitudId, string actorPersonaId, string actorRole, string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(solicitudId))
        {
            return Result.Fail("Solicitud es requerida.");
        }

        if (string.IsNullOrWhiteSpace(actorPersonaId))
        {
            return Result.Fail("Persona ejecutora es requerida.");
        }

        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Result.Fail("Usuario ejecutor es requerido.");
        }

        var solicitud = await _db.Solicitudes
            .SingleOrDefaultAsync(s => s.SolicitudId == solicitudId);

        if (solicitud == null)
        {
            return Result.Fail("Solicitud no existe.");
        }

        if (solicitud.EstadoSolicitud == SolicitudEstado.Rechazado ||
            solicitud.EstadoSolicitud == SolicitudEstado.Cancelado)
        {
            return Result.Fail("La solicitud ya fue resuelta.");
        }

        var role = actorRole?.Trim() ?? "Usuario";
        var isAdmin =
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        var isLider = string.Equals(role, "Lider", StringComparison.OrdinalIgnoreCase);
        var isOwner = string.Equals(solicitud.PersonaSolicitanteId, actorPersonaId, StringComparison.Ordinal);

        if (solicitud.EstadoSolicitud == SolicitudEstado.AprobadoFinal)
        {
            if (!isAdmin)
            {
                return Result.Fail("Solo Admin o SuperAdmin pueden cancelar una solicitud aprobada.");
            }
        }
        else if (solicitud.EstadoSolicitud == SolicitudEstado.Pendiente ||
                 solicitud.EstadoSolicitud == SolicitudEstado.AprobadoLider)
        {
            if (!isOwner && !isAdmin && !isLider)
            {
                return Result.Fail("No tienes permisos para cancelar esta solicitud.");
            }

            if (!isOwner && isLider)
            {
                var actorEquipoId = await _db.Personas
                    .AsNoTracking()
                    .Where(p => p.PersonaId == actorPersonaId && !p.Borrado)
                    .Select(p => p.EquipoId)
                    .FirstOrDefaultAsync();

                var targetEquipoId = await _db.Personas
                    .AsNoTracking()
                    .Where(p => p.PersonaId == solicitud.PersonaSolicitanteId)
                    .Select(p => p.EquipoId)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(actorEquipoId) ||
                    !string.Equals(actorEquipoId, targetEquipoId, StringComparison.Ordinal))
                {
                    return Result.Fail("No tienes permisos para cancelar esta solicitud.");
                }
            }
        }
        else
        {
            return Result.Fail("No se puede cancelar la solicitud en su estado actual.");
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            if (solicitud.EstadoSolicitud == SolicitudEstado.AprobadoFinal)
            {
                var undoResult = await UndoAppliedEffectsAsync(solicitud);
                if (!undoResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return undoResult;
                }
            }

            solicitud.EstadoSolicitud = SolicitudEstado.Cancelado;
            solicitud.ActualizadoEn = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Result.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al cancelar solicitud {SolicitudId}.", solicitudId);
            return Result.Fail("Ha ocurrido un error al cancelar la solicitud.");
        }
    }

    private async Task<(string? Aprobar, string? Rechazar)> ResolveDecisionPermissionCodesAsync(Solicitud solicitud)
    {
        var kind = await ResolveSolicitudKindAsync(solicitud);
        return kind switch
        {
            "vacacion" => (
                PermisosAccesoCodigos.SolicitudAprobarVacacion,
                PermisosAccesoCodigos.SolicitudRechazarVacacion),
            "permiso" => (
                PermisosAccesoCodigos.SolicitudAprobarPermiso,
                PermisosAccesoCodigos.SolicitudRechazarPermiso),
            "cambioturno" => (
                PermisosAccesoCodigos.SolicitudAprobarCambioTurno,
                PermisosAccesoCodigos.SolicitudRechazarCambioTurno),
            _ => (null, null)
        };
    }

    private async Task<string?> ResolveSolicitudKindAsync(Solicitud solicitud)
    {
        if (solicitud == null)
        {
            return null;
        }

        var tipoId = solicitud.TipoSolicitudId?.Trim().ToUpperInvariant() ?? string.Empty;
        if (tipoId.StartsWith("VAC", StringComparison.Ordinal))
        {
            return "vacacion";
        }

        if (tipoId.StartsWith("PER", StringComparison.Ordinal))
        {
            return "permiso";
        }

        if (tipoId.StartsWith("CAM", StringComparison.Ordinal))
        {
            return "cambioturno";
        }

        if (tipoId.StartsWith("CAL", StringComparison.Ordinal))
        {
            return "calamidad";
        }

        var hasVacacion = await _db.Vacaciones
            .AsNoTracking()
            .AnyAsync(v => v.SolicitudId == solicitud.SolicitudId);
        if (hasVacacion)
        {
            return "vacacion";
        }

        var hasPermiso = await _db.Permisos
            .AsNoTracking()
            .AnyAsync(p => p.SolicitudId == solicitud.SolicitudId);
        if (hasPermiso)
        {
            return "permiso";
        }

        var hasCambio = await _db.CambiosTurno
            .AsNoTracking()
            .AnyAsync(c => c.SolicitudId == solicitud.SolicitudId);
        if (hasCambio)
        {
            return "cambioturno";
        }

        var hasCalamidad = await _db.Calamidades
            .AsNoTracking()
            .AnyAsync(c => c.SolicitudId == solicitud.SolicitudId);
        if (hasCalamidad)
        {
            return "calamidad";
        }

        var tipoName = await _db.TipoSolicitudes
            .AsNoTracking()
            .Where(t => t.TipoSolicitudId == solicitud.TipoSolicitudId)
            .Select(t => t.NombreSolicitud)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(tipoName))
        {
            var normalized = tipoName.Trim().ToLowerInvariant();
            if (normalized.Contains("vac"))
            {
                return "vacacion";
            }

            if (normalized.Contains("perm"))
            {
                return "permiso";
            }

            if (normalized.Contains("cambio"))
            {
                return "cambioturno";
            }

            if (normalized.Contains("calam") || normalized.Contains("ausen"))
            {
                return "calamidad";
            }
        }

        return null;
    }

    private async Task<Result> UndoAppliedEffectsAsync(Solicitud solicitud)
    {
        var kind = await ResolveSolicitudKindAsync(solicitud);
        if (!string.Equals(kind, "calamidad", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Ok();
        }

        var replacements = await _db.CalamidadReemplazos
            .Where(item => item.SolicitudId == solicitud.SolicitudId)
            .ToListAsync();

        if (replacements.Count == 0)
        {
            return Result.Ok();
        }

        var generatedTurnoIds = replacements
            .Where(item => string.Equals(item.ModoReemplazo, "NEW_SHIFT", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.TurnoReemplazoId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _db.CalamidadReemplazos.RemoveRange(replacements);

        if (generatedTurnoIds.Count > 0)
        {
            var hasPermiso = await (
                from permiso in _db.Permisos.AsNoTracking()
                join solicitudLink in _db.Solicitudes.AsNoTracking() on permiso.SolicitudId equals solicitudLink.SolicitudId
                where generatedTurnoIds.Contains(permiso.RegistroTurnoId)
                    && solicitudLink.EstadoSolicitud != SolicitudEstado.Rechazado
                    && solicitudLink.EstadoSolicitud != SolicitudEstado.Cancelado
                select permiso.PermisoId)
                .AnyAsync();

            var hasCambio = await (
                from cambio in _db.CambiosTurno.AsNoTracking()
                join solicitudLink in _db.Solicitudes.AsNoTracking() on cambio.SolicitudId equals solicitudLink.SolicitudId
                where (generatedTurnoIds.Contains(cambio.TurnoOrigenId) ||
                       generatedTurnoIds.Contains(cambio.TurnoDestinoId))
                    && solicitudLink.EstadoSolicitud != SolicitudEstado.Rechazado
                    && solicitudLink.EstadoSolicitud != SolicitudEstado.Cancelado
                select cambio.CambioTurnoId)
                .AnyAsync();

            var hasOtherCalamidadLink = await _db.CalamidadReemplazos
                .AsNoTracking()
                .AnyAsync(item =>
                    item.SolicitudId != solicitud.SolicitudId &&
                    (generatedTurnoIds.Contains(item.TurnoAusenteId) ||
                     generatedTurnoIds.Contains(item.TurnoReemplazoId)));

            if (hasPermiso || hasCambio || hasOtherCalamidadLink)
            {
                return Result.Fail("No se puede cancelar la calamidad porque los turnos de reemplazo ya tienen solicitudes asociadas.");
            }

            var generatedTurnos = await _db.RegistroTurnos
                .Where(rt => generatedTurnoIds.Contains(rt.TurnoId))
                .ToListAsync();

            if (generatedTurnos.Count > 0)
            {
                _db.RegistroTurnos.RemoveRange(generatedTurnos);
            }
        }
        return Result.Ok();
    }

    public async Task<IReadOnlyList<SolicitudListItemViewModel>> GetAllAsync(
        string? personaId,
        string? equipoId,
        string role,
        bool canReviewEquipoSolicitudes = false)
    {
        var roleNormalized = role?.Trim() ?? "Usuario";
        var solicitudesQuery = _db.Solicitudes
            .AsNoTracking()
            .Include(s => s.TipoSolicitud)
            .Include(s => s.PersonaSolicitante)
            .AsQueryable();

        if (string.Equals(roleNormalized, "Usuario", StringComparison.OrdinalIgnoreCase))
        {
            if (canReviewEquipoSolicitudes)
            {
                if (!string.IsNullOrWhiteSpace(equipoId))
                {
                    solicitudesQuery = solicitudesQuery.Where(s => s.PersonaSolicitante!.EquipoId == equipoId);
                }
            }
            else if (!string.IsNullOrWhiteSpace(personaId))
            {
                solicitudesQuery = solicitudesQuery.Where(s => s.PersonaSolicitanteId == personaId);
            }
        }
        else if (string.Equals(roleNormalized, "Lider", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(equipoId))
            {
                solicitudesQuery = solicitudesQuery.Where(s => s.PersonaSolicitante!.EquipoId == equipoId);
            }
        }

        var solicitudes = await solicitudesQuery.ToListAsync();

        var personaIds = solicitudes
            .Select(s => s.PersonaSolicitanteId)
            .Concat(solicitudes.Where(s => s.PersonaAprobador1Id != null).Select(s => s.PersonaAprobador1Id!))
            .Concat(solicitudes.Where(s => s.PersonaAprobador2Id != null).Select(s => s.PersonaAprobador2Id!))
            .Distinct()
            .ToList();

        var personas = await _db.Personas
            .AsNoTracking()
            .Include(p => p.Equipo)
            .Where(p => personaIds.Contains(p.PersonaId))
            .ToDictionaryAsync(p => p.PersonaId);

        var personaGrupoMap = await (
            from pg in _db.PersonaGrupos.AsNoTracking()
            join g in _db.Grupos.AsNoTracking() on pg.GrupoId equals g.GrupoId
            where personaIds.Contains(pg.PersonaId)
            select new { pg.PersonaId, g.NombreGrupo })
            .ToListAsync();

        var grupoLookup = personaGrupoMap
            .GroupBy(x => x.PersonaId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.NombreGrupo).OrderBy(n => n).FirstOrDefault());

        var solicitudIds = solicitudes.Select(s => s.SolicitudId).ToList();
        var vacacionMap = await _db.Vacaciones
            .AsNoTracking()
            .Where(v => solicitudIds.Contains(v.SolicitudId))
            .ToDictionaryAsync(v => v.SolicitudId);

        var calamidadMap = await _db.Calamidades
            .AsNoTracking()
            .Where(c => solicitudIds.Contains(c.SolicitudId))
            .ToDictionaryAsync(c => c.SolicitudId);

        var permisoItems = await _db.Permisos
            .AsNoTracking()
            .Where(p => solicitudIds.Contains(p.SolicitudId))
            .Select(p => new
            {
                p.SolicitudId,
                p.RegistroTurnoId,
                p.HoraInicio,
                p.HoraFin,
                p.Motivo
            })
            .ToListAsync();

        var cambioMap = await _db.CambiosTurno
            .AsNoTracking()
            .Where(c => solicitudIds.Contains(c.SolicitudId))
            .ToDictionaryAsync(c => c.SolicitudId);

        var turnoIds = permisoItems.Select(p => p.RegistroTurnoId)
            .Concat(cambioMap.Values.Select(c => c.TurnoOrigenId))
            .Concat(cambioMap.Values.Select(c => c.TurnoDestinoId))
            .Distinct()
            .ToList();

        var turnoMap = await (
            from rt in _db.RegistroTurnos.AsNoTracking()
            join p in _db.Personas.AsNoTracking() on rt.PersonaId equals p.PersonaId
            join tt in _db.TipoTurnos.AsNoTracking() on rt.TipoTurnoId equals tt.TipoTurnoId
            where turnoIds.Contains(rt.TurnoId)
            select new
            {
                rt.TurnoId,
                rt.FechaTurno,
                PersonaNombre = BuildPersonaName(p.Nombre, p.SegundoNombre, p.Apellido, p.SegundoApellido),
                TurnoLabel = $"{tt.NombreTurno} ({tt.HoraInicio:HH\\:mm}-{tt.HoraFin:HH\\:mm})"
            })
            .ToDictionaryAsync(rt => rt.TurnoId);

        var monthNames = new[]
        {
            "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
            "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
        };

        string FormatDate(DateTime? date)
        {
            if (!date.HasValue)
            {
                return string.Empty;
            }
            var value = date.Value;
            return $"{value.Day} {monthNames[value.Month - 1]} {value.Year}";
        }

        var items = new List<SolicitudListItemViewModel>();
        foreach (var solicitud in solicitudes.OrderByDescending(s => s.FechaSolicitud))
        {
            var title = solicitud.TipoSolicitud?.NombreSolicitud ?? "Solicitud";
            var userName = solicitud.PersonaSolicitante?.NombreCompleto ?? "Usuario";
            var typeName = solicitud.TipoSolicitud?.NombreSolicitud ?? "Solicitud";
            DateTime startDate = solicitud.FechaSolicitud;
            DateTime endDate = solicitud.FechaSolicitud;
            string permisoHoraInicio = string.Empty;
            string permisoHoraFin = string.Empty;
            string permisoMotivo = string.Empty;
            string calamidadMotivo = string.Empty;
            string cambioOrigenNombre = string.Empty;
            string cambioOrigenMonth = string.Empty;
            string cambioOrigenDay = string.Empty;
            string cambioOrigenTurno = string.Empty;
            string cambioDestinoNombre = string.Empty;
            string cambioDestinoMonth = string.Empty;
            string cambioDestinoDay = string.Empty;
            string cambioDestinoTurno = string.Empty;
            string cambioMotivo = string.Empty;

            if (vacacionMap.TryGetValue(solicitud.SolicitudId, out var vacacion))
            {
                startDate = vacacion.FechaInicio.ToDateTime(TimeOnly.MinValue);
                endDate = vacacion.FechaFin.ToDateTime(TimeOnly.MinValue);
            }
            else if (calamidadMap.TryGetValue(solicitud.SolicitudId, out var calamidad))
            {
                startDate = calamidad.FechaInicio.ToDateTime(TimeOnly.MinValue);
                endDate = calamidad.FechaFin.ToDateTime(TimeOnly.MinValue);
                calamidadMotivo = calamidad.Motivo ?? string.Empty;
            }
            else if (cambioMap.TryGetValue(solicitud.SolicitudId, out var cambio))
            {
                cambioMotivo = cambio.Motivo ?? string.Empty;
                if (turnoMap.TryGetValue(cambio.TurnoOrigenId, out var turnoOrigen))
                {
                    startDate = turnoOrigen.FechaTurno.ToDateTime(TimeOnly.MinValue);
                    endDate = startDate;
                    cambioOrigenNombre = turnoOrigen.PersonaNombre ?? string.Empty;
                    cambioOrigenMonth = monthNames[startDate.Month - 1];
                    cambioOrigenDay = startDate.Day.ToString();
                    cambioOrigenTurno = turnoOrigen.TurnoLabel ?? string.Empty;
                }
                if (turnoMap.TryGetValue(cambio.TurnoDestinoId, out var turnoDestino))
                {
                    endDate = turnoDestino.FechaTurno.ToDateTime(TimeOnly.MinValue);
                    cambioDestinoNombre = turnoDestino.PersonaNombre ?? string.Empty;
                    cambioDestinoMonth = monthNames[endDate.Month - 1];
                    cambioDestinoDay = endDate.Day.ToString();
                    cambioDestinoTurno = turnoDestino.TurnoLabel ?? string.Empty;
                }
            }
            else
            {
                var permiso = permisoItems.FirstOrDefault(p => p.SolicitudId == solicitud.SolicitudId);
                if (permiso != null && turnoMap.TryGetValue(permiso.RegistroTurnoId, out var turno))
                {
                    startDate = turno.FechaTurno.ToDateTime(TimeOnly.MinValue);
                    endDate = startDate;
                    permisoHoraInicio = permiso.HoraInicio.ToString("HH:mm");
                    permisoHoraFin = permiso.HoraFin.ToString("HH:mm");
                    permisoMotivo = permiso.Motivo ?? string.Empty;
                }
            }

            if (startDate > endDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var days = (int)Math.Floor((endDate - startDate).TotalDays) + 1;
            if (days < 1)
            {
                days = 1;
            }

            var (status, statusCode) = solicitud.EstadoSolicitud switch
            {
                SolicitudEstado.AprobadoFinal => ("Aprobado", "approved"),
                SolicitudEstado.Rechazado => ("Rechazado", "rejected"),
                SolicitudEstado.Cancelado => ("Cancelado", "cancelled"),
                SolicitudEstado.AprobadoLider => ("En aprobacion", "inreview"),
                _ => ("Pendiente", "pending")
            };

            items.Add(new SolicitudListItemViewModel
            {
                SolicitudId = solicitud.SolicitudId,
                PersonaSolicitanteId = solicitud.PersonaSolicitanteId,
                IsOwnedByCurrentPersona = !string.IsNullOrWhiteSpace(personaId) &&
                    string.Equals(solicitud.PersonaSolicitanteId, personaId, StringComparison.Ordinal),
                Title = $"Solicitud de {title}",
                UserName = userName,
                TypeName = typeName,
                EquipoName = personas.TryGetValue(solicitud.PersonaSolicitanteId, out var persona)
                    ? persona.Equipo?.NombreEquipo ?? string.Empty
                    : string.Empty,
                GrupoName = grupoLookup.TryGetValue(solicitud.PersonaSolicitanteId, out var grupoName)
                    ? grupoName ?? string.Empty
                    : string.Empty,
                StartMonth = monthNames[startDate.Month - 1],
                StartDay = startDate.Day.ToString(),
                EndMonth = monthNames[endDate.Month - 1],
                EndDay = endDate.Day.ToString(),
                Days = days.ToString(),
                Status = status,
                StatusCode = statusCode,
                Aprobador1Name = solicitud.PersonaAprobador1Id != null && personas.TryGetValue(solicitud.PersonaAprobador1Id, out var aprobador1)
                    ? aprobador1.NombreCompleto
                    : string.Empty,
                Aprobador1Date = FormatDate(solicitud.FechaAprobacion1),
                Aprobador2Name = solicitud.PersonaAprobador2Id != null && personas.TryGetValue(solicitud.PersonaAprobador2Id, out var aprobador2)
                    ? aprobador2.NombreCompleto
                    : string.Empty,
                Aprobador2Date = FormatDate(solicitud.FechaAprobacion2),
                PermisoHoraInicio = permisoHoraInicio,
                PermisoHoraFin = permisoHoraFin,
                PermisoMotivo = permisoMotivo,
                CalamidadMotivo = calamidadMotivo,
                CambioOrigenNombre = cambioOrigenNombre,
                CambioOrigenMonth = cambioOrigenMonth,
                CambioOrigenDay = cambioOrigenDay,
                CambioOrigenTurno = cambioOrigenTurno,
                CambioDestinoNombre = cambioDestinoNombre,
                CambioDestinoMonth = cambioDestinoMonth,
                CambioDestinoDay = cambioDestinoDay,
                CambioDestinoTurno = cambioDestinoTurno,
                CambioMotivo = cambioMotivo
            });
        }

        return items;
    }

    private static string BuildPersonaName(string nombre, string? segundoNombre, string apellido, string? segundoApellido)
    {
        return string.Join(" ", new[] { nombre, segundoNombre, apellido, segundoApellido }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
