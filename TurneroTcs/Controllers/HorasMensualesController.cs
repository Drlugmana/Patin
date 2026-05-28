using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurneroTcs.Records;
using TurneroTcs.Services.Interfaces;

namespace TurneroTcs.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,Lider,Usuario")]
public class HorasMensualesController : Controller
{
    private const int DefaultPageSize = 15;
    private readonly IHorasMensualesService _horasMensualesService;

    public HorasMensualesController(IHorasMensualesService horasMensualesService)
    {
        _horasMensualesService = horasMensualesService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? fromDate,
        string? toDate,
        string? fromMonth,
        string? toMonth,
        string? equipoId,
        string? grupoId,
        string? personaId,
        string? sortField,
        string? sortDirection,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var request = new HorasMensualesReportRequest(
            fromDate,
            toDate,
            fromMonth,
            toMonth,
            equipoId,
            grupoId,
            personaId,
            sortField,
            sortDirection,
            page,
            pageSize);

        var model = await _horasMensualesService.GetHorasMensualesAsync(request, BuildActorContext());
        ViewBag.SortField = sortField ?? string.Empty;
        ViewBag.SortDirection = sortDirection ?? "asc";
        return View(model);
    }

    [HttpGet]
    // Recargos view and related action removed per request.

    [HttpGet]
    public async Task<IActionResult> DescargarExcel(
        string? fromDate,
        string? toDate,
        string? fromMonth,
        string? toMonth,
        string? equipoId,
        string? grupoId,
        string? personaId,
        string? sortField,
        string? sortDirection,
        int pageSize = DefaultPageSize)
    {
        var request = new HorasMensualesReportRequest(
            fromDate,
            toDate,
            fromMonth,
            toMonth,
            equipoId,
            grupoId,
            personaId,
            sortField,
            sortDirection,
            Page: 1,
            PageSize: pageSize);

        var excel = await _horasMensualesService.ExportExcelAsync(request, BuildActorContext());
        return File(excel.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excel.FileName);
    }

    private HorasMensualesActorContext BuildActorContext()
    {
        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var isLider = User.IsInRole("Lider");
        var isUsuario = User.IsInRole("Usuario") && !isAdmin && !isLider;

        return new HorasMensualesActorContext(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            isAdmin,
            isLider,
            isUsuario);
    }
}
