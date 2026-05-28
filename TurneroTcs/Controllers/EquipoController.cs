using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Data;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Controllers;

[Authorize(Policy = "UserAbove")]
public class EquipoController : Controller
{
    private readonly IEquipoService _equipoService;
    private readonly IGrupoService _grupoService;
    private readonly ITipoTurnoService _tipoTurnoService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<EquipoController> _logger;

    public EquipoController(
        IEquipoService equipoService,
        IGrupoService grupoService,
        ITipoTurnoService tipoTurnoService,
        ApplicationDbContext db,
        ILogger<EquipoController> logger)
    {
        _equipoService = equipoService;
        _grupoService = grupoService;
        _tipoTurnoService = tipoTurnoService;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin,Lider")]
    public async Task<IActionResult> Index()
    {
        var list = (await _equipoService.GetAllAsync()).ToList();

        if (IsLiderOnly())
        {
            var liderEquipoId = await GetCurrentUserEquipoIdAsync();
            if (string.IsNullOrWhiteSpace(liderEquipoId))
            {
                list = new List<Equipo>();
            }
            else
            {
                list = list.Where(e => e.EquipoId == liderEquipoId).ToList();
            }
        }

        var visibleEquipoIds = list.Select(e => e.EquipoId).ToHashSet(StringComparer.Ordinal);
        var grupos = await _grupoService.GetAllAsync();
        var gruposFiltrados = grupos.Where(g => visibleEquipoIds.Contains(g.EquipoId)).ToList();

        _logger.LogDebug("Index equipos: {Count} registros.", list.Count);

        ViewBag.Grupos = gruposFiltrados;
        ViewBag.TipoTurnos = await _tipoTurnoService.GetAllAsync();
        ViewBag.EquipoTipoTurnos = visibleEquipoIds.Count == 0
            ? new Dictionary<string, HashSet<string>>()
            : await _db.EquipoTipoTurnos
                .AsNoTracking()
                .Where(et => visibleEquipoIds.Contains(et.EquipoId))
                .GroupBy(et => et.EquipoId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Select(x => x.TipoTurnoId).ToHashSet());

        return View(list);
    }

    [HttpGet]
    [Authorize(Policy = "AdminAbove")]
    public IActionResult Create()
    {
        var equipoViewModel = new EquipoViewModel();
        return View(equipoViewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminAbove")]
    public async Task<IActionResult> Create(EquipoViewModel vm)
    {
        if (ModelState.IsValid)
        {
            var equipo = new Equipo
            {
                NombreEquipo = vm.NombreEquipo
            };
            var result = await _equipoService.CreateEquipoAsync(equipo);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", result.Error ?? "Ocurrio un error inesperado.");
                _logger.LogWarning("Crear equipo fallo para equipo {NombreEquipo}: {Reason}", vm.NombreEquipo, result.Error);
                return View(vm);
            }
            return RedirectToAction("Index");
        }
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminAbove")]
    public async Task<IActionResult> Patch(string id, EquipoPatchRequest request)
    {
        if (Request.HasFormContentType)
        {
            var activoValue = Request.Form.ContainsKey("Activo") ? Request.Form["Activo"].ToString() : null;
            bool? activo = null;
            if (!string.IsNullOrWhiteSpace(activoValue))
            {
                activo = bool.TryParse(activoValue, out var parsed) ? parsed : activo;
            }

            request = request with
            {
                NombreEquipo = Request.Form.ContainsKey("NombreEquipo") ? Request.Form["NombreEquipo"].ToString() : request.NombreEquipo,
                Activo = Request.Form.ContainsKey("Activo") ? activo : request.Activo,
                TipoGeneracion = Request.Form.ContainsKey("TipoGeneracion") ? Request.Form["TipoGeneracion"].ToString() : request.TipoGeneracion
            };
        }

        var result = await _equipoService.PatchAsync(id, request, GetCurrentUserRole());
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Actualizar equipo fallo.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminAbove")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _equipoService.DeleteAsync(id, GetCurrentUserRole());
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Eliminar equipo fallo.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Admin,Lider")]
    public async Task<IActionResult> UpdateTipoTurnos(string equipoId, string[] tipoTurnoIds)
    {
        var isAjax = string.Equals(
            Request.Headers["X-Requested-With"].ToString(),
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(equipoId))
        {
            if (isAjax)
            {
                return BadRequest(new { success = false, error = "Equipo invalido." });
            }

            return BadRequest(new { message = "Equipo invalido." });
        }

        if (IsLiderOnly())
        {
            var liderEquipoId = await GetCurrentUserEquipoIdAsync();
            if (string.IsNullOrWhiteSpace(liderEquipoId) || !string.Equals(liderEquipoId, equipoId, StringComparison.Ordinal))
            {
                if (isAjax)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { success = false, error = "No autorizado para actualizar este equipo." });
                }

                return Forbid();
            }
        }

        var validTurnos = await _db.TipoTurnos
            .AsNoTracking()
            .Select(t => t.TipoTurnoId)
            .ToListAsync();

        var selected = (tipoTurnoIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Where(id => validTurnos.Contains(id))
            .Distinct()
            .ToList();

        var existing = await _db.EquipoTipoTurnos
            .Where(et => et.EquipoId == equipoId)
            .ToListAsync();

        _db.EquipoTipoTurnos.RemoveRange(existing);

        foreach (var tipoTurnoId in selected)
        {
            _db.EquipoTipoTurnos.Add(new EquipoTipoTurno
            {
                EquipoId = equipoId,
                TipoTurnoId = tipoTurnoId
            });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Tipos de turno actualizados.";

        if (isAjax)
        {
            return Json(new { success = true, message = "Tipos de turno actualizados." });
        }

        return RedirectToAction("Index");
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DescargarConfiguracion(string equipoId)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
            return BadRequest(new { message = "Equipo invalido." });

        var equipo = await _db.Equipos
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EquipoId == equipoId);

        if (equipo == null)
            return NotFound();

        var grupos = await _db.Grupos
            .AsNoTracking()
            .Where(g => g.EquipoId == equipoId)
            .Select(g => new
            {
                g.GrupoId,
                g.NombreGrupo,
                g.Activo
            })
            .ToListAsync();

        var personas = await _db.Personas
            .AsNoTracking()
            .Where(p => p.EquipoId == equipoId && !p.Borrado)
            .Select(p => new
            {
                p.PersonaId,
                p.Nombre,
                p.SegundoNombre,
                p.Apellido,
                p.SegundoApellido,
                p.Ultimatix,
                Activo = !p.Borrado
            })
            .ToListAsync();

        var rolesRaw = await (
            from p in _db.Personas.AsNoTracking()
            join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
            join ur in _db.UserRoles.AsNoTracking() on u.Id equals ur.UserId
            join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where p.EquipoId == equipoId && !p.Borrado
            select new { p.PersonaId, Role = r.Name })
            .ToListAsync();

        var rolesByPersona = rolesRaw
            .GroupBy(x => x.PersonaId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Role ?? string.Empty)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Distinct()
                    .OrderBy(r => r)
                    .ToArray());

        var personaGrupos = await (
            from pg in _db.PersonaGrupos.AsNoTracking()
            join g in _db.Grupos.AsNoTracking() on pg.GrupoId equals g.GrupoId
            where g.EquipoId == equipoId
            select new
            {
                pg.PersonaId,
                g.GrupoId,
                g.NombreGrupo,
                g.Activo,
                pg.EsPrincipal
            })
            .ToListAsync();

        var gruposByPersona = personaGrupos
            .GroupBy(x => x.PersonaId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new
                    {
                        x.GrupoId,
                        x.NombreGrupo,
                        x.Activo,
                        x.EsPrincipal
                    })
                    .OrderBy(x => x.NombreGrupo)
                    .ToArray());

        var personasExport = personas.Select(p => new
        {
            p.PersonaId,
            p.Nombre,
            p.SegundoNombre,
            p.Apellido,
            p.SegundoApellido,
            p.Ultimatix,
            p.Activo,
            Roles = rolesByPersona.TryGetValue(p.PersonaId, out var roles)
                ? roles
                : Array.Empty<string>(),
            Grupos = gruposByPersona.TryGetValue(p.PersonaId, out var gruposPersona)
                ? gruposPersona
                : Array.Empty<object>()
        }).ToList();

        var roleCounts = personasExport
            .SelectMany(p => p.Roles.DefaultIfEmpty("SinRol"))
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => g.Count());

        var tipoTurnos = await (
            from et in _db.EquipoTipoTurnos.AsNoTracking()
            join t in _db.TipoTurnos.AsNoTracking() on et.TipoTurnoId equals t.TipoTurnoId
            where et.EquipoId == equipoId
            select new
            {
                t.TipoTurnoId,
                t.NombreTurno,
                HoraInicio = t.HoraInicio.ToString("HH\\:mm"),
                HoraFin = t.HoraFin.ToString("HH\\:mm"),
                t.Activo
            })
            .ToListAsync();

        var planificacion = await (
            from p in _db.Planificaciones.AsNoTracking()
            join g in _db.Grupos.AsNoTracking() on p.GrupoId equals g.GrupoId
            join t in _db.TipoTurnos.AsNoTracking() on p.TipoTurnoId equals t.TipoTurnoId
            where g.EquipoId == equipoId
            select new
            {
                p.PlanificacionId,
                p.GrupoId,
                GrupoNombre = g.NombreGrupo,
                p.Dia,
                p.TipoTurnoId,
                TipoTurnoNombre = t.NombreTurno,
                p.NumeroPersonas
            })
            .ToListAsync();

        var totalPorDia = planificacion
            .GroupBy(p => p.Dia)
            .Select(g => new { Dia = g.Key, TotalPersonas = g.Sum(x => x.NumeroPersonas) })
            .OrderBy(g => g.Dia)
            .ToList();

        var totalPorDiaTurno = planificacion
            .GroupBy(p => new { p.Dia, p.TipoTurnoId, p.TipoTurnoNombre })
            .Select(g => new
            {
                g.Key.Dia,
                g.Key.TipoTurnoId,
                g.Key.TipoTurnoNombre,
                TotalPersonas = g.Sum(x => x.NumeroPersonas)
            })
            .OrderBy(g => g.Dia)
            .ThenBy(g => g.TipoTurnoNombre)
            .ToList();

        var export = new
        {
            GeneratedAt = DateTime.UtcNow,
            Equipo = new
            {
                equipo.EquipoId,
                equipo.NombreEquipo,
                equipo.Activo
            },
            Grupos = grupos,
            Personas = personasExport,
            Roles = new
            {
                Counts = roleCounts
            },
            TipoTurnos = tipoTurnos,
            Planificacion = new
            {
                Entries = planificacion,
                TotalPorDia = totalPorDia,
                TotalPorDiaTurno = totalPorDiaTurno
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(export, options);
        var fileName = $"equipo_{SanitizeFileName(equipo.NombreEquipo)}_{DateTime.UtcNow:yyyyMMdd}.json";
        return File(Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    private bool IsLiderOnly()
    {
        return User.IsInRole("Lider") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin");
    }

    private async Task<string?> GetCurrentUserEquipoIdAsync()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return null;
        }

        return await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == currentUserId && !p.Borrado)
            .Select(p => p.EquipoId)
            .FirstOrDefaultAsync();
    }

    private string GetCurrentUserRole()
    {
        if (User.IsInRole("SuperAdmin")) return "SuperAdmin";
        if (User.IsInRole("Admin")) return "Admin";
        return string.Empty;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "equipo";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            sb.Append(invalid.Contains(ch) ? '_' : ch);
        }
        return sb.ToString();
    }
}
