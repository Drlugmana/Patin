using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Security;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Controllers;

[Authorize(Policy = "UserAbove")]
public class CalendarioController : Controller
{
    private readonly IEquipoService _equipoService;
    private readonly ITipoTurnoService _tipoTurnoService;
    private readonly IPermisoAccesoResolver _permisoAccesoResolver;
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public CalendarioController(
        IEquipoService equipoService,
        ITipoTurnoService tipoTurnoService,
        IPermisoAccesoResolver permisoAccesoResolver,
        ApplicationDbContext db,
        IMemoryCache cache)
    {
        _equipoService = equipoService;
        _tipoTurnoService = tipoTurnoService;
        _permisoAccesoResolver = permisoAccesoResolver;
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? equipoId)
    {
        var equipos = await _equipoService.GetAllAsync();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var effectivePermissions = string.IsNullOrWhiteSpace(userId)
            ? Array.Empty<string>()
            : await _permisoAccesoResolver.ObtenerPermisosEfectivosAsync(userId);
        var permissionSet = new HashSet<string>(effectivePermissions, StringComparer.OrdinalIgnoreCase);
        var isSuperAdminOrAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var isLider = User.IsInRole("Lider");

        var canCreateTurno = permissionSet.Contains(PermisosAccesoCodigos.RegistroTurnoCrear);
        var canEditTurno = permissionSet.Contains(PermisosAccesoCodigos.RegistroTurnoEditar);
        var canDeleteTurno = permissionSet.Contains(PermisosAccesoCodigos.RegistroTurnoEliminar);
        var canCreateCambioTurno = permissionSet.Contains(PermisosAccesoCodigos.CambioTurnoCrear);
        var canCreatePermiso = permissionSet.Contains(PermisosAccesoCodigos.PermisoCrear);
        var canCreateCalamidad =
            permissionSet.Contains(PermisosAccesoCodigos.RegistroTurnoEditar) ||
            permissionSet.Contains(PermisosAccesoCodigos.SolicitudAprobarCambioTurno);

        var hasCalendarPermissionConfigured =
            canCreateTurno ||
            canEditTurno ||
            canDeleteTurno ||
            canCreateCambioTurno ||
            canCreatePermiso ||
            canCreateCalamidad;

        // Compatibility fallback: keep calendar operable for elevated roles when calendar permissions
        // have not been configured yet in permission tables.
        if (!hasCalendarPermissionConfigured)
        {
            canCreateTurno = isSuperAdminOrAdmin || isLider;
            canEditTurno = isSuperAdminOrAdmin;
            canDeleteTurno = isSuperAdminOrAdmin || isLider;
            canCreateCambioTurno = isSuperAdminOrAdmin || isLider;
            canCreatePermiso = true;
            canCreateCalamidad = isSuperAdminOrAdmin || isLider;
        }

        // Business rule: SuperAdmin/Admin always have full shift management.
        if (isSuperAdminOrAdmin)
        {
            canCreateTurno = true;
            canEditTurno = true;
            canDeleteTurno = true;
            canCreateCambioTurno = true;
            canCreateCalamidad = true;
        }
        var currentPersonaId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.Borrado)
            .Select(p => p.PersonaId)
            .FirstOrDefaultAsync();
        var resolvedEquipoId = await ResolveEquipoIdAsync(equipoId);
        var selectedId = !string.IsNullOrWhiteSpace(resolvedEquipoId)
            ? resolvedEquipoId
            : equipos.FirstOrDefault()?.EquipoId;

