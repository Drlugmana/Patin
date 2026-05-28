using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TurneroTcs.Models;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.ViewModels;
using TurneroTcs.Records;
using TurneroTcs.Data;

namespace TurneroTcs.Controllers;

[Authorize(Policy = "LiderAbove")]
public class PersonaController : Controller
{
    private readonly IPersonaService _personaService;
    private readonly IRolService _rolService;
    private readonly ILogger<PersonaController> _logger;
    private readonly IEquipoService _equipoService;
    private readonly IGrupoService _grupoService;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    
    public PersonaController(IPersonaService personaService, 
                            IRolService rolService, 
                            ILogger<PersonaController> logger,
                            IEquipoService equipoService,
                            IGrupoService grupoService,
                            ApplicationDbContext db,
                            UserManager<IdentityUser> userManager)
    {
        _personaService = personaService;
        _rolService = rolService;
        _logger = logger;
        _equipoService = equipoService;
        _grupoService = grupoService;
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? equipoId, string? grupoId, string? sortField, string? sortDirection, int page = 1, int pageSize = 15)
    {
        var list = await _personaService.GetAllAsync();
        _logger.LogDebug("Lista de {Count} personas traidas exitosamente.", list.Count);

        var currentRole = GetCurrentUserRole();
        var isLiderOnly = User.IsInRole("Lider") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin");
        if (isLiderOnly)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var liderEquipoId = await _db.Personas
                .AsNoTracking()
                .Where(p => p.UserId == currentUserId && !p.Borrado)
                .Select(p => p.EquipoId)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(liderEquipoId))
            {
                list = Array.Empty<PersonaListViewModel>();
            }
            else
            {
                list = list.Where(p => p.EquipoId == liderEquipoId).ToList();
            }
        }

        var equipos = isLiderOnly
            ? new List<Equipo>()
            : (await _equipoService.GetAllAsync()).ToList();

        if (isLiderOnly)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var liderEquipoId = await _db.Personas
                .AsNoTracking()
                .Where(p => p.UserId == currentUserId && !p.Borrado)
                .Select(p => p.EquipoId)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(liderEquipoId))
            {
                var liderEquipo = await _equipoService.GetAllAsync();
                equipos = liderEquipo.Where(e => e.EquipoId == liderEquipoId).ToList();
            }
        }

        var grupos = await _grupoService.GetAllAsync();
        if (!string.IsNullOrWhiteSpace(equipoId))
        {
            grupos = grupos.Where(g => g.EquipoId == equipoId).ToList();
        }
        else if (isLiderOnly && equipos.Count == 1)
        {
            var liderEquipoIdValue = equipos[0].EquipoId;
            grupos = grupos.Where(g => g.EquipoId == liderEquipoIdValue).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            list = list.Where(p =>
                    p.Ultimatix.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    p.NombreCompleto.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(equipoId))
        {
            list = list.Where(p => p.EquipoId == equipoId).ToList();
        }

        if (!string.IsNullOrWhiteSpace(grupoId))
        {
            list = list.Where(p =>
                    p.GrupoIds.Contains(grupoId) ||
                    p.GrupoIdsSecundarios.Contains(grupoId))
                .ToList();
        }

        var normalizedSortField = string.Equals(sortField, "nombre", StringComparison.OrdinalIgnoreCase)
            ? "nombre"
            : "nombre";
        var normalizedSortDirection = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase)
            ? "desc"
            : "asc";

        list = (normalizedSortField, normalizedSortDirection) switch
        {
            ("nombre", "desc") => list
                .OrderByDescending(p => p.NombreCompleto, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Ultimatix, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => list
                .OrderBy(p => p.NombreCompleto, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Ultimatix, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        if (pageSize is not (15 or 25 or 50 or 100))
        {
            pageSize = 15;
        }

        var totalCount = list.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages == 0) totalPages = 1;
        page = Math.Clamp(page, 1, totalPages);

        list = list
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.Equipos = equipos;
        ViewBag.Grupos = grupos;
        ViewBag.Search = search;
        ViewBag.EquipoId = equipoId;
        ViewBag.GrupoId = grupoId;
        ViewBag.SortField = normalizedSortField;
        ViewBag.SortDirection = normalizedSortDirection;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;

        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Patch(string id, PersonaPatchRequest request){
        var currentUserRole = GetCurrentUserRole();
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        if (Request.HasFormContentType)
        {
            var grupoValues = Request.Form["GrupoIds"];
            var grupoIds = grupoValues
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value ?? string.Empty)
                .ToArray();
            var grupoSecValues = Request.Form["GrupoIdsSecundarios"];
            var grupoSecIds = grupoSecValues
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value ?? string.Empty)
                .ToArray();
            request = request with
            {
                SegundoNombre = request.SegundoNombre ?? (Request.Form.ContainsKey("SegundoNombre") ? string.Empty : null),
                SegundoApellido = request.SegundoApellido ?? (Request.Form.ContainsKey("SegundoApellido") ? string.Empty : null),
                EquipoId = Request.Form.ContainsKey("EquipoId") ? Request.Form["EquipoId"].ToString() : request.EquipoId,
                GrupoIds = grupoValues.Count > 0 ? grupoIds : request.GrupoIds,
                GrupoIdsSecundarios = grupoSecValues.Count > 0 ? grupoSecIds : request.GrupoIdsSecundarios,
                ColorUsuario = Request.Form.ContainsKey("ColorUsuario") ? Request.Form["ColorUsuario"].ToString() : request.ColorUsuario
            };
        }

        var result = await _personaService.PatchAsync(id, request, currentUserRole, currentUserId);

        if (!result.Succeeded){
            TempData["Error"] = result.Error ?? "Actualizar usuario falló.";
            return RedirectToAction("Index");
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var currentUserRole = GetCurrentUserRole();
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var result = await _personaService.DeleteAsync(id, currentUserRole, currentUserId);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Eliminar usuario fallo.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(string id)
    {
        var currentUserRole = GetCurrentUserRole();
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var result = await _personaService.RestoreAsync(id, currentUserRole, currentUserId);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Recuperar usuario fallo.";
        }

        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> DeleteImpact(string id)
    {
        var currentUserRole = GetCurrentUserRole();
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var result = await _personaService.GetDeleteImpactAsync(id, currentUserRole, currentUserId);

        if (!result.Succeeded || result.Value == null)
        {
            return BadRequest(new { message = result.Error ?? "No se pudo revisar el impacto del borrado." });
        }

        return Json(new
        {
            futureTurnosCount = result.Value.FutureTurnosCount,
            firstFutureTurnoDate = result.Value.FirstFutureTurnoDate?.ToString("yyyy-MM-dd"),
            lastFutureTurnoDate = result.Value.LastFutureTurnoDate?.ToString("yyyy-MM-dd"),
            hasLinkedFutureTurnos = result.Value.HasLinkedFutureTurnos
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        var currentRole = GetCurrentUserRole();
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentRole != "SuperAdmin" && currentRole != "Lider")
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newPassword))
        {
            return BadRequest(new { message = "Datos invalidos." });
        }

        if (!await CanManagePersonaAsync(currentUserId, currentRole, id))
        {
            return Forbid();
        }

        var userId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.PersonaId == id)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { message = "Usuario no encontrado." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return BadRequest(new { message = "Usuario no encontrado." });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var error = result.Errors.FirstOrDefault()?.Description ?? "No se pudo restablecer la contrasena.";
            return BadRequest(new { message = error });
        }

        return Json(new { ok = true });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var currentUserRole = GetCurrentUserRole();
        if (string.IsNullOrEmpty(currentUserRole))
        {
            _logger.LogWarning("Usuario sin rol intentó abrir Crear Persona.");
            return Forbid();
        }

        var allowedRoles = GetAllowedRoles(currentUserRole);
        var equipos = await GetEquiposForCreateAsync(currentUserRole, User.FindFirstValue(ClaimTypes.NameIdentifier));
        var selectedEquipoId = currentUserRole == "Lider"
            ? equipos.FirstOrDefault()?.EquipoId
            : null;
        var personaViewModel = new PersonaCrearViewModel
        {
            Roles = (await _rolService.GetRolesAsync()).Where(r => allowedRoles.Contains(r.Value)),
            Equipos = equipos.Select(e => new SelectListItem
            {
                Value = e.EquipoId,
                Text = e.NombreEquipo
            }),
            EquipoId = selectedEquipoId,
            GruposNombres = await GetGruposAsync(selectedEquipoId)
        };
        
        return View(personaViewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PersonaCrearViewModel personaCrearViewModel)
    {
        var currentUserRole = GetCurrentUserRole();
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserRole))
        {
            _logger.LogWarning("Usuario sin rol intentó crear Persona.");
            return Forbid();
        }

        var allowedRoles = GetAllowedRoles(currentUserRole);
        if (ModelState.IsValid)
        {
            var persona = new Persona
            {
                Nombre = personaCrearViewModel.Nombre,
                Apellido = personaCrearViewModel.Apellido,
                SegundoNombre = personaCrearViewModel.SegundoNombre,
                SegundoApellido = personaCrearViewModel.SegundoApellido,
                Ultimatix = personaCrearViewModel.Ultimatix,
                EquipoId = personaCrearViewModel.EquipoId,
                ColorUsuario = personaCrearViewModel.ColorUsuario
            };

            var result = await _personaService.CreateAsync(
                persona,
                personaCrearViewModel.Password,
                personaCrearViewModel.RoleName,
                personaCrearViewModel.EquipoId,
                currentUserRole,
                currentUserId ?? string.Empty,
                personaCrearViewModel.Grupos);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", result.Error ?? "Ocurrio un error inesperado.");
                _logger.LogWarning("Crear persona fallo para Ultimatix {Ultimatix}: {Reason}", personaCrearViewModel.Ultimatix, result.Error);
                personaCrearViewModel.Roles = (await _rolService.GetRolesAsync()).Where(r => allowedRoles.Contains(r.Value));
                var equipos = await GetEquiposForCreateAsync(currentUserRole, currentUserId);
                if (currentUserRole == "Lider")
                {
                    personaCrearViewModel.EquipoId = equipos.FirstOrDefault()?.EquipoId;
                }
                personaCrearViewModel.Equipos = equipos.Select(e => new SelectListItem
                {
                    Value = e.EquipoId,
                    Text = e.NombreEquipo
                });
                personaCrearViewModel.GruposNombres = await GetGruposAsync(personaCrearViewModel.EquipoId);
                return View(personaCrearViewModel);
            }
            return RedirectToAction("Index");
        }

        personaCrearViewModel.Roles = (await _rolService.GetRolesAsync()).Where(r => allowedRoles.Contains(r.Value));
        var equiposFallback = await GetEquiposForCreateAsync(currentUserRole, currentUserId);
        if (currentUserRole == "Lider")
        {
            personaCrearViewModel.EquipoId = equiposFallback.FirstOrDefault()?.EquipoId;
        }
        personaCrearViewModel.Equipos = equiposFallback.Select(e => new SelectListItem
        {
            Value = e.EquipoId,
            Text = e.NombreEquipo
        });
        personaCrearViewModel.GruposNombres = await GetGruposAsync(personaCrearViewModel.EquipoId);
        return View(personaCrearViewModel);
    }

    [HttpGet]
    public async Task<IActionResult> GruposPorEquipo(string? equipoId)
    {
        if (User.IsInRole("Lider") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var liderEquipoId = await _db.Personas
                .AsNoTracking()
                .Where(p => p.UserId == currentUserId && !p.Borrado)
                .Select(p => p.EquipoId)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(liderEquipoId) || !string.Equals(equipoId, liderEquipoId, StringComparison.Ordinal))
            {
                return Json(Array.Empty<object>());
            }
        }

        var grupos = await GetGruposAsync(equipoId);
        return Json(grupos.Select(g => new { value = g.Value, text = g.Text }));
    }

    private string GetCurrentUserRole()
    {
        if (User.IsInRole("SuperAdmin")) return "SuperAdmin";
        if (User.IsInRole("Admin")) return "Admin";
        if (User.IsInRole("Lider")) return "Lider";
        if (User.IsInRole("Usuario")) return "Usuario";
        return string.Empty;
    }

    private static IReadOnlyCollection<string> GetAllowedRoles(string currentUserRole)
    {
        return currentUserRole switch
        {
            "SuperAdmin" => new[] { "SuperAdmin", "Admin", "Lider", "Usuario" },
            "Admin" => new[] { "Lider", "Usuario" },
            "Lider" => new[] { "Usuario" },
            _ => Array.Empty<string>()
        };
    }

    private async Task<bool> CanManagePersonaAsync(string? currentUserId, string currentUserRole, string personaId)
    {
        if (string.IsNullOrWhiteSpace(currentUserRole) || string.IsNullOrWhiteSpace(personaId))
        {
            return false;
        }

        if (currentUserRole == "SuperAdmin" || currentUserRole == "Admin")
        {
            return true;
        }

        if (currentUserRole != "Lider" || string.IsNullOrWhiteSpace(currentUserId))
        {
            return false;
        }

        var liderEquipoId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == currentUserId && !p.Borrado)
            .Select(p => p.EquipoId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(liderEquipoId))
        {
            return false;
        }

        var targetEquipoId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.PersonaId == personaId)
            .Select(p => p.EquipoId)
            .FirstOrDefaultAsync();

        return !string.IsNullOrWhiteSpace(targetEquipoId)
            && string.Equals(targetEquipoId, liderEquipoId, StringComparison.Ordinal);
    }

    private async Task<IEnumerable<SelectListItem>> GetGruposAsync(string? equipoId)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return Enumerable.Empty<SelectListItem>();
        }

        var grupos = await _grupoService.GetAllAsync();
        return grupos
            .Where(g => g.EquipoId == equipoId && g.Activo)
            .OrderBy(g => g.NombreGrupo)
            .Select(g => new SelectListItem
            {
                Value = g.GrupoId,
                Text = g.NombreGrupo
            });
    }

    private async Task<IReadOnlyList<Equipo>> GetEquiposForCreateAsync(string currentUserRole, string? currentUserId)
    {
        if (currentUserRole != "Lider")
        {
            return await _equipoService.GetAllAsync();
        }

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Array.Empty<Equipo>();
        }

        var liderEquipoId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == currentUserId && !p.Borrado)
            .Select(p => p.EquipoId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(liderEquipoId))
        {
            return Array.Empty<Equipo>();
        }

        var equipos = await _equipoService.GetAllAsync();
        return equipos.Where(e => e.EquipoId == liderEquipoId).ToList();
    }
}
