using System;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Records;
using TurneroTcs.Security;
using TurneroTcs.Services.Interfaces;

namespace TurneroTcs.Controllers;

[Authorize(Policy = "AdminAbove")]
public class PermisoAccesoController : Controller
{
    private readonly IPermisoAccesoService _permisoAccesoService;
    private readonly IPermisoAccesoResolver _permisoAccesoResolver;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PermisoAccesoController> _logger;

    public PermisoAccesoController(
        IPermisoAccesoService permisoAccesoService,
        IPermisoAccesoResolver permisoAccesoResolver,
        ApplicationDbContext db,
        ILogger<PermisoAccesoController> logger)
    {
        _permisoAccesoService = permisoAccesoService;
        _permisoAccesoResolver = permisoAccesoResolver;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await CanReadAsync())
        {
            return Forbid();
        }

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Usuarios()
    {
        if (!await CanReadAsync())
        {
            return Forbid();
        }

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (!await CanReadAsync())
        {
            return Forbid();
        }

        var items = await _permisoAccesoService.GetAllAsync();
        return Ok(items);
    }

    [HttpGet]
    public async Task<IActionResult> Roles(string? id)
    {
        if (!await CanReadAsync())
        {
            return Forbid();
        }

        var assignedRoleIds = string.IsNullOrWhiteSpace(id)
            ? new HashSet<string>()
            : (await _db.RolesPermisosAcceso
                .AsNoTracking()
                .Where(x => x.PermisoAccesoId == id)
                .Select(x => x.RoleId)
                .ToListAsync())
                .ToHashSet(StringComparer.Ordinal);

        var roles = await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name
            })
            .ToListAsync();

        var payload = roles.Select(r => new
        {
            r.Id,
            r.Name,
            Assigned = assignedRoleIds.Contains(r.Id)
        });

        return Ok(payload);
    }

    [HttpGet]
    public async Task<IActionResult> Users(string? id)
    {
        if (!await CanReadAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return Ok(Array.Empty<object>());
        }

        var users = await _permisoAccesoService.GetUsersAsync(id);
        return Ok(users);
    }

    [HttpGet]
    public async Task<IActionResult> UserCatalog()
    {
        if (!await CanReadAsync())
        {
            return Forbid();
        }

        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new
            {
                u.Id,
                UserName = u.UserName ?? string.Empty
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet]
    public async Task<IActionResult> UserPermissions(string? userId)
    {
        if (!await CanReadAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Ok(Array.Empty<object>());
        }

        var list = await _permisoAccesoService.GetUserPermissionsAsync(userId);
        return Ok(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Patch(string id, [FromBody] PermisoAccesoPatchRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var role = GetCurrentUserRole();

        var result = await _permisoAccesoService.PatchAsync(id, request, userId, role);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error ?? "No se pudo actualizar el permiso." });
        }

        return Ok(new { message = "Permiso actualizado correctamente." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(string id, [FromBody] PermisoAccesoRoleRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RoleId))
        {
            return BadRequest(new { message = "Rol es requerido." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var role = GetCurrentUserRole();

        var result = await _permisoAccesoService.AssignRoleAsync(id, request.RoleId, userId, role);
        if (!result.Succeeded)
        {
            _logger.LogWarning("AssignRole fallo para PermisoAccesoId {PermisoAccesoId} y RoleId {RoleId}: {Error}", id, request.RoleId, result.Error);
            return BadRequest(new { message = result.Error ?? "No se pudo asignar el permiso al rol." });
        }

        return Ok(new { message = "Permiso asignado al rol correctamente." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnassignRole(string id, [FromBody] PermisoAccesoRoleRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RoleId))
        {
            return BadRequest(new { message = "Rol es requerido." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var role = GetCurrentUserRole();

        var result = await _permisoAccesoService.UnassignRoleAsync(id, request.RoleId, userId, role);
        if (!result.Succeeded)
        {
            _logger.LogWarning("UnassignRole fallo para PermisoAccesoId {PermisoAccesoId} y RoleId {RoleId}: {Error}", id, request.RoleId, result.Error);
            return BadRequest(new { message = result.Error ?? "No se pudo quitar el permiso del rol." });
        }

        return Ok(new { message = "Permiso retirado del rol correctamente." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignUser(string id, [FromBody] PermisoAccesoUserRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new { message = "Usuario es requerido." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var role = GetCurrentUserRole();

        var result = await _permisoAccesoService.AssignUserAsync(
            id,
            request.UserId,
            request.EsDenegado ?? false,
            userId,
            role);

        if (!result.Succeeded)
        {
            _logger.LogWarning("AssignUser fallo para PermisoAccesoId {PermisoAccesoId} y UserId {UserId}: {Error}", id, request.UserId, result.Error);
            return BadRequest(new { message = result.Error ?? "No se pudo asignar el permiso al usuario." });
        }

        return Ok(new { message = "Permiso de usuario actualizado correctamente." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnassignUser(string id, [FromBody] PermisoAccesoUserRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new { message = "Usuario es requerido." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var role = GetCurrentUserRole();

        var result = await _permisoAccesoService.UnassignUserAsync(id, request.UserId, userId, role);
        if (!result.Succeeded)
        {
            _logger.LogWarning("UnassignUser fallo para PermisoAccesoId {PermisoAccesoId} y UserId {UserId}: {Error}", id, request.UserId, result.Error);
            return BadRequest(new { message = result.Error ?? "No se pudo quitar el permiso del usuario." });
        }

        return Ok(new { message = "Permiso retirado del usuario correctamente." });
    }

    private string GetCurrentUserRole()
    {
        if (User.IsInRole("SuperAdmin")) return "SuperAdmin";
        if (User.IsInRole("Admin")) return "Admin";
        if (User.IsInRole("Lider")) return "Lider";
        if (User.IsInRole("Usuario")) return "Usuario";
        return string.Empty;
    }

    private async Task<bool> CanReadAsync()
    {
        var role = GetCurrentUserRole();
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await _permisoAccesoResolver.TienePermisoAsync(userId, PermisosAccesoCodigos.PermisoAccesoVer);
    }
}
