using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Controllers;

public class GrupoController : Controller
{
    private readonly IGrupoService _grupoService;
    private readonly IEquipoService _equipoService;

    private readonly ILogger<GrupoController> _logger;

    public GrupoController(IGrupoService grupoService, IEquipoService equipoService, ILogger<GrupoController> logger)
    {
        _grupoService = grupoService;
        _equipoService = equipoService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var grupos = await _grupoService.GetAllAsync();
        _logger.LogDebug("Index grupos: {Count} registros", grupos.Count);
        ViewBag.Equipos = await _equipoService.GetAllAsync();
        return View(grupos);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return RedirectToAction("Index", "Equipo");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GrupoViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Datos invalidos para crear el grupo.";
            return RedirectToAction("Index", "Equipo");
        }

        var grupo = new Grupo
        {
            NombreGrupo = vm.NombreGrupo,
            EquipoId = vm.EquipoId
        };

        var result = await _grupoService.CreateGrupoAsync(grupo);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Crear grupo fallo para grupo {Grupo}: {Reason}", vm.NombreGrupo, result.Error);
            TempData["Error"] = result.Error ?? "Ocurrio un error inesperado.";
            return RedirectToAction("Index", "Equipo");
        }

        return RedirectToAction("Index", "Equipo");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInline(string nombreGrupo, string equipoId, bool? activo)
    {
        if (string.IsNullOrWhiteSpace(nombreGrupo) || string.IsNullOrWhiteSpace(equipoId))
        {
            return BadRequest(new { message = "Nombre y equipo son requeridos." });
        }

        var grupo = new Grupo
        {
            NombreGrupo = nombreGrupo.Trim(),
            EquipoId = equipoId.Trim(),
            Activo = activo ?? true
        };

        var result = await _grupoService.CreateGrupoAsync(grupo);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error ?? "No se pudo crear el grupo." });
        }

        return RedirectToAction("Index", "Equipo");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Patch(string id, GrupoPatchRequest request)
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
                NombreGrupo = Request.Form.ContainsKey("NombreGrupo") ? Request.Form["NombreGrupo"].ToString() : request.NombreGrupo,
                EquipoId = Request.Form.ContainsKey("EquipoId") ? Request.Form["EquipoId"].ToString() : request.EquipoId,
                Activo = Request.Form.ContainsKey("Activo") ? activo : request.Activo
            };
        }

        var result = await _grupoService.PatchAsync(id, request, GetCurrentUserRole());
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Actualizar grupo fallo.";
        }

        return RedirectToAction("Index", "Equipo");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var isAjax = string.Equals(
            Request.Headers["X-Requested-With"].ToString(),
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);

        var result = await _grupoService.DeleteAsync(id, GetCurrentUserRole());
        if (!result.Succeeded)
        {
            if (isAjax)
            {
                return BadRequest(new { success = false, message = result.Error ?? "Eliminar grupo fallo." });
            }

            TempData["Error"] = result.Error ?? "Eliminar grupo fallo.";
        }

        if (isAjax)
        {
            return Json(new { success = true });
        }

        return RedirectToAction("Index", "Equipo");
    }

    private async Task LoadEquiposAsync(GrupoViewModel vm)
    {
        var equipos = await _equipoService.GetAllAsync();
        vm.Equipos = equipos.Select(e => new SelectListItem
        {
            Value = e.EquipoId,
            Text = e.NombreEquipo
        });
    }

    private string GetCurrentUserRole()
    {
        if (User.IsInRole("SuperAdmin")) return "SuperAdmin";
        if (User.IsInRole("Admin")) return "Admin";
        return string.Empty;
    }
}
