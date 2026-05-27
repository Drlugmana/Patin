using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroTcs.Records;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Controllers;

[Authorize(Policy = "AdminAbove")]
public class FeriadoController : Controller
{
    private readonly IFeriadoService _feriadoService;
    private readonly ILogger<FeriadoController> _logger;

    public FeriadoController(IFeriadoService feriadoService, ILogger<FeriadoController> logger)
    {
        _feriadoService = feriadoService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await BuildIndexViewModelAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FeriadoIndexViewModel viewModel)
    {
        var createModel = viewModel.Create ?? new FeriadoCrearViewModel();
        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildIndexViewModelAsync(createModel));
        }

        var result = await _feriadoService.CreateAsync(
            new FeriadoCreateRequest(
                createModel.NombreFeriado,
                createModel.InicioFeriado,
                createModel.FinFeriado));

        if (!result.Succeeded)
        {
            ModelState.AddModelError("Create", result.Error ?? "No se pudo crear el feriado.");
            return View(nameof(Index), await BuildIndexViewModelAsync(createModel));
        }

        TempData["Success"] = "Feriado creado correctamente.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Patch(string id, FeriadoPatchRequest request)
    {
        if (Request.HasFormContentType)
        {
            var inicioValue = Request.Form.ContainsKey("InicioFeriado") ? Request.Form["InicioFeriado"].ToString() : null;
            var finValue = Request.Form.ContainsKey("FinFeriado") ? Request.Form["FinFeriado"].ToString() : null;

            DateOnly? inicio = null;
            DateOnly? fin = null;
            if (!string.IsNullOrWhiteSpace(inicioValue) &&
                DateOnly.TryParseExact(inicioValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedInicio))
            {
                inicio = parsedInicio;
            }

            if (!string.IsNullOrWhiteSpace(finValue) &&
                DateOnly.TryParseExact(finValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedFin))
            {
                fin = parsedFin;
            }

            request = request with
            {
                NombreFeriado = Request.Form.ContainsKey("NombreFeriado") ? Request.Form["NombreFeriado"].ToString() : request.NombreFeriado,
                InicioFeriado = Request.Form.ContainsKey("InicioFeriado") ? inicio : request.InicioFeriado,
                FinFeriado = Request.Form.ContainsKey("FinFeriado") ? fin : request.FinFeriado
            };
        }

        var result = await _feriadoService.PatchAsync(id, request, GetCurrentUserRole());
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "No se pudo actualizar el feriado.";
        }
        else
        {
            TempData["Success"] = "Feriado actualizado correctamente.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _feriadoService.DeleteAsync(id, GetCurrentUserRole());
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "No se pudo eliminar el feriado.";
        }
        else
        {
            TempData["Success"] = "Feriado eliminado correctamente.";
        }

        return RedirectToAction(nameof(Index));
    }

    private string GetCurrentUserRole()
    {
        if (User.IsInRole("SuperAdmin")) return "SuperAdmin";
        if (User.IsInRole("Admin")) return "Admin";
        return string.Empty;
    }

    private async Task<FeriadoIndexViewModel> BuildIndexViewModelAsync(FeriadoCrearViewModel? createModel = null)
    {
        var list = await _feriadoService.GetAllAsync();
        return new FeriadoIndexViewModel
        {
            Feriados = list,
            Create = createModel ?? new FeriadoCrearViewModel()
        };
    }
}
