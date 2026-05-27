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
public class ExcepcionController : Controller
{
    private readonly IExcepcionTurnoPersonaService _excepcionService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ExcepcionController> _logger;

    public ExcepcionController(
        IExcepcionTurnoPersonaService excepcionService,
        ApplicationDbContext db,
        ILogger<ExcepcionController> logger)
    {
        _excepcionService = excepcionService;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string equipoId)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return BadRequest("Equipo invalido.");
        }

        if (IsLiderOnly())
        {
            var liderEquipoId = await GetCurrentUserEquipoIdAsync();
            if (string.IsNullOrWhiteSpace(liderEquipoId) || !string.Equals(liderEquipoId, equipoId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
        }

        var equipo = await _db.Equipos.AsNoTracking().FirstOrDefaultAsync(e => e.EquipoId == equipoId);
        if (equipo == null)
        {
            return NotFound();
        }

        var result = await _excepcionService.GetAllAsync(equipoId, GetCurrentUserRole(), await GetCurrentUserEquipoIdAsync());
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "No se pudieron cargar las excepciones.";
        }

        var personas = await _db.Personas
            .AsNoTracking()
            .Where(p => p.EquipoId == equipoId && !p.Borrado)
            .OrderBy(p => p.Nombre)
            .ThenBy(p => p.Apellido)
            .ToListAsync();

        var tipoTurnos = await _db.TipoTurnos
            .AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.NombreTurno)
            .ToListAsync();

        var model = new ExcepcionTurnoPersonaIndexViewModel
        {
            EquipoId = equipoId,
            NombreEquipo = equipo.NombreEquipo,
            Excepciones = result.Succeeded ? result.Value ?? Array.Empty<Models.ExcepcionTurnoPersona>() : Array.Empty<Models.ExcepcionTurnoPersona>(),
            Personas = personas,
            TipoTurnos = tipoTurnos,
            Create = new ExcepcionTurnoPersonaCreateViewModel()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string equipoId, ExcepcionTurnoPersonaIndexViewModel viewModel)
    {
        var createModel = viewModel.Create ?? new ExcepcionTurnoPersonaCreateViewModel();
        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildIndexViewModelAsync(equipoId, createModel));
        }

        var diasSeleccionados = createModel.SelectedDiasSemana ?? new List<int>();
        var result = await _excepcionService.CreateAsync(
            equipoId,
            new ExcepcionTurnoPersonaCreateRequest(
                createModel.PersonaId,
                createModel.TipoTurnoId,
                createModel.MotivoExcepcion,
                createModel.FechaInicio,
                createModel.FechaFin,
                diasSeleccionados),
            GetCurrentUserRole(),
            await GetCurrentUserEquipoIdAsync());

        if (!result.Succeeded)
        {
            ModelState.AddModelError("Create", result.Error ?? "No se pudo crear la excepcion.");
            return View(nameof(Index), await BuildIndexViewModelAsync(equipoId, createModel));
        }

        TempData["Success"] = "Excepcion creada correctamente.";
        return RedirectToAction(nameof(Index), new { equipoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Patch(string equipoId, string id, ExcepcionTurnoPersonaPatchRequest request)
    {
        if (Request.HasFormContentType)
        {
            var fechaInicioValue = Request.Form.ContainsKey("FechaInicio") ? Request.Form["FechaInicio"].ToString() : null;
            var fechaFinValue = Request.Form.ContainsKey("FechaFin") ? Request.Form["FechaFin"].ToString() : null;
            DateOnly? fechaInicio = null;
            DateOnly? fechaFin = null;

            if (!string.IsNullOrWhiteSpace(fechaInicioValue) && DateOnly.TryParse(fechaInicioValue, out var parsedInicio))
            {
                fechaInicio = parsedInicio;
            }

            if (!string.IsNullOrWhiteSpace(fechaFinValue) && DateOnly.TryParse(fechaFinValue, out var parsedFin))
            {
                fechaFin = parsedFin;
            }

            var diasFromForm = Request.Form.Where(kv => kv.Key == "DiasSemana" || kv.Key == "DiasSemana[]" )
                .SelectMany(kv => kv.Value)
                .SelectMany(v => (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(s => int.TryParse(s, out _))
                .Select(int.Parse)
                .ToList();

            request = request with
            {
                PersonaId = Request.Form.ContainsKey("PersonaId") ? Request.Form["PersonaId"].ToString() : request.PersonaId,
                TipoTurnoId = Request.Form.ContainsKey("TipoTurnoId") ? Request.Form["TipoTurnoId"].ToString() : request.TipoTurnoId,
                MotivoExcepcion = Request.Form.ContainsKey("MotivoExcepcion") ? Request.Form["MotivoExcepcion"].ToString() : request.MotivoExcepcion,
                FechaInicio = Request.Form.ContainsKey("FechaInicio") ? fechaInicio : request.FechaInicio,
                FechaFin = Request.Form.ContainsKey("FechaFin") ? fechaFin : request.FechaFin,
                DiasSemana = diasFromForm.Count > 0 ? diasFromForm : request.DiasSemana
            };
        }

        var result = await _excepcionService.PatchAsync(equipoId, id, request, GetCurrentUserRole(), await GetCurrentUserEquipoIdAsync());
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "No se pudo actualizar la excepcion.";
        }
        else
        {
            TempData["Success"] = "Excepcion actualizada correctamente.";
        }

        return RedirectToAction(nameof(Index), new { equipoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string equipoId, string id)
    {
        var result = await _excepcionService.DeleteAsync(equipoId, id, GetCurrentUserRole(), await GetCurrentUserEquipoIdAsync());
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "No se pudo eliminar la excepcion.";
        }
        else
        {
            TempData["Success"] = "Excepcion eliminada correctamente.";
        }

        return RedirectToAction(nameof(Index), new { equipoId });
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
        if (User.IsInRole("Lider")) return "Lider";
        return string.Empty;
    }

    private async Task<ExcepcionTurnoPersonaIndexViewModel> BuildIndexViewModelAsync(string equipoId, ExcepcionTurnoPersonaCreateViewModel? createModel = null)
    {
        var equipo = await _db.Equipos.AsNoTracking().FirstOrDefaultAsync(e => e.EquipoId == equipoId);
        var result = await _excepcionService.GetAllAsync(equipoId, GetCurrentUserRole(), await GetCurrentUserEquipoIdAsync());
        return new ExcepcionTurnoPersonaIndexViewModel
        {
            EquipoId = equipoId,
            NombreEquipo = equipo?.NombreEquipo,
            Excepciones = result.Succeeded ? result.Value ?? Array.Empty<Models.ExcepcionTurnoPersona>() : Array.Empty<Models.ExcepcionTurnoPersona>(),
            Personas = await _db.Personas.AsNoTracking().Where(p => p.EquipoId == equipoId && !p.Borrado).OrderBy(p => p.Nombre).ThenBy(p => p.Apellido).ToListAsync(),
            TipoTurnos = await _db.TipoTurnos.AsNoTracking().Where(t => t.Activo).OrderBy(t => t.NombreTurno).ToListAsync(),
            Create = createModel ?? new ExcepcionTurnoPersonaCreateViewModel()
        };
    }
}
