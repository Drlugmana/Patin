using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Security;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Controllers;

[Authorize(Policy = "UserAbove")]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ISolicitudService _solicitudService;
    private readonly IPermisoAccesoResolver _permisoAccesoResolver;

    [ActivatorUtilitiesConstructor]
    public SolicitudesController(
        ApplicationDbContext db,
        ISolicitudService solicitudService,
        IPermisoAccesoResolver permisoAccesoResolver)
    {
        _db = db;
        _solicitudService = solicitudService;
        _permisoAccesoResolver = permisoAccesoResolver;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var persona = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.Borrado)
            .Select(p => new { p.PersonaId, p.EquipoId })
            .FirstOrDefaultAsync();

        var role = User.IsInRole("SuperAdmin") || User.IsInRole("Admin")
            ? "Admin"
            : User.IsInRole("Lider")
                ? "Lider"
                : "Usuario";

        var canApproveVacacion = false;
        var canRejectVacacion = false;
        var canApprovePermiso = false;
        var canRejectPermiso = false;
        var canApproveCambioTurno = false;
        var canRejectCambioTurno = false;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            canApproveVacacion = await _permisoAccesoResolver.TienePermisoAsync(userId, PermisosAccesoCodigos.SolicitudAprobarVacacion);
            canRejectVacacion = await _permisoAccesoResolver.TienePermisoAsync(userId, PermisosAccesoCodigos.SolicitudRechazarVacacion);
            canApprovePermiso = await _permisoAccesoResolver.TienePermisoAsync(userId, PermisosAccesoCodigos.SolicitudAprobarPermiso);
            canRejectPermiso = await _permisoAccesoResolver.TienePermisoAsync(userId, PermisosAccesoCodigos.SolicitudRechazarPermiso);
            canApproveCambioTurno = await _permisoAccesoResolver.TienePermisoAsync(userId, PermisosAccesoCodigos.SolicitudAprobarCambioTurno);
            canRejectCambioTurno = await _permisoAccesoResolver.TienePermisoAsync(userId, PermisosAccesoCodigos.SolicitudRechazarCambioTurno);
        }

        var canReviewEquipoSolicitudes = !string.Equals(role, "Usuario", StringComparison.OrdinalIgnoreCase)
            || canApproveVacacion
            || canRejectVacacion
            || canApprovePermiso
            || canRejectPermiso
            || canApproveCambioTurno
            || canRejectCambioTurno;

        var items = await _solicitudService.GetAllAsync(
            persona?.PersonaId,
            persona?.EquipoId,
            role,
            canReviewEquipoSolicitudes);
        var model = new SolicitudesIndexViewModel
        {
            Items = items,
            Selected = items.FirstOrDefault(),
            CurrentPersonaId = persona?.PersonaId ?? string.Empty,
            CanApproveVacacion = canApproveVacacion,
            CanRejectVacacion = canRejectVacacion,
            CanApprovePermiso = canApprovePermiso,
            CanRejectPermiso = canRejectPermiso,
            CanApproveCambioTurno = canApproveCambioTurno,
            CanRejectCambioTurno = canRejectCambioTurno
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVacacion([FromBody] SolicitudCreateRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var personaId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.Borrado)
            .Select(p => p.PersonaId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(personaId))
        {
            return BadRequest(new { message = "No se encontro la persona solicitante." });
        }

        var role = User.IsInRole("SuperAdmin") || User.IsInRole("Admin")
            ? "Admin"
            : User.IsInRole("Lider")
                ? "Lider"
                : "Usuario";

        var result = await _solicitudService.CreateAsync(personaId, role, request);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error ?? "No se pudo crear la solicitud." });
        }

        return Ok(new { message = "Solicitud creada." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePermiso([FromBody] PermisoCreateRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Solicitud invalida." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var personaId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.Borrado)
            .Select(p => p.PersonaId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(personaId))
        {
            return BadRequest(new { message = "No se encontro la persona solicitante." });
        }

        if (string.IsNullOrWhiteSpace(request.TipoSolicitudId))
        {
            return BadRequest(new { message = "Tipo de solicitud es requerido." });
        }

        static bool TryParseTime(string? value, out TimeOnly time)
        {
            time = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
                || TimeOnly.TryParseExact(value, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
                || TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
        }

        if (!TryParseTime(request.HoraInicio, out var horaInicio) ||
            !TryParseTime(request.HoraFin, out var horaFin))
        {
            return BadRequest(new { message = "Formato de hora invalido." });
        }

        if (horaFin <= horaInicio)
        {
            return BadRequest(new { message = "La hora fin debe ser mayor que la hora inicio." });
        }

        var registros = await (
            from rt in _db.RegistroTurnos.AsNoTracking()
            join tt in _db.TipoTurnos.AsNoTracking() on rt.TipoTurnoId equals tt.TipoTurnoId
            where rt.PersonaId == personaId && rt.FechaTurno == request.Fecha
            select new
            {
                rt.TurnoId,
                HoraInicio = tt.HoraInicio,
                HoraFin = tt.HoraFin
            })
            .ToListAsync();

        if (registros.Count == 0)
        {
            return BadRequest(new { message = "No hay turnos registrados para ese dia." });
        }

        static bool IsWithinShift(TimeOnly shiftStart, TimeOnly shiftEnd, TimeOnly permisoStart, TimeOnly permisoEnd)
        {
            if (shiftEnd > shiftStart)
            {
                return permisoStart >= shiftStart && permisoEnd <= shiftEnd;
            }
            return permisoStart >= shiftStart || permisoEnd <= shiftEnd;
        }

        var matching = registros
            .Where(r => IsWithinShift(r.HoraInicio, r.HoraFin, horaInicio, horaFin))
            .OrderBy(r => r.HoraInicio)
            .ToList();

        if (matching.Count == 0)
        {
            return BadRequest(new { message = "El horario no coincide con un turno registrado." });
        }

        var role = User.IsInRole("SuperAdmin") || User.IsInRole("Admin")
            ? "Admin"
            : User.IsInRole("Lider")
                ? "Lider"
                : "Usuario";

        var result = await _solicitudService.CreateAsync(
            personaId,
            role,
            new SolicitudCreateRequest(
                request.TipoSolicitudId,
                null,
                new PermisoRequest(
                    matching[0].TurnoId,
                    horaInicio,
                    horaFin,
                    request.Motivo ?? string.Empty),
                null));

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error ?? "No se pudo crear la solicitud." });
        }

        return Ok(new { message = "Solicitud creada." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCambioTurno([FromBody] CambioTurnoCreateRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Solicitud invalida." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var personaId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.Borrado)
            .Select(p => p.PersonaId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(personaId))
        {
            return BadRequest(new { message = "No se encontro la persona solicitante." });
        }

        if (string.IsNullOrWhiteSpace(request.TipoSolicitudId))
        {
            return BadRequest(new { message = "Tipo de solicitud es requerido." });
        }

        if (string.IsNullOrWhiteSpace(request.TurnoOrigenId))
        {
            return BadRequest(new { message = "Debe seleccionar el turno origen." });
        }

        var existingCambio = await (
            from c in _db.CambiosTurno.AsNoTracking()
            join s in _db.Solicitudes.AsNoTracking() on c.SolicitudId equals s.SolicitudId
            where (s.EstadoSolicitud == SolicitudEstado.Pendiente
                || s.EstadoSolicitud == SolicitudEstado.AprobadoLider)
                && (c.TurnoOrigenId == request.TurnoOrigenId || c.TurnoDestinoId == request.TurnoOrigenId
                    || (!string.IsNullOrWhiteSpace(request.TurnoDestinoId) &&
                        (c.TurnoOrigenId == request.TurnoDestinoId || c.TurnoDestinoId == request.TurnoDestinoId)))
            select c.CambioTurnoId)
            .AnyAsync();

        if (existingCambio)
        {
            return BadRequest(new { message = "Ya existe un cambio de turno pendiente para ese turno." });
        }

        var role = User.IsInRole("SuperAdmin") || User.IsInRole("Admin")
            ? "Admin"
            : User.IsInRole("Lider")
                ? "Lider"
                : "Usuario";

        string? turnoDestinoId = request.TurnoDestinoId;
        if (string.IsNullOrWhiteSpace(turnoDestinoId))
        {
            if (string.IsNullOrWhiteSpace(request.FechaDestino) ||
                string.IsNullOrWhiteSpace(request.TipoTurnoDestinoId))
            {
                return BadRequest(new { message = "Debe seleccionar el destino del turno." });
            }

            if (!DateOnly.TryParse(request.FechaDestino, out var fechaDestino))
            {
                return BadRequest(new { message = "Fecha destino invalida." });
            }

            var turnoOrigen = await _db.RegistroTurnos
                .AsNoTracking()
                .SingleOrDefaultAsync(rt => rt.TurnoId == request.TurnoOrigenId);

            if (turnoOrigen == null)
            {
                return BadRequest(new { message = "El turno origen no existe." });
            }

            var exists = await _db.RegistroTurnos
                .AsNoTracking()
                .AnyAsync(rt =>
                    rt.PersonaId == turnoOrigen.PersonaId &&
                    rt.FechaTurno == fechaDestino &&
                    rt.TipoTurnoId == request.TipoTurnoDestinoId);

            if (exists)
            {
                return BadRequest(new { message = "Ya existe un turno en ese horario." });
            }

            turnoDestinoId = Guid.NewGuid().ToString("N");
            var provisional = new RegistroTurno
            {
                TurnoId = turnoDestinoId,
                PersonaId = turnoOrigen.PersonaId,
                GrupoId = turnoOrigen.GrupoId,
                TipoTurnoId = request.TipoTurnoDestinoId,
                FechaTurno = fechaDestino
            };
            _db.RegistroTurnos.Add(provisional);
        }
        else
        {
            var destinoExists = await _db.RegistroTurnos
                .AsNoTracking()
                .AnyAsync(rt => rt.TurnoId == turnoDestinoId);
            if (!destinoExists)
            {
                return BadRequest(new { message = "El turno destino no existe." });
            }
        }

        var result = await _solicitudService.CreateAsync(
            personaId,
            role,
            new SolicitudCreateRequest(
                request.TipoSolicitudId,
                null,
                null,
                new CambioTurnoRequest(
                    request.TurnoOrigenId,
                    turnoDestinoId,
                    request.Motivo ?? string.Empty)));

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error ?? "No se pudo crear la solicitud." });
        }

        return Ok(new { message = "Solicitud creada." });
    }

    [HttpPost]
    [Authorize(Policy = "LiderAbove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCalamidad([FromBody] CalamidadCreateRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Solicitud invalida." });
        }

        if (string.IsNullOrWhiteSpace(request.PersonaId))
        {
            return BadRequest(new { message = "Debe seleccionar la persona ausente." });
        }

        var turnoId = (request.TurnoId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(turnoId))
        {
            return BadRequest(new { message = "Debe seleccionar el turno para registrar la calamidad." });
        }

        if (request.FechaFin < request.FechaInicio)
        {
            return BadRequest(new { message = "La fecha fin no puede ser menor que la fecha inicio." });
        }

        var motivo = (request.Motivo ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return BadRequest(new { message = "El motivo de calamidad es requerido." });
        }

        if (motivo.Length > 240)
        {
            return BadRequest(new { message = "El motivo no puede superar 240 caracteres." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var role = User.IsInRole("SuperAdmin") || User.IsInRole("Admin")
            ? "Admin"
            : User.IsInRole("Lider")
                ? "Lider"
                : "Usuario";

        if (string.Equals(role, "Usuario", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var currentPersona = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.Borrado)
            .Select(p => new { p.PersonaId, p.EquipoId })
            .FirstOrDefaultAsync();

        if (currentPersona == null)
        {
            return BadRequest(new { message = "No se encontro la persona que registra la calamidad." });
        }

        var turnoSeleccionado = await _db.RegistroTurnos
            .AsNoTracking()
            .Where(rt => rt.TurnoId == turnoId)
            .Select(rt => new
            {
                rt.TurnoId,
                rt.PersonaId,
                rt.FechaTurno
            })
            .FirstOrDefaultAsync();

        if (turnoSeleccionado == null)
        {
            return BadRequest(new { message = "El turno seleccionado no existe." });
        }

        if (await IsTurnoAlreadyLinkedToActiveCalamidadReplacementAsync(turnoId))
        {
            return BadRequest(new
            {
                message = "No se puede registrar una calamidad sobre un turno que ya actua como reemplazo."
            });
        }

        if (!string.Equals(turnoSeleccionado.PersonaId, request.PersonaId, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "El turno seleccionado no pertenece a la persona de la calamidad." });
        }

        if (turnoSeleccionado.FechaTurno != request.FechaInicio)
        {
            return BadRequest(new { message = "El turno seleccionado no coincide con la fecha de inicio de la calamidad." });
        }

        var targetPersona = await _db.Personas
            .AsNoTracking()
            .Where(p => p.PersonaId == request.PersonaId && !p.Borrado)
            .Select(p => new
            {
                p.PersonaId,
                p.EquipoId,
                p.Nombre,
                p.SegundoNombre,
                p.Apellido,
                p.SegundoApellido
            })
            .FirstOrDefaultAsync();

        if (targetPersona == null)
        {
            return BadRequest(new { message = "La persona seleccionada no existe." });
        }

        if (string.Equals(role, "Lider", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentPersona.EquipoId, targetPersona.EquipoId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        var hasOverlap = await (
            from c in _db.Calamidades.AsNoTracking()
            join s in _db.Solicitudes.AsNoTracking() on c.SolicitudId equals s.SolicitudId
            where s.PersonaSolicitanteId == request.PersonaId
                && s.EstadoSolicitud != SolicitudEstado.Rechazado
                && s.EstadoSolicitud != SolicitudEstado.Cancelado
                && c.FechaInicio <= request.FechaFin
                && request.FechaInicio <= c.FechaFin
            select c.CalamidadId)
            .AnyAsync();

        if (hasOverlap)
        {
            return BadRequest(new
            {
                message = "Ya existe una calamidad registrada para esta persona en el rango de fechas seleccionado."
            });
        }

        var tipoSolicitudId = await ResolveOrCreateCalamidadTipoSolicitudIdAsync();
        if (string.IsNullOrWhiteSpace(tipoSolicitudId))
        {
            return BadRequest(new { message = "No se pudo resolver el tipo de solicitud CALAMIDAD." });
        }

        var normalizedReemplazos = (request.Reemplazos ?? Array.Empty<CalamidadReemplazoItem>())
            .Where(item =>
                item != null &&
                !string.IsNullOrWhiteSpace(item.TurnoAusenteId))
            .Select(item => new
            {
                TurnoAusenteId = item.TurnoAusenteId.Trim(),
                TurnoReemplazoId = string.IsNullOrWhiteSpace(item.TurnoReemplazoId) ? null : item.TurnoReemplazoId.Trim(),
                PersonaReemplazoId = string.IsNullOrWhiteSpace(item.PersonaReemplazoId) ? null : item.PersonaReemplazoId.Trim(),
                ModoReemplazo = NormalizeCalamidadReemplazoModo(
                    item.ModoReemplazo,
                    item.TurnoReemplazoId,
                    item.PersonaReemplazoId)
            })
            .GroupBy(item => item.TurnoAusenteId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (!request.SinReemplazosConfirmado && normalizedReemplazos.Count == 0)
        {
            return BadRequest(new
            {
                message = "Debes guardar reemplazos o confirmar que no se requieren reemplazos para hacer efectiva la calamidad."
            });
        }

        var missingSwapTurno = normalizedReemplazos
            .Any(item =>
                string.Equals(item.ModoReemplazo, "SWAP", StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(item.TurnoReemplazoId));

        if (missingSwapTurno)
        {
            return BadRequest(new { message = "Debes seleccionar el turno existente para los reemplazos por intercambio." });
        }

        var missingNewShiftPersona = normalizedReemplazos
            .Any(item =>
                string.Equals(item.ModoReemplazo, "NEW_SHIFT", StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(item.PersonaReemplazoId));

        if (missingNewShiftPersona)
        {
            return BadRequest(new { message = "Debes seleccionar la persona para crear el turno nuevo de reemplazo." });
        }

        var duplicatedReplacements = normalizedReemplazos
            .Where(item => string.Equals(item.ModoReemplazo, "SWAP", StringComparison.Ordinal))
            .Where(item => !string.IsNullOrWhiteSpace(item.TurnoReemplazoId))
            .GroupBy(item => item.TurnoReemplazoId!, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);

        if (duplicatedReplacements)
        {
            return BadRequest(new { message = "Un mismo turno de reemplazo no se puede asignar a dos ausencias." });
        }

        var now = DateTime.UtcNow;
        var solicitudId = Guid.NewGuid().ToString("N");

        var solicitud = new Solicitud
        {
            SolicitudId = solicitudId,
            PersonaSolicitanteId = request.PersonaId,
            TipoSolicitudId = tipoSolicitudId,
            EstadoSolicitud = SolicitudEstado.AprobadoFinal,
            FechaSolicitud = now,
            FechaAprobacion1 = now,
            FechaAprobacion2 = now,
            PersonaAprobador1Id = currentPersona.PersonaId,
            PersonaAprobador2Id = currentPersona.PersonaId,
            CreadoEn = now,
            ActualizadoEn = now
        };

        var calamidad = new Calamidad
        {
            CalamidadId = Guid.NewGuid().ToString("N"),
            SolicitudId = solicitudId,
            FechaInicio = request.FechaInicio,
            FechaFin = request.FechaFin,
            Motivo = motivo
        };

        var replacementsToSave = new List<CalamidadReemplazo>();
        var generatedTurnos = new List<RegistroTurno>();
        if (normalizedReemplazos.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(targetPersona.EquipoId))
            {
                return BadRequest(new { message = "La persona ausente no tiene equipo asignado." });
            }

            var turnoAusenteIds = normalizedReemplazos
                .Select(item => item.TurnoAusenteId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var turnoReemplazoIdsSwap = normalizedReemplazos
                .Where(item =>
                    string.Equals(item.ModoReemplazo, "SWAP", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(item.TurnoReemplazoId))
                .Select(item => item.TurnoReemplazoId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var turnoIds = turnoAusenteIds
                .Concat(turnoReemplazoIdsSwap)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var turnos = await _db.RegistroTurnos
                .AsNoTracking()
                .Where(rt => turnoIds.Contains(rt.TurnoId))
                .ToDictionaryAsync(rt => rt.TurnoId);

            if (turnos.Count != turnoIds.Count)
            {
                return BadRequest(new { message = "Uno o mas turnos seleccionados no existen." });
            }

            var personaIdsEnTurnos = turnos.Values
                .Select(rt => rt.PersonaId)
                .Concat(normalizedReemplazos
                    .Where(item => string.Equals(item.ModoReemplazo, "NEW_SHIFT", StringComparison.Ordinal))
                    .Where(item => !string.IsNullOrWhiteSpace(item.PersonaReemplazoId))
                    .Select(item => item.PersonaReemplazoId!))
                .Distinct()
                .ToList();

            var equipoByPersona = await _db.Personas
                .AsNoTracking()
                .Where(p => personaIdsEnTurnos.Contains(p.PersonaId))
                .Select(p => new { p.PersonaId, p.EquipoId })
                .ToDictionaryAsync(p => p.PersonaId, p => p.EquipoId);

            var potentialNewShiftKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in normalizedReemplazos)
            {
                var turnoAusenteId = item.TurnoAusenteId;
                var turnoAusente = turnos[turnoAusenteId];

                if (!string.Equals(turnoAusente.PersonaId, request.PersonaId, StringComparison.Ordinal))
                {
                    return BadRequest(new { message = "El turno ausente no pertenece a la persona de la calamidad." });
                }

                if (turnoAusente.FechaTurno < request.FechaInicio || turnoAusente.FechaTurno > request.FechaFin)
                {
                    return BadRequest(new { message = "El turno ausente esta fuera del rango de la calamidad." });
                }

                if (string.Equals(item.ModoReemplazo, "SWAP", StringComparison.Ordinal))
                {
                    var turnoReemplazoId = item.TurnoReemplazoId!;
                    var turnoReemplazo = turnos[turnoReemplazoId];

                    if (string.Equals(turnoReemplazo.PersonaId, turnoAusente.PersonaId, StringComparison.Ordinal))
                    {
                        return BadRequest(new { message = "La misma persona ausente no puede ser su propio reemplazo." });
                    }

                    if (!equipoByPersona.TryGetValue(turnoReemplazo.PersonaId, out var replacementEquipoId) ||
                        !string.Equals(replacementEquipoId, targetPersona.EquipoId, StringComparison.Ordinal))
                    {
                        return BadRequest(new { message = "El reemplazo debe pertenecer al mismo equipo." });
                    }

                    replacementsToSave.Add(new CalamidadReemplazo
                    {
                        CalamidadReemplazoId = Guid.NewGuid().ToString("N"),
                        SolicitudId = solicitudId,
                        TurnoAusenteId = turnoAusenteId,
                        TurnoReemplazoId = turnoReemplazoId,
                        ModoReemplazo = "SWAP"
                    });
                    continue;
                }

                var personaReemplazoId = item.PersonaReemplazoId!;
                if (string.Equals(personaReemplazoId, turnoAusente.PersonaId, StringComparison.Ordinal))
                {
                    return BadRequest(new { message = "La misma persona ausente no puede ser su propio reemplazo." });
                }

                if (!equipoByPersona.TryGetValue(personaReemplazoId, out var replacementEquipoIdNew) ||
                    !string.Equals(replacementEquipoIdNew, targetPersona.EquipoId, StringComparison.Ordinal))
                {
                    return BadRequest(new { message = "El reemplazo debe pertenecer al mismo equipo." });
                }

                var newShiftKey = $"{personaReemplazoId}|{turnoAusente.FechaTurno:yyyy-MM-dd}|{turnoAusente.TipoTurnoId}|{turnoAusente.GrupoId}";
                if (!potentialNewShiftKeys.Add(newShiftKey))
                {
                    return BadRequest(new { message = "No se pueden crear dos turnos nuevos identicos para la misma persona en la misma fecha." });
                }

                var turnoDuplicado = await _db.RegistroTurnos
                    .AsNoTracking()
                    .AnyAsync(rt =>
                        rt.PersonaId == personaReemplazoId &&
                        rt.FechaTurno == turnoAusente.FechaTurno &&
                        rt.TipoTurnoId == turnoAusente.TipoTurnoId &&
                        rt.GrupoId == turnoAusente.GrupoId);

                if (turnoDuplicado)
                {
                    return BadRequest(new { message = "La persona seleccionada ya tiene ese turno registrado. Usa intercambio o elige otra persona." });
                }

                var newTurnoId = Guid.NewGuid().ToString("N");
                generatedTurnos.Add(new RegistroTurno
                {
                    TurnoId = newTurnoId,
                    PersonaId = personaReemplazoId,
                    GrupoId = turnoAusente.GrupoId,
                    TipoTurnoId = turnoAusente.TipoTurnoId,
                    FechaTurno = turnoAusente.FechaTurno
                });

                replacementsToSave.Add(new CalamidadReemplazo
                {
                    CalamidadReemplazoId = Guid.NewGuid().ToString("N"),
                    SolicitudId = solicitudId,
                    TurnoAusenteId = turnoAusenteId,
                    TurnoReemplazoId = newTurnoId,
                    ModoReemplazo = "NEW_SHIFT"
                });
            }
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Solicitudes.Add(solicitud);
            _db.Calamidades.Add(calamidad);

            if (generatedTurnos.Count > 0)
            {
                _db.RegistroTurnos.AddRange(generatedTurnos);
            }

            if (replacementsToSave.Count > 0)
            {
                _db.CalamidadReemplazos.AddRange(replacementsToSave);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = "No se pudo registrar la calamidad." });
        }

        return Ok(new
        {
            message = replacementsToSave.Count > 0
                ? $"Calamidad registrada con {replacementsToSave.Count} reemplazo(s)."
                : "Calamidad registrada sin reemplazos.",
            solicitudId,
            personaId = targetPersona.PersonaId,
            personaNombre = BuildPersonaName(
                targetPersona.Nombre,
                targetPersona.SegundoNombre,
                targetPersona.Apellido,
                targetPersona.SegundoApellido),
            fechaInicio = request.FechaInicio.ToString("yyyy-MM-dd"),
            fechaFin = request.FechaFin.ToString("yyyy-MM-dd"),
            replacementsSaved = replacementsToSave.Count,
            createdShifts = generatedTurnos.Count
        });
    }

    [HttpPost]
    [Authorize(Policy = "LiderAbove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCalamidadReemplazos([FromBody] CalamidadReemplazoSaveRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SolicitudId))
        {
            return BadRequest(new { message = "Solicitud de reemplazos invalida." });
        }

        if (request.Items == null)
        {
            return BadRequest(new { message = "Debes enviar al menos un reemplazo." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var role = User.IsInRole("SuperAdmin") || User.IsInRole("Admin")
            ? "Admin"
            : User.IsInRole("Lider")
                ? "Lider"
                : "Usuario";

        if (string.Equals(role, "Usuario", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var currentPersona = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.Borrado)
            .Select(p => new { p.PersonaId, p.EquipoId })
            .FirstOrDefaultAsync();

        if (currentPersona == null)
        {
            return BadRequest(new { message = "No se encontro la persona que registra los reemplazos." });
        }

        var solicitud = await _db.Solicitudes
            .AsNoTracking()
            .Where(s => s.SolicitudId == request.SolicitudId)
            .Select(s => new
            {
                s.SolicitudId,
                s.PersonaSolicitanteId
            })
            .FirstOrDefaultAsync();

        if (solicitud == null)
        {
            return BadRequest(new { message = "La solicitud de calamidad no existe." });
        }

        var calamidad = await _db.Calamidades
            .AsNoTracking()
            .Where(c => c.SolicitudId == request.SolicitudId)
            .Select(c => new
            {
                c.SolicitudId,
                c.FechaInicio,
                c.FechaFin
            })
            .FirstOrDefaultAsync();

        if (calamidad == null)
        {
            return BadRequest(new { message = "No se encontro la calamidad asociada." });
        }

        var targetEquipoId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.PersonaId == solicitud.PersonaSolicitanteId && !p.Borrado)
            .Select(p => p.EquipoId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(targetEquipoId))
        {
            return BadRequest(new { message = "La persona ausente no tiene equipo asignado." });
        }

        if (string.Equals(role, "Lider", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentPersona.EquipoId, targetEquipoId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        var normalizedItems = request.Items
            .Where(item =>
                item != null &&
                !string.IsNullOrWhiteSpace(item.TurnoAusenteId))
            .Select(item => new
            {
                TurnoAusenteId = item.TurnoAusenteId.Trim(),
                TurnoReemplazoId = string.IsNullOrWhiteSpace(item.TurnoReemplazoId) ? null : item.TurnoReemplazoId.Trim(),
                PersonaReemplazoId = string.IsNullOrWhiteSpace(item.PersonaReemplazoId) ? null : item.PersonaReemplazoId.Trim(),
                ModoReemplazo = NormalizeCalamidadReemplazoModo(
                    item.ModoReemplazo,
                    item.TurnoReemplazoId,
                    item.PersonaReemplazoId)
            })
            .GroupBy(item => item.TurnoAusenteId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var missingSwapTurno = normalizedItems
            .Any(item =>
                string.Equals(item.ModoReemplazo, "SWAP", StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(item.TurnoReemplazoId));

        if (missingSwapTurno)
        {
            return BadRequest(new { message = "Debes seleccionar el turno existente para los reemplazos por intercambio." });
        }

        var missingNewShiftPersona = normalizedItems
            .Any(item =>
                string.Equals(item.ModoReemplazo, "NEW_SHIFT", StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(item.PersonaReemplazoId));

        if (missingNewShiftPersona)
        {
            return BadRequest(new { message = "Debes seleccionar la persona para crear el turno nuevo de reemplazo." });
        }

        var duplicatedReplacements = normalizedItems
            .Where(item => string.Equals(item.ModoReemplazo, "SWAP", StringComparison.Ordinal))
            .Where(item => !string.IsNullOrWhiteSpace(item.TurnoReemplazoId))
            .GroupBy(item => item.TurnoReemplazoId!, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);

        if (duplicatedReplacements)
        {
            return BadRequest(new { message = "Un mismo turno de reemplazo no se puede asignar a dos ausencias." });
        }

        var replacementsToSave = new List<CalamidadReemplazo>();
        var generatedTurnos = new List<RegistroTurno>();
        if (normalizedItems.Count > 0)
        {
            var turnoAusenteIds = normalizedItems
                .Select(item => item.TurnoAusenteId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var turnoReemplazoIdsSwap = normalizedItems
                .Where(item =>
                    string.Equals(item.ModoReemplazo, "SWAP", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(item.TurnoReemplazoId))
                .Select(item => item.TurnoReemplazoId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var turnoIds = turnoAusenteIds
                .Concat(turnoReemplazoIdsSwap)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var turnos = await _db.RegistroTurnos
                .AsNoTracking()
                .Where(rt => turnoIds.Contains(rt.TurnoId))
                .ToDictionaryAsync(rt => rt.TurnoId);

            if (turnos.Count != turnoIds.Count)
            {
                return BadRequest(new { message = "Uno o mas turnos seleccionados no existen." });
            }

            var personaIdsEnTurnos = turnos.Values
                .Select(rt => rt.PersonaId)
                .Concat(normalizedItems
                    .Where(item => string.Equals(item.ModoReemplazo, "NEW_SHIFT", StringComparison.Ordinal))
                    .Where(item => !string.IsNullOrWhiteSpace(item.PersonaReemplazoId))
                    .Select(item => item.PersonaReemplazoId!))
                .Distinct()
                .ToList();

            var equipoByPersona = await _db.Personas
                .AsNoTracking()
                .Where(p => personaIdsEnTurnos.Contains(p.PersonaId))
                .Select(p => new { p.PersonaId, p.EquipoId })
                .ToDictionaryAsync(p => p.PersonaId, p => p.EquipoId);

            var potentialNewShiftKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in normalizedItems)
            {
                var turnoAusenteId = item.TurnoAusenteId;
                var turnoAusente = turnos[turnoAusenteId];

                if (!string.Equals(turnoAusente.PersonaId, solicitud.PersonaSolicitanteId, StringComparison.Ordinal))
                {
                    return BadRequest(new { message = "El turno ausente no pertenece a la persona de la calamidad." });
                }

                if (turnoAusente.FechaTurno < calamidad.FechaInicio || turnoAusente.FechaTurno > calamidad.FechaFin)
                {
                    return BadRequest(new { message = "El turno ausente esta fuera del rango de la calamidad." });
                }

                if (string.Equals(item.ModoReemplazo, "SWAP", StringComparison.Ordinal))
                {
                    var turnoReemplazoId = item.TurnoReemplazoId!;
                    var turnoReemplazo = turnos[turnoReemplazoId];

                    if (!equipoByPersona.TryGetValue(turnoReemplazo.PersonaId, out var replacementEquipoId) ||
                        !string.Equals(replacementEquipoId, targetEquipoId, StringComparison.Ordinal))
                    {
                        return BadRequest(new { message = "El reemplazo debe pertenecer al mismo equipo." });
                    }

                    if (string.Equals(turnoReemplazo.PersonaId, turnoAusente.PersonaId, StringComparison.Ordinal))
                    {
                        return BadRequest(new { message = "La misma persona ausente no puede ser su propio reemplazo." });
                    }

                    replacementsToSave.Add(new CalamidadReemplazo
                    {
                        CalamidadReemplazoId = Guid.NewGuid().ToString("N"),
                        SolicitudId = request.SolicitudId,
                        TurnoAusenteId = turnoAusenteId,
                        TurnoReemplazoId = turnoReemplazoId,
                        ModoReemplazo = "SWAP"
                    });
                    continue;
                }

                var personaReemplazoId = item.PersonaReemplazoId!;
                if (string.Equals(personaReemplazoId, turnoAusente.PersonaId, StringComparison.Ordinal))
                {
                    return BadRequest(new { message = "La misma persona ausente no puede ser su propio reemplazo." });
                }

                if (!equipoByPersona.TryGetValue(personaReemplazoId, out var replacementEquipoIdNew) ||
                    !string.Equals(replacementEquipoIdNew, targetEquipoId, StringComparison.Ordinal))
                {
                    return BadRequest(new { message = "El reemplazo debe pertenecer al mismo equipo." });
                }

                var newShiftKey = $"{personaReemplazoId}|{turnoAusente.FechaTurno:yyyy-MM-dd}|{turnoAusente.TipoTurnoId}|{turnoAusente.GrupoId}";
                if (!potentialNewShiftKeys.Add(newShiftKey))
                {
                    return BadRequest(new { message = "No se pueden crear dos turnos nuevos identicos para la misma persona en la misma fecha." });
                }

                var turnoDuplicado = await _db.RegistroTurnos
                    .AsNoTracking()
                    .AnyAsync(rt =>
                        rt.PersonaId == personaReemplazoId &&
                        rt.FechaTurno == turnoAusente.FechaTurno &&
                        rt.TipoTurnoId == turnoAusente.TipoTurnoId &&
                        rt.GrupoId == turnoAusente.GrupoId);

                if (turnoDuplicado)
                {
                    return BadRequest(new { message = "La persona seleccionada ya tiene ese turno registrado. Usa intercambio o elige otra persona." });
                }

                var newTurnoId = Guid.NewGuid().ToString("N");
                generatedTurnos.Add(new RegistroTurno
                {
                    TurnoId = newTurnoId,
                    PersonaId = personaReemplazoId,
                    GrupoId = turnoAusente.GrupoId,
                    TipoTurnoId = turnoAusente.TipoTurnoId,
                    FechaTurno = turnoAusente.FechaTurno
                });

                replacementsToSave.Add(new CalamidadReemplazo
                {
                    CalamidadReemplazoId = Guid.NewGuid().ToString("N"),
                    SolicitudId = request.SolicitudId,
                    TurnoAusenteId = turnoAusenteId,
                    TurnoReemplazoId = newTurnoId,
                    ModoReemplazo = "NEW_SHIFT"
                });
            }
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var existing = await _db.CalamidadReemplazos
                .Where(item => item.SolicitudId == request.SolicitudId)
                .ToListAsync();

            var oldGeneratedTurnoIds = existing
                .Where(item => string.Equals(item.ModoReemplazo, "NEW_SHIFT", StringComparison.Ordinal))
                .Select(item => item.TurnoReemplazoId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (existing.Count > 0)
            {
                _db.CalamidadReemplazos.RemoveRange(existing);
            }

            if (oldGeneratedTurnoIds.Count > 0)
            {
                var oldGeneratedTurnos = await _db.RegistroTurnos
                    .Where(rt => oldGeneratedTurnoIds.Contains(rt.TurnoId))
                    .ToListAsync();
                if (oldGeneratedTurnos.Count > 0)
                {
                    _db.RegistroTurnos.RemoveRange(oldGeneratedTurnos);
                }
            }

            if (generatedTurnos.Count > 0)
            {
                _db.RegistroTurnos.AddRange(generatedTurnos);
            }

            if (replacementsToSave.Count > 0)
            {
                _db.CalamidadReemplazos.AddRange(replacementsToSave);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = "No se pudieron guardar los reemplazos de calamidad." });
        }

        return Ok(new
        {
            message = replacementsToSave.Count == 0
                ? "Reemplazos eliminados."
                : $"Reemplazos guardados: {replacementsToSave.Count}.",
            saved = replacementsToSave.Count,
            createdShifts = generatedTurnos.Count
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve([FromBody] SolicitudDecisionRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SolicitudId))
        {
            return BadRequest(new { message = "Solicitud invalida." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var personaId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.Borrado)
            .Select(p => p.PersonaId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(personaId))
        {
            return BadRequest(new { message = "No se encontro la persona aprobadora." });
        }

        var role = User.IsInRole("SuperAdmin") || User.IsInRole("Admin")
            ? "Admin"
            : User.IsInRole("Lider")
                ? "Lider"
                : "Usuario";

        var result = await _solicitudService.ApproveAsync(request.SolicitudId, personaId, role, userId);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error ?? "No se pudo aprobar la solicitud." });
        }

        var payload = await BuildDecisionPayloadAsync(request.SolicitudId);
        return Ok(payload);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject([FromBody] SolicitudDecisionRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SolicitudId))
        {
            return BadRequest(new { message = "Solicitud invalida." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var personaId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.Borrado)
            .Select(p => p.PersonaId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(personaId))
        {
            return BadRequest(new { message = "No se encontro la persona aprobadora." });
        }

        var role = User.IsInRole("SuperAdmin") || User.IsInRole("Admin")
            ? "Admin"
            : User.IsInRole("Lider")
                ? "Lider"
                : "Usuario";

        var result = await _solicitudService.RejectAsync(request.SolicitudId, personaId, role, userId);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error ?? "No se pudo rechazar la solicitud." });
        }

        var payload = await BuildDecisionPayloadAsync(request.SolicitudId);
        return Ok(payload);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel([FromBody] SolicitudDecisionRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SolicitudId))
        {
            return BadRequest(new { message = "Solicitud invalida." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var personaId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.Borrado)
            .Select(p => p.PersonaId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(personaId))
        {
            return BadRequest(new { message = "No se encontro la persona ejecutora." });
        }

        var role = User.IsInRole("SuperAdmin") || User.IsInRole("Admin")
            ? "Admin"
            : User.IsInRole("Lider")
                ? "Lider"
                : "Usuario";

        var result = await _solicitudService.CancelAsync(request.SolicitudId, personaId, role, userId);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error ?? "No se pudo cancelar la solicitud." });
        }

        var payload = await BuildDecisionPayloadAsync(request.SolicitudId);
        return Ok(payload);
    }

    private async Task<object?> BuildDecisionPayloadAsync(string solicitudId)
    {
        var solicitud = await _db.Solicitudes
            .AsNoTracking()
            .Include(s => s.PersonaAprobador1)
            .Include(s => s.PersonaAprobador2)
            .SingleOrDefaultAsync(s => s.SolicitudId == solicitudId);

        if (solicitud == null)
        {
            return null;
        }

        var (status, statusCode) = solicitud.EstadoSolicitud switch
        {
            SolicitudEstado.AprobadoFinal => ("Aprobado", "approved"),
            SolicitudEstado.Rechazado => ("Rechazado", "rejected"),
            SolicitudEstado.Cancelado => ("Cancelado", "cancelled"),
            SolicitudEstado.AprobadoLider => ("En aprobacion", "inreview"),
            _ => ("Pendiente", "pending")
        };

        string FormatDate(DateTime? date)
        {
            if (!date.HasValue)
            {
                return string.Empty;
            }

            var monthNames = new[]
            {
                "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
            };
            var value = date.Value;
            return $"{value.Day} {monthNames[value.Month - 1]} {value.Year}";
        }

        return new
        {
            status,
            statusCode,
            aprobador1 = solicitud.PersonaAprobador1?.NombreCompleto ?? string.Empty,
            aprobador1Fecha = FormatDate(solicitud.FechaAprobacion1),
            aprobador2 = solicitud.PersonaAprobador2?.NombreCompleto ?? string.Empty,
            aprobador2Fecha = FormatDate(solicitud.FechaAprobacion2)
        };
    }

    private async Task<string?> ResolveOrCreateCalamidadTipoSolicitudIdAsync()
    {
        var existingId = await _db.TipoSolicitudes
            .AsNoTracking()
            .Where(t =>
                t.TipoSolicitudId.ToUpper().StartsWith("CAL") ||
                t.NombreSolicitud.ToUpper().Contains("CALAM"))
            .OrderBy(t => t.TipoSolicitudId)
            .Select(t => t.TipoSolicitudId)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(existingId))
        {
            return existingId;
        }

        const string baseId = "CAL";
        var tipoId = baseId;
        var existsById = await _db.TipoSolicitudes
            .AsNoTracking()
            .AnyAsync(t => t.TipoSolicitudId == tipoId);

        if (existsById)
        {
            tipoId = $"CAL-{Guid.NewGuid():N}".Substring(0, 10).ToUpperInvariant();
        }

        _db.TipoSolicitudes.Add(new TipoSolicitud
        {
            TipoSolicitudId = tipoId,
            NombreSolicitud = "Calamidad"
        });

        await _db.SaveChangesAsync();
        return tipoId;
    }

    private static string BuildPersonaName(
        string nombre,
        string? segundoNombre,
        string apellido,
        string? segundoApellido)
    {
        return string.Join(" ", new[] { nombre, segundoNombre, apellido, segundoApellido }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string NormalizeCalamidadReemplazoModo(
        string? rawMode,
        string? turnoReemplazoId,
        string? personaReemplazoId)
    {
        var mode = (rawMode ?? string.Empty).Trim().ToUpperInvariant();
        if (mode == "NEW_SHIFT" || mode == "TURNO_NUEVO")
        {
            return "NEW_SHIFT";
        }

        if (mode == "SWAP" || mode == "INTERCAMBIO")
        {
            return "SWAP";
        }

        var hasTurno = !string.IsNullOrWhiteSpace(turnoReemplazoId);
        var hasPersona = !string.IsNullOrWhiteSpace(personaReemplazoId);
        return hasPersona && !hasTurno ? "NEW_SHIFT" : "SWAP";
    }

    private Task<bool> IsTurnoAlreadyLinkedToActiveCalamidadReplacementAsync(string turnoId)
    {
        return (
            from reemplazo in _db.CalamidadReemplazos.AsNoTracking()
            join solicitud in _db.Solicitudes.AsNoTracking() on reemplazo.SolicitudId equals solicitud.SolicitudId
            where solicitud.EstadoSolicitud != SolicitudEstado.Rechazado
                && solicitud.EstadoSolicitud != SolicitudEstado.Cancelado
                && (reemplazo.TurnoAusenteId == turnoId || reemplazo.TurnoReemplazoId == turnoId)
            select reemplazo.CalamidadReemplazoId)
            .AnyAsync();
    }
}
