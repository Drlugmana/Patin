using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroTcs.Records;
using TurneroTcs.Models;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Controllers;

[Authorize(Policy = "AdminAbove")]
public class TipoTurnoController : Controller
{
    private readonly ITipoTurnoService _tipoTurnoService;
    private readonly ILogger<TipoTurnoController> _logger;

    public TipoTurnoController(ITipoTurnoService tipoTurnoService, ILogger<TipoTurnoController> logger)
    {
        _tipoTurnoService = tipoTurnoService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var list = await _tipoTurnoService.GetAllAsync();
        _logger.LogDebug("Index turnos: {Count} registros.", list.Count);

        return View(list);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var equipoViewModel = new TipoTurnoViewModel();
        return View(equipoViewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TipoTurnoViewModel vm)
    {
        if (ModelState.IsValid)
        {
            var tipoturno = new TipoTurno
            {
                NombreTurno = vm.NombreTurno,
                HoraFin = vm.HoraFin,
                HoraInicio = vm.HoraInicio

            };
            var result = await _tipoTurnoService.CreateTurno(tipoturno);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", result.Error ?? "Ocurrio un error inesperado.");
                _logger.LogWarning("Crear turno fallo para turno {NombreTurno}: {Reason}", vm.NombreTurno, result.Error);
                return View(vm);
            }
            TempData["Success"] = "Tipo de turno creado correctamente.";
            return RedirectToAction("Index");
        }
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Patch(string id, TipoTurnoPatchRequest request)
    {
        if (Request.HasFormContentType)
        {
            var inicioValue = Request.Form.ContainsKey("HoraInicio") ? Request.Form["HoraInicio"].ToString() : null;
            var finValue = Request.Form.ContainsKey("HoraFin") ? Request.Form["HoraFin"].ToString() : null;
            TimeOnly? inicio = null;
            TimeOnly? fin = null;
            if (!string.IsNullOrWhiteSpace(inicioValue) && TimeOnly.TryParse(inicioValue, out var parsedInicio))
            {
                inicio = parsedInicio;
            }
            if (!string.IsNullOrWhiteSpace(finValue) && TimeOnly.TryParse(finValue, out var parsedFin))
            {
                fin = parsedFin;
            }

            var activoValue = Request.Form.ContainsKey("Activo") ? Request.Form["Activo"].ToString() : null;
            bool? activo = null;
            if (!string.IsNullOrWhiteSpace(activoValue))
            {
                activo = bool.TryParse(activoValue, out var parsedActivo) ? parsedActivo : activo;
            }

            request = request with
            {
                NombreTurno = Request.Form.ContainsKey("NombreTurno") ? Request.Form["NombreTurno"].ToString() : request.NombreTurno,
                HoraInicio = Request.Form.ContainsKey("HoraInicio") ? inicio : request.HoraInicio,
                HoraFin = Request.Form.ContainsKey("HoraFin") ? fin : request.HoraFin,
                Activo = Request.Form.ContainsKey("Activo") ? activo : request.Activo
            };
        }

        var result = await _tipoTurnoService.PatchAsync(id, request, GetCurrentUserRole());
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Actualizar turno fallo.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _tipoTurnoService.DeleteAsync(id, GetCurrentUserRole());
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Eliminar turno fallo.";
        }

        return RedirectToAction(nameof(Index));
    }

    private string GetCurrentUserRole()
    {
        if (User.IsInRole("SuperAdmin")) return "SuperAdmin";
        if (User.IsInRole("Admin")) return "Admin";
        return string.Empty;
    }
}
