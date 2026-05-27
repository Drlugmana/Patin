using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Records;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Controllers;

[Authorize(Policy = "LiderAbove")]
public class RegistroTurnoController : Controller
{
    private readonly IRegistroTurnoService _registroTurnoService;
    private readonly IGrupoService _grupoService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RegistroTurnoController> _logger;

    public RegistroTurnoController(
        IRegistroTurnoService registroTurnoService,
        IGrupoService grupoService,
        ApplicationDbContext db,
        ILogger<RegistroTurnoController> logger)
    {
        _registroTurnoService = registroTurnoService;
        _grupoService = grupoService;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Generate()
    {
        return RedirectToAction("Index", "Calendario");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(RegistroTurnoGenerateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Completa todos los campos requeridos.";
            return RedirectToAction("Index", "Calendario");
        }

        var result = await _registroTurnoService.GenerateAsync(
            vm.PersonaId,
            vm.TipoTurnoIds,
            vm.FechaInicio,
            vm.FechaFin,
            vm.GrupoId,
            GetCurrentUserRole(),
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "No se pudieron generar los turnos.";
            return RedirectToAction("Index", "Calendario");
        }

        TempData["Message"] = $"Se generaron {result.CreatedCount} turnos.";
        _logger.LogDebug("Generados {Count} turnos para persona {PersonaId}.", result.CreatedCount, vm.PersonaId);
        return RedirectToAction("Index", "Calendario");
    }

    [HttpGet]
    public async Task<IActionResult> GruposPorPersona(string? personaId)
    {
        if (string.IsNullOrWhiteSpace(personaId))
        {
            return Json(Array.Empty<object>());
        }

        if (!await CanManagePersonaAsync(personaId))
        {
            return Forbid();
        }

        var grupos = await _grupoService.GetByPersonaAsync(personaId ?? string.Empty);
        return Json(grupos.Select(g => new { value = g.GrupoId, text = g.NombreGrupo }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePreview([FromBody] RegistroTurnoPreviewSaveRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "No hay turnos para guardar." });
        }

        var result = await _registroTurnoService.SavePreviewAsync(
            request.Items,
            GetCurrentUserRole(),
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error ?? "No se pudieron guardar los turnos." });
        }

        return Ok(new { created = result.CreatedCount, skipped = result.SkippedCount });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmChange([FromBody] RegistroTurnoChangeRequest request)
    {
        var result = await _registroTurnoService.ConfirmChangeAsync(
            request,
            GetCurrentUserRole(),
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error ?? "No se pudo confirmar el cambio." });
        }

        return Ok(new { message = "Cambio confirmado." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTurno([FromBody] RegistroTurnoDeleteRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TurnoId))
        {
            return BadRequest(new { message = "Turno invalido." });
        }

        var result = await _registroTurnoService.DeleteAsync(
            request.TurnoId,
            GetCurrentUserRole(),
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error ?? "No se pudo eliminar el turno." });
        }

        return Ok(new { message = "Turno eliminado." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTurnoExtra([FromBody] RegistroTurnoExtraRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TurnoId))
        {
            return BadRequest(new { message = "Turno invalido." });
        }

        var result = await _registroTurnoService.SetTurnoExtraAsync(
            request.TurnoId,
            request.EsTurnoExtra,
            GetCurrentUserRole(),
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error ?? "No se pudo actualizar el turno extra." });
        }

        return Ok(new { message = "Turno extra actualizado." });
    }

    private string GetCurrentUserRole()
    {
        if (User.IsInRole("SuperAdmin")) return "SuperAdmin";
        if (User.IsInRole("Admin")) return "Admin";
        if (User.IsInRole("Lider")) return "Lider";
        return string.Empty;
    }

    private async Task<bool> CanManagePersonaAsync(string personaId)
    {
        if (string.IsNullOrWhiteSpace(personaId))
        {
            return false;
        }

        var currentRole = GetCurrentUserRole();
        if (currentRole == "SuperAdmin" || currentRole == "Admin")
        {
            return true;
        }

        if (currentRole != "Lider")
        {
            return false;
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
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
            .Where(p => p.PersonaId == personaId && !p.Borrado)
            .Select(p => p.EquipoId)
            .FirstOrDefaultAsync();

        return !string.IsNullOrWhiteSpace(targetEquipoId)
            && string.Equals(targetEquipoId, liderEquipoId, StringComparison.Ordinal);
    }
}