        var personasQuery = _db.Personas
            .AsNoTracking()
            .Where(p => !p.Borrado);
        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            personasQuery = personasQuery.Where(p => p.EquipoId == selectedId);
        }

        var personas = await personasQuery
            .OrderBy(p => p.Apellido)
            .ThenBy(p => p.Nombre)
            .ToListAsync();

        var grupos = string.IsNullOrWhiteSpace(selectedId)
            ? new List<Grupo>()
            : await _db.Grupos
                .AsNoTracking()
                .Where(g => g.EquipoId == selectedId && g.Activo)
                .OrderBy(g => g.NombreGrupo)
                .ToListAsync();

        var personaIds = personas.Select(p => p.PersonaId).ToList();
        var personaGrupos = personaIds.Count == 0
            ? new List<(string PersonaId, string GrupoId)>()
            : (await _db.PersonaGrupos
                .AsNoTracking()
                .Where(pg => personaIds.Contains(pg.PersonaId))
                .Select(pg => new { pg.PersonaId, pg.GrupoId })
                .ToListAsync())
            .Select(pg => (pg.PersonaId, pg.GrupoId))
            .ToList();

        var personaGroupsLookup = personaGrupos
            .GroupBy(x => x.PersonaId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.GrupoId).Distinct().ToArray());

        var tiposTurno = await _tipoTurnoService.GetAllAsync();
        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            // Manual assignment in the calendar uses this list to populate and auto-select shift types.
            // Keep every shift mapped to the team, even if it still has no planification rows.
            var mappedTurnos = await _db.EquipoTipoTurnos
                .AsNoTracking()
                .Where(et => et.EquipoId == selectedId)
                .Select(et => et.TipoTurnoId)
                .ToListAsync();
            if (mappedTurnos.Count > 0)
            {
                tiposTurno = tiposTurno.Where(t => mappedTurnos.Contains(t.TipoTurnoId)).ToList();
            }
        }

        var personaItems = personas
            .Select(p => new CalendarioPersonaItem
            {
                Value = p.PersonaId,
                Text = BuildPersonaName(p.Nombre, p.SegundoNombre, p.Apellido, p.SegundoApellido),
                Color = p.ColorUsuario,
                GrupoIds = personaGroupsLookup.TryGetValue(p.PersonaId, out var grupoIds)
                    ? grupoIds
                    : Array.Empty<string>()
            })
            .OrderBy(item => item.Text, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var model = new CalendarioViewModel
        {
            SelectedEquipoId = selectedId,
            CurrentPersonaId = currentPersonaId,
            CanCreateTurno = canCreateTurno,
            CanEditTurno = canEditTurno,
            CanDeleteTurno = canDeleteTurno,
            CanCreateCambioTurno = canCreateCambioTurno,
            CanCreatePermiso = canCreatePermiso,
            CanCreateCalamidad = canCreateCalamidad,
            Equipos = equipos.Select(e => new SelectListItem
            {
                Value = e.EquipoId,
                Text = e.NombreEquipo,
                Selected = e.EquipoId == selectedId
            }),
            Personas = personaItems,
            Grupos = grupos.Select(g => new SelectListItem
            {
                Value = g.GrupoId,
                Text = g.NombreGrupo
            }),
            TiposTurno = tiposTurno.Select(t => new SelectListItem
            {
                Value = t.TipoTurnoId,
                Text = $"{t.NombreTurno} ({t.HoraInicio:HH\\:mm}-{t.HoraFin:HH\\:mm})"
            })
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> WeekData(string? equipoId, DateOnly weekStart, bool clearPreview = false)
    {
        var resolvedEquipoId = await ResolveEquipoIdAsync(equipoId);
        if (string.IsNullOrWhiteSpace(resolvedEquipoId))
        {
            return Json(new
            {
                shifts = Array.Empty<object>(),
                assignments = Array.Empty<object>(),
                holidays = Array.Empty<object>(),
                vacations = Array.Empty<object>()
            });
        }

        if (clearPreview)
        {
            ClearPreviewKeysForUser();
        }

        var mappedTurnoIds = await _db.EquipoTipoTurnos
            .AsNoTracking()
            .Where(et => et.EquipoId == resolvedEquipoId)
            .Select(et => et.TipoTurnoId)
            .ToListAsync();

        var weekEnd = weekStart.AddDays(8);
        var feriadosByDate = await GetFeriadosByDateAsync(weekStart, weekEnd);
        var (vacacionesByDate, calamidadesByDate) = await GetAusenciasByDateAsync(resolvedEquipoId, weekStart, weekEnd);
        var tipoTurnos = await _db.TipoTurnos
            .AsNoTracking()
            .Where(t => mappedTurnoIds.Count == 0 || mappedTurnoIds.Contains(t.TipoTurnoId))
            .OrderBy(t => t.HoraInicio)
            .ToListAsync();

        var registros = await (
            from rt in _db.RegistroTurnos.AsNoTracking()
            join p in _db.Personas.AsNoTracking() on rt.PersonaId equals p.PersonaId
            join g in _db.Grupos.AsNoTracking() on rt.GrupoId equals g.GrupoId into grupoJoin
            from g in grupoJoin.DefaultIfEmpty()
            where p.EquipoId == resolvedEquipoId
                && rt.FechaTurno >= weekStart
                && rt.FechaTurno <= weekEnd
                && !rt.NoLaboradoPorFeriado
                && (mappedTurnoIds.Count == 0 || mappedTurnoIds.Contains(rt.TipoTurnoId))
            orderby g.NombreGrupo, p.Apellido, rt.FechaTurno
            select new
            {
                rt.TurnoId,
                rt.FechaTurno,
                rt.TipoTurnoId,
                rt.GrupoId,
                rt.PersonaId,
                rt.EsTurnoExtra,
                p.Nombre,
                p.SegundoNombre,
                p.Apellido,
                p.SegundoApellido,
                p.Ultimatix,
                p.ColorUsuario,
                GrupoNombre = g != null ? g.NombreGrupo : null
            }).ToListAsync();

        var turnoIds = registros
            .Select(r => r.TurnoId)
            .Distinct()
            .ToList();

        var cambioVisualByTurno = new Dictionary<string, (string State, string Label, int Priority, DateTime UpdatedAt, string RelatedTurnoId, string Role)>(StringComparer.OrdinalIgnoreCase);
        if (turnoIds.Count > 0)
        {
            var cambiosTurno = await (
                from c in _db.CambiosTurno.AsNoTracking()
                join s in _db.Solicitudes.AsNoTracking() on c.SolicitudId equals s.SolicitudId
                where (turnoIds.Contains(c.TurnoOrigenId) || turnoIds.Contains(c.TurnoDestinoId))
                select new
                {
                    c.TurnoOrigenId,
                    c.TurnoDestinoId,
                    s.EstadoSolicitud,
                    s.ActualizadoEn
                }).ToListAsync();

            static (string? state, string? label, int priority) GetCambioVisual(SolicitudEstado estado)
            {
                return estado switch
                {
                    SolicitudEstado.Pendiente => ("requested", "Cambio solicitado", 3),
                    SolicitudEstado.AprobadoLider => ("inreview", "Cambio con 1 aprobacion", 2),
                    SolicitudEstado.AprobadoFinal => ("approved", "Cambio aprobado", 1),
                    SolicitudEstado.Rechazado => ("rejected", "Cambio rechazado", 3),
                    SolicitudEstado.Cancelado => ("cancelled", "Cambio cancelado", 3),
                    _ => (null, null, 0)
                };
            }

            void UpsertVisual(string turnoId, string relatedTurnoId, string role, SolicitudEstado estado, DateTime updatedAt)
            {
                var visual = GetCambioVisual(estado);
                if (string.IsNullOrWhiteSpace(turnoId) ||
                    string.IsNullOrWhiteSpace(visual.state) ||
                    string.IsNullOrWhiteSpace(visual.label))
                {
                    return;
                }

                if (!cambioVisualByTurno.TryGetValue(turnoId, out var current))
                {
                    cambioVisualByTurno[turnoId] = (
                        visual.state!,
                        visual.label!,
                        visual.priority,
                        updatedAt,
                        relatedTurnoId ?? string.Empty,
                        role ?? string.Empty);
                    return;
                }

                if (visual.priority > current.Priority ||
                    (visual.priority == current.Priority && updatedAt > current.UpdatedAt))
                {
                    cambioVisualByTurno[turnoId] = (
                        visual.state!,
                        visual.label!,
                        visual.priority,
                        updatedAt,
                        relatedTurnoId ?? string.Empty,
                        role ?? string.Empty);
                }
            }

            foreach (var cambio in cambiosTurno)
            {
                UpsertVisual(
                    cambio.TurnoOrigenId,
                    cambio.TurnoDestinoId,
                    "origen",
                    cambio.EstadoSolicitud,
                    cambio.ActualizadoEn);
                UpsertVisual(
                    cambio.TurnoDestinoId,
                    cambio.TurnoOrigenId,
                    "destino",
                    cambio.EstadoSolicitud,
                    cambio.ActualizadoEn);
            }
        }

        var calamidadesActivas = await (
            from c in _db.Calamidades.AsNoTracking()
            join s in _db.Solicitudes.AsNoTracking() on c.SolicitudId equals s.SolicitudId
            join p in _db.Personas.AsNoTracking() on s.PersonaSolicitanteId equals p.PersonaId
            where p.EquipoId == resolvedEquipoId
                && s.EstadoSolicitud == SolicitudEstado.AprobadoFinal
                && c.FechaInicio <= weekEnd
                && c.FechaFin >= weekStart
            select new
            {
                c.SolicitudId,
                PersonaAusenteId = s.PersonaSolicitanteId,
                c.FechaInicio,
                c.FechaFin
            })
            .ToListAsync();

        var calamidadRangesByPersona = calamidadesActivas
            .GroupBy(x => x.PersonaAusenteId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => (x.FechaInicio, x.FechaFin)).ToList(),
                StringComparer.OrdinalIgnoreCase);

        bool IsTurnoAusente(string personaId, DateOnly fechaTurno)
        {
            if (string.IsNullOrWhiteSpace(personaId) ||
                !calamidadRangesByPersona.TryGetValue(personaId, out var ranges))
            {
                return false;
            }

            return ranges.Any(range => fechaTurno >= range.FechaInicio && fechaTurno <= range.FechaFin);
        }

        var turnosAusentesSet = registros
            .Where(r => IsTurnoAusente(r.PersonaId, r.FechaTurno))
            .Select(r => r.TurnoId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var calamidadSolicitudIdsActivas = calamidadesActivas
            .Select(x => x.SolicitudId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var reemplazoByTurnoAusente = new Dictionary<string, (string PersonaId, string NombreCompleto, string? Ultimatix, string? ColorUsuario, string ModoReemplazo, string TurnoReemplazoId, DateTime UpdatedAt)>(StringComparer.OrdinalIgnoreCase);
        if (turnosAusentesSet.Count > 0 && calamidadSolicitudIdsActivas.Count > 0)
        {
            var calamidadReemplazos = await (
                from r in _db.CalamidadReemplazos.AsNoTracking()
                join s in _db.Solicitudes.AsNoTracking() on r.SolicitudId equals s.SolicitudId
                join turnoReemplazo in _db.RegistroTurnos.AsNoTracking() on r.TurnoReemplazoId equals turnoReemplazo.TurnoId
                join personaReemplazo in _db.Personas.AsNoTracking() on turnoReemplazo.PersonaId equals personaReemplazo.PersonaId
                where turnosAusentesSet.Contains(r.TurnoAusenteId)
                    && calamidadSolicitudIdsActivas.Contains(r.SolicitudId)
                select new
                {
                    r.TurnoAusenteId,
                    r.TurnoReemplazoId,
                    r.ModoReemplazo,
                    s.ActualizadoEn,
                    PersonaReemplazoId = personaReemplazo.PersonaId,
                    personaReemplazo.Nombre,
                    personaReemplazo.SegundoNombre,
                    personaReemplazo.Apellido,
                    personaReemplazo.SegundoApellido,
                    personaReemplazo.Ultimatix,
                    personaReemplazo.ColorUsuario
                })
                .ToListAsync();

            foreach (var reemplazo in calamidadReemplazos)
            {
                var replacementName = BuildPersonaName(
                    reemplazo.Nombre,
                    reemplazo.SegundoNombre,
                    reemplazo.Apellido,
                    reemplazo.SegundoApellido);

                if (!reemplazoByTurnoAusente.TryGetValue(reemplazo.TurnoAusenteId, out var current) ||
                    reemplazo.ActualizadoEn > current.UpdatedAt)
                {
                    reemplazoByTurnoAusente[reemplazo.TurnoAusenteId] = (
                        reemplazo.PersonaReemplazoId,
                        replacementName,
                        reemplazo.Ultimatix,
                        reemplazo.ColorUsuario,
                        string.IsNullOrWhiteSpace(reemplazo.ModoReemplazo) ? "SWAP" : reemplazo.ModoReemplazo,
                        reemplazo.TurnoReemplazoId,
                        reemplazo.ActualizadoEn);
                }
            }
        }

        var turnosReemplazoNuevosSet = reemplazoByTurnoAusente.Values
            .Where(item => string.Equals(item.ModoReemplazo, "NEW_SHIFT", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.TurnoReemplazoId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var turnosReemplazoSwapSet = reemplazoByTurnoAusente.Values
            .Where(item => string.Equals(item.ModoReemplazo, "SWAP", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.TurnoReemplazoId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var shiftItems = tipoTurnos.Select(t => new
        {
            id = t.TipoTurnoId,
            label = $"{t.HoraInicio:HH\\:mm} - {t.HoraFin:HH\\:mm}",
            shortLabel = t.NombreTurno
        });

        var weekStartDate = weekStart.ToDateTime(TimeOnly.MinValue);
        var assignmentItems = registros
            .Select(r =>
            {
                var offset = (r.FechaTurno.ToDateTime(TimeOnly.MinValue) - weekStartDate).Days;
                cambioVisualByTurno.TryGetValue(r.TurnoId, out var cambioVisual);
                var include = true;
                var personaId = r.PersonaId;
                var title = BuildPersonaName(r.Nombre, r.SegundoNombre, r.Apellido, r.SegundoApellido);
                var meta = r.Ultimatix;
                var color = r.ColorUsuario;
                string? calamidadReemplazaNombre = null;

                if (turnosReemplazoNuevosSet.Contains(r.TurnoId))
                {
                    include = false;
                }
                else if (turnosReemplazoSwapSet.Contains(r.TurnoId) && !turnosAusentesSet.Contains(r.TurnoId))
                {
                    // SWAP means the source shift is moved to cover the absent slot,
                    // so we hide it from its original cell to avoid duplicates.
                    include = false;
                }
                else if (turnosAusentesSet.Contains(r.TurnoId))
                {
                    if (!reemplazoByTurnoAusente.TryGetValue(r.TurnoId, out var replacementData))
                    {
                        include = false;
                    }
                    else
                    {
                        personaId = replacementData.PersonaId;
                        title = replacementData.NombreCompleto;
                        meta = replacementData.Ultimatix;
                        color = replacementData.ColorUsuario;
                        calamidadReemplazaNombre = BuildPersonaName(
                            r.Nombre,
                            r.SegundoNombre,
                            r.Apellido,
                            r.SegundoApellido);
                    }
                }

                return new
                {
                    include,
                    id = r.TurnoId,
                    personaId,
                    grupoId = r.GrupoId,
                    title,
                    meta,
                    offset,
                    shift = r.TipoTurnoId,
                    color,
                    group = r.GrupoNombre,
                    cambioState = string.IsNullOrWhiteSpace(cambioVisual.State) ? null : cambioVisual.State,
                    cambioLabel = string.IsNullOrWhiteSpace(cambioVisual.Label) ? null : cambioVisual.Label,
                    cambioRelatedTurnoId = string.IsNullOrWhiteSpace(cambioVisual.RelatedTurnoId) ? null : cambioVisual.RelatedTurnoId,
                    cambioRole = string.IsNullOrWhiteSpace(cambioVisual.Role) ? null : cambioVisual.Role,
                    esTurnoExtra = r.EsTurnoExtra,
                    calamidadReemplazaNombre
                };
            })
            .Where(a => a.include && a.offset >= 0 && a.offset <= 8)
            .Select(a => new
            {
                a.id,
                a.personaId,
                a.grupoId,
                a.title,
                a.meta,
                day = $"d{a.offset}",
                a.shift,
                a.color,
                a.group,
                a.cambioState,
                a.cambioLabel,
                a.cambioRelatedTurnoId,
                a.cambioRole,
                a.esTurnoExtra,
                a.calamidadReemplazaNombre
            })
            .ToList();

        var absentAssignmentItems = registros
            .Where(r => turnosAusentesSet.Contains(r.TurnoId))
            .Select(r =>
            {
                var offset = (r.FechaTurno.ToDateTime(TimeOnly.MinValue) - weekStartDate).Days;
                if (offset < 0 || offset > 8)
                {
                    return null;
                }

                reemplazoByTurnoAusente.TryGetValue(r.TurnoId, out var replacementData);

                return new
                {
                    id = $"abs-{r.TurnoId}",
                    turnoAusenteId = r.TurnoId,
                    personaId = r.PersonaId,
                    grupoId = r.GrupoId,
                    title = BuildPersonaName(r.Nombre, r.SegundoNombre, r.Apellido, r.SegundoApellido),
                    meta = r.Ultimatix,
                    day = $"d{offset}",
                    shift = r.TipoTurnoId,
                    color = r.ColorUsuario,
                    group = r.GrupoNombre,
                    calamidadAusente = true,
                    calamidadReemplazadoPorNombre = replacementData.NombreCompleto ?? string.Empty
                };
            })
            .Where(a => a != null)
            .Select(a => a!)
            .ToList();

        var personaIds = registros.Select(r => r.PersonaId).Distinct().ToList();
        var personaGroups = await (
            from pg in _db.PersonaGrupos.AsNoTracking()
            where personaIds.Contains(pg.PersonaId)
            select new { pg.PersonaId, pg.GrupoId })
            .ToListAsync();

        var personaGroupsLookup = personaGroups
            .GroupBy(x => x.PersonaId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.GrupoId).Distinct().ToArray());

        var previewAssignments = new List<object>();
        var previewActive = false;
        var previewKey = GetPreviewCacheKey(resolvedEquipoId);
        if (_cache.TryGetValue(previewKey, out List<TurnoGeneradoPreview>? previewList)
            && previewList != null
            && previewList.Count > 0)
        {
            previewActive = true;
            var previewWeek = previewList
                .Where(p => p.FechaTurno >= weekStart && p.FechaTurno <= weekEnd && !p.NoLaboradoPorFeriado)
                .ToList();

            if (previewWeek.Count > 0)
            {
                var previewPersonaIds = previewWeek.Select(p => p.PersonaId).Distinct().ToList();
                var previewPersonas = await _db.Personas
                    .AsNoTracking()
                    .Where(p => previewPersonaIds.Contains(p.PersonaId) && !p.Borrado)
                    .Select(p => new
                    {
                        p.PersonaId,
                        p.Nombre,
                        p.SegundoNombre,
                        p.Apellido,
                        p.SegundoApellido,
                        p.Ultimatix,
                        p.ColorUsuario
                    })
                    .ToListAsync();

                var personaLookup = previewPersonas.ToDictionary(p => p.PersonaId, p => p);

                var previewGrupoIds = previewWeek.Select(p => p.GrupoId).Distinct().ToList();
                var grupoLookup = await _db.Grupos
                    .AsNoTracking()
                    .Where(g => previewGrupoIds.Contains(g.GrupoId))
                    .Select(g => new { g.GrupoId, g.NombreGrupo })
                    .ToDictionaryAsync(g => g.GrupoId, g => g.NombreGrupo);

                previewAssignments = previewWeek
                    .Select(p =>
                    {
                        var offset = (p.FechaTurno.ToDateTime(TimeOnly.MinValue) - weekStartDate).Days;
                        if (offset < 0 || offset > 8)
                        {
                            return null;
                        }
                        personaLookup.TryGetValue(p.PersonaId, out var persona);
                        grupoLookup.TryGetValue(p.GrupoId, out var grupoNombre);
                        return new
                        {
                            id = $"preview-{p.PersonaId}-{p.TipoTurnoId}-{p.FechaTurno:yyyyMMdd}-{p.GrupoId}",
                            personaId = p.PersonaId,
                            grupoId = p.GrupoId,
                            title = persona == null
                                ? "Sin asignar"
                                : BuildPersonaName(persona.Nombre, persona.SegundoNombre, persona.Apellido, persona.SegundoApellido),
                            meta = persona?.Ultimatix,
                            day = $"d{offset}",
                            shift = p.TipoTurnoId,
                            color = persona?.ColorUsuario,
                            group = grupoNombre,
                            preview = true
                        };
                    })
                    .Where(p => p != null)
                    .Select(p => (object)p!)
                    .ToList();
            }
        }

        var holidayItems = feriadosByDate
            .OrderBy(x => x.Key)
            .Select(x => new
            {
                date = x.Key.ToString("yyyy-MM-dd"),
                names = x.Value.ToArray()
            })
            .ToList();

        var vacationItems = vacacionesByDate
            .OrderBy(x => x.Key)
            .Select(x => new
            {
                date = x.Key.ToString("yyyy-MM-dd"),
                people = x.Value.ToArray()
            })
            .ToList();

        var calamityItems = calamidadesByDate
            .OrderBy(x => x.Key)
            .Select(x => new
            {
                date = x.Key.ToString("yyyy-MM-dd"),
                people = x.Value.ToArray()
            })
            .ToList();

        return Json(new
        {
            shifts = shiftItems,
            assignments = assignmentItems,
            absentAssignments = absentAssignmentItems,
            personaGroups = personaGroupsLookup,
            previewAssignments,
            previewActive,
            previewEquipoId = previewActive ? resolvedEquipoId : null,
            holidays = holidayItems,
            vacations = vacationItems,
            calamities = calamityItems
        });
    }

    [HttpPost]
    [Authorize(Policy = "AdminAbove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCurrentWeek([FromBody] CalendarioDeleteWeekRequest request)
    {
        if (request == null || request.WeekStart == default)
        {
            return BadRequest(new { message = "Semana invalida." });
        }

        var resolvedEquipoId = await ResolveEquipoIdAsync(request.EquipoId);
        if (string.IsNullOrWhiteSpace(resolvedEquipoId))
        {
            return BadRequest(new { message = "No se pudo identificar el equipo de la semana actual." });
        }

        var monday = NormalizeWeekStartToMonday(request.WeekStart);
        var sunday = monday.AddDays(6);

        var turnosSemana = await (
            from rt in _db.RegistroTurnos
            join p in _db.Personas on rt.PersonaId equals p.PersonaId
            where !p.Borrado
                && p.EquipoId == resolvedEquipoId
                && rt.FechaTurno >= monday
                && rt.FechaTurno <= sunday
            select rt)
            .ToListAsync();

        if (turnosSemana.Count == 0)
        {
            return Ok(new
            {
                deletedCount = 0,
                message = $"No hay turnos guardados del {monday:dd/MM/yyyy} al {sunday:dd/MM/yyyy}."
            });
        }

        var turnoIds = turnosSemana
            .Select(turno => turno.TurnoId)
            .Where(turnoId => !string.IsNullOrWhiteSpace(turnoId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasPermisoActivo = await (
            from permiso in _db.Permisos.AsNoTracking()
            join solicitud in _db.Solicitudes.AsNoTracking() on permiso.SolicitudId equals solicitud.SolicitudId
            where turnoIds.Contains(permiso.RegistroTurnoId)
                && solicitud.EstadoSolicitud != SolicitudEstado.Rechazado
                && solicitud.EstadoSolicitud != SolicitudEstado.Cancelado
            select permiso.PermisoId)
            .AnyAsync();

        if (hasPermisoActivo)
        {
            return BadRequest(new
            {
                message = "La semana contiene turnos con permisos activos asociados y no se puede eliminar completa."
            });
        }

        var hasCambioActivo = await (
            from cambio in _db.CambiosTurno.AsNoTracking()
            join solicitud in _db.Solicitudes.AsNoTracking() on cambio.SolicitudId equals solicitud.SolicitudId
            where (turnoIds.Contains(cambio.TurnoOrigenId) || turnoIds.Contains(cambio.TurnoDestinoId))
                && solicitud.EstadoSolicitud != SolicitudEstado.Rechazado
                && solicitud.EstadoSolicitud != SolicitudEstado.Cancelado
            select cambio.CambioTurnoId)
            .AnyAsync();

        if (hasCambioActivo)
        {
            return BadRequest(new
            {
                message = "La semana contiene turnos con solicitudes de cambio activas y no se puede eliminar completa."
            });
        }

        var hasCalamidadReemplazo = await _db.CalamidadReemplazos
            .AsNoTracking()
            .AnyAsync(reemplazo =>
                turnoIds.Contains(reemplazo.TurnoAusenteId) ||
                turnoIds.Contains(reemplazo.TurnoReemplazoId));

        if (hasCalamidadReemplazo)
        {
            return BadRequest(new
            {
                message = "La semana contiene turnos vinculados a reemplazos por calamidad y no se puede eliminar completa."
            });
        }

        _db.RegistroTurnos.RemoveRange(turnosSemana);

        try
        {
            await _db.SaveChangesAsync();
            return Ok(new
            {
                deletedCount = turnosSemana.Count,
                message = $"Semana eliminada. Se borraron {turnosSemana.Count} turno(s) del {monday:dd/MM/yyyy} al {sunday:dd/MM/yyyy}."
            });
        }
        catch (Exception)
        {
            return BadRequest(new
            {
                message = "No se pudo eliminar la semana actual."
            });
        }
    }

    [HttpPost]
    [Authorize(Policy = "AdminAbove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarGrupoTurno([FromBody] CambiarGrupoTurnoRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TurnoId) || string.IsNullOrWhiteSpace(request.PersonaId))
        {
            return BadRequest(new { message = "Datos invalidos para cambiar grupo." });
        }

        // Get the turno (RegistroTurno)
        var turno = await _db.RegistroTurnos
            .FirstOrDefaultAsync(rt => rt.TurnoId == request.TurnoId && rt.PersonaId == request.PersonaId);

        if (turno == null)
        {
            return BadRequest(new { message = "No se encontro el turno especificado." });
        }

        // Verify persona belongs to current user's accessible equipo
        var persona = await _db.Personas
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonaId == request.PersonaId && !p.Borrado);

        if (persona == null)
        {
            return BadRequest(new { message = "No se encontro la persona especificada." });
        }

        // Verify access to this equipo
        if (!User.IsInRole("SuperAdmin") && !User.IsInRole("Admin"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userPersona = await _db.Personas
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.Borrado);

            if (userPersona?.EquipoId != persona.EquipoId)
            {
                return Forbid();
            }
        }

        // Get the new grupo to verify it exists and persona can use it
        Grupo? nuevoGrupo = null;
        if (!string.IsNullOrWhiteSpace(request.NuevoGrupoId))
        {
            nuevoGrupo = await _db.Grupos
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.GrupoId == request.NuevoGrupoId && g.Activo);

            if (nuevoGrupo == null)
            {
                return BadRequest(new { message = "El nuevo grupo no existe." });
            }

            // Verify persona is part of this grupo or can use it
            var personaGrupo = await _db.PersonaGrupos
                .AsNoTracking()
                .FirstOrDefaultAsync(pg => pg.PersonaId == request.PersonaId && pg.GrupoId == request.NuevoGrupoId);

            if (personaGrupo == null)
            {
                return BadRequest(new { message = "La persona no puede usar este grupo." });
            }
        }

        // Update the turno with new group
        turno.GrupoId = string.IsNullOrWhiteSpace(request.NuevoGrupoId) ? null : request.NuevoGrupoId;

        try
        {
            _db.RegistroTurnos.Update(turno);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Grupo del turno cambio exitosamente." });
        }
        catch (Exception)
        {
            return BadRequest(new { message = "No se pudo cambiar el grupo del turno." });
        }
    }

    private async Task<string?> ResolveEquipoIdAsync(string? equipoId)
    {
        if (User.IsInRole("SuperAdmin") || User.IsInRole("Admin"))
        {
            return equipoId;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var persona = await _db.Personas
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId && !p.Borrado);
        return persona?.EquipoId;
    }

    private static DateOnly NormalizeWeekStartToMonday(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff);
    }

    private static string BuildPersonaName(string nombre, string? segundoNombre, string apellido, string? segundoApellido)
    {
        return string.Join(" ", new[] { nombre, segundoNombre, apellido, segundoApellido }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private string GetPreviewCacheKey(string equipoId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon";
        return $"PlanificacionPreview:{userId}:{equipoId}";
    }

    private string GetPreviewIndexKey()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon";
        return $"PlanificacionPreviewIndex:{userId}";
    }

    private void ClearPreviewKeysForUser()
    {
        var indexKey = GetPreviewIndexKey();
        if (_cache.TryGetValue(indexKey, out HashSet<string>? keys) && keys != null)
        {
            foreach (var key in keys)
            {
                _cache.Remove(key);
            }
        }
        _cache.Remove(indexKey);
    }

    private async Task<Dictionary<DateOnly, List<string>>> GetFeriadosByDateAsync(DateOnly weekStart, DateOnly weekEnd)
    {
        var result = new Dictionary<DateOnly, List<string>>();

        var feriados = await _db.Feriados
            .AsNoTracking()
            .Where(f => f.InicioFeriado <= weekEnd && f.FinFeriado >= weekStart)
            .Select(f => new { f.NombreFeriado, f.InicioFeriado, f.FinFeriado })
            .ToListAsync();

        foreach (var feriado in feriados)
        {
            var inicio = feriado.InicioFeriado > weekStart ? feriado.InicioFeriado : weekStart;
            var fin = feriado.FinFeriado < weekEnd ? feriado.FinFeriado : weekEnd;
            for (var current = inicio; current <= fin; current = current.AddDays(1))
            {
                AddHolidayName(result, current, feriado.NombreFeriado);
            }
        }

        return result;
    }

    private async Task<(Dictionary<DateOnly, List<string>> Vacaciones, Dictionary<DateOnly, List<string>> Calamidades)> GetAusenciasByDateAsync(
        string equipoId,
        DateOnly weekStart,
        DateOnly weekEnd)
    {
        var vacaciones = new Dictionary<DateOnly, List<string>>();
        var calamidades = new Dictionary<DateOnly, List<string>>();

        var vacacionesActivas = await (
            from v in _db.Vacaciones.AsNoTracking()
            join s in _db.Solicitudes.AsNoTracking() on v.SolicitudId equals s.SolicitudId
            join p in _db.Personas.AsNoTracking() on s.PersonaSolicitanteId equals p.PersonaId
            where p.EquipoId == equipoId
                && s.EstadoSolicitud == SolicitudEstado.AprobadoFinal
                && v.FechaInicio <= weekEnd
                && v.FechaFin >= weekStart
            select new
            {
                p.Nombre,
                p.SegundoNombre,
                p.Apellido,
                p.SegundoApellido,
                v.FechaInicio,
                v.FechaFin
            })
            .ToListAsync();

        foreach (var vacacion in vacacionesActivas)
        {
            var inicio = vacacion.FechaInicio > weekStart ? vacacion.FechaInicio : weekStart;
            var fin = vacacion.FechaFin < weekEnd ? vacacion.FechaFin : weekEnd;
            var personaNombre = BuildPersonaName(
                vacacion.Nombre,
                vacacion.SegundoNombre,
                vacacion.Apellido,
                vacacion.SegundoApellido);
            for (var current = inicio; current <= fin; current = current.AddDays(1))
            {
                AddAbsencePerson(vacaciones, current, personaNombre);
            }
        }

        var calamidadesActivas = await (
            from c in _db.Calamidades.AsNoTracking()
            join s in _db.Solicitudes.AsNoTracking() on c.SolicitudId equals s.SolicitudId
            join p in _db.Personas.AsNoTracking() on s.PersonaSolicitanteId equals p.PersonaId
            where p.EquipoId == equipoId
                && s.EstadoSolicitud == SolicitudEstado.AprobadoFinal
                && c.FechaInicio <= weekEnd
                && c.FechaFin >= weekStart
            select new
            {
                p.Nombre,
                p.SegundoNombre,
                p.Apellido,
                p.SegundoApellido,
                c.FechaInicio,
                c.FechaFin
            })
            .ToListAsync();

        foreach (var calamidad in calamidadesActivas)
        {
            var inicio = calamidad.FechaInicio > weekStart ? calamidad.FechaInicio : weekStart;
            var fin = calamidad.FechaFin < weekEnd ? calamidad.FechaFin : weekEnd;
            var personaNombre = BuildPersonaName(
                calamidad.Nombre,
                calamidad.SegundoNombre,
                calamidad.Apellido,
                calamidad.SegundoApellido);
            for (var current = inicio; current <= fin; current = current.AddDays(1))
            {
                AddAbsencePerson(calamidades, current, personaNombre);
            }
        }

        return (vacaciones, calamidades);
    }

    private static void AddHolidayName(Dictionary<DateOnly, List<string>> target, DateOnly date, string? holidayName)
    {
        if (string.IsNullOrWhiteSpace(holidayName))
        {
            return;
        }

        if (!target.TryGetValue(date, out var list))
        {
            list = new List<string>();
            target[date] = list;
        }

        if (!list.Contains(holidayName, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(holidayName);
        }
    }

    private static void AddAbsencePerson(Dictionary<DateOnly, List<string>> target, DateOnly date, string? personaName)
    {
        if (string.IsNullOrWhiteSpace(personaName))
        {
            return;
        }

        if (!target.TryGetValue(date, out var list))
        {
            list = new List<string>();
            target[date] = list;
        }

        if (!list.Contains(personaName, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(personaName);
        }
    }
}
