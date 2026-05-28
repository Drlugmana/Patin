using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using ClosedXML.Excel;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Repositories;
using TurneroTcs.Repositories.Interfaces;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Services;

public class HorasMensualesService : IHorasMensualesService
{
    private const int DefaultPageSize = 15;
    private static readonly int[] AllowedPageSizes = [15, 25, 50, 100];
    private readonly IHorasMensualesRepository _repository;

    public HorasMensualesService(IHorasMensualesRepository repository)
    {
        _repository = repository;
    }

    public async Task<HorasMensualesViewModel> GetHorasMensualesAsync(
        HorasMensualesReportRequest request,
        HorasMensualesActorContext actor)
    {
        return await BuildModelAsync(request, actor, applyPaging: true, request.SortField, request.SortDirection);
    }

    // Recargos-related endpoint removed. The monthly-hours service no longer exposes a Recargos view model.

    public async Task<HorasMensualesPdfDocument> ExportPdfAsync(
        HorasMensualesReportRequest request,
        HorasMensualesActorContext actor)
    {
        var model = await BuildModelAsync(request, actor, applyPaging: false, request.SortField, request.SortDirection);
        var title = $"Horas mensuales {model.PeriodLabel}";
        var filterSummary = BuildFilterSummary(model);
        var pdf = BuildPdfReport(model, title, filterSummary);
        var fileName = $"HorasMensuales_{BuildPeriodFileToken(model.PeriodStart, model.PeriodEnd)}.pdf";

        return new HorasMensualesPdfDocument(pdf, fileName);
    }

    public async Task<HorasMensualesExcelDocument> ExportExcelAsync(
        HorasMensualesReportRequest request,
        HorasMensualesActorContext actor)
    {
        var model = await BuildModelAsync(request, actor, applyPaging: false, request.SortField, request.SortDirection);
        var (turnos, ultimatixByPersona) = await LoadTurnosForExcelAsync(request, actor);
        var detailRows = BuildExcelDetailRows(turnos, ultimatixByPersona);
        var title = $"Horas mensuales {model.PeriodLabel}";
        var filterSummary = BuildFilterSummary(model);
        var excel = BuildExcelReport(model, detailRows, title, filterSummary);
        var fileName = $"HorasMensuales_{BuildPeriodFileToken(model.PeriodStart, model.PeriodEnd)}.xlsx";

        return new HorasMensualesExcelDocument(excel, fileName);
    }

    private async Task<(IReadOnlyList<HorasMensualesTurnoRow> Turnos, Dictionary<string,string> UltimatixByPersona)> LoadTurnosForExcelAsync(
        HorasMensualesReportRequest request,
        HorasMensualesActorContext actor)
    {
        var (periodStart, periodEnd) = ResolvePeriod(request);
        var firstWeekStart = GetWeekStart(periodStart);
        var monthQueryStart = firstWeekStart;

        var equipoId = request.EquipoId;
        var grupoId = request.GrupoId;
        var personaId = request.PersonaId;

        var resolvedEquipoId = await ResolveEquipoIdAsync(equipoId, actor);
        if (actor.IsUsuario)
        {
            var currentPersona = await ResolvePersonaAsync(actor);
            resolvedEquipoId = currentPersona?.EquipoId;
            personaId = currentPersona?.PersonaId;
            grupoId = null;
        }

        var personas = (await _repository.GetActivePersonasByEquipoAsync(resolvedEquipoId)).ToList();

        if (!string.IsNullOrWhiteSpace(grupoId) && personas.Count > 0)
        {
            var personaGrupos = (await _repository.GetPersonaGruposAsync(personas.Select(p => p.PersonaId).ToList())).ToList();
            personas = personas
                .Where(p => personaGrupos.Any(pg => pg.PersonaId == p.PersonaId && pg.GrupoId == grupoId))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(personaId))
        {
            personas = personas.Where(p => p.PersonaId == personaId).ToList();
        }

        var personaIds = personas.Select(p => p.PersonaId).ToList();

        var registros = (await _repository.GetTurnosAsync(personaIds, monthQueryStart, periodEnd))
            .Where(r => !r.NoLaboradoPorFeriado)
            .ToList();

        if (registros.Count > 0)
        {
            var turnoIds = registros
                .Select(r => r.TurnoId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (turnoIds.Count > 0)
            {
                var cambiosTurno = await _repository.GetCambiosTurnoAsync(turnoIds);
                registros = FilterTurnosByCambioEstado(registros, cambiosTurno);
            }
        }

        var personasList = (await _repository.GetActivePersonasByEquipoAsync(resolvedEquipoId)).ToList();
        var ultById = personasList
            .Where(p => !string.IsNullOrWhiteSpace(p.PersonaId))
            .ToDictionary(p => p.PersonaId, p => p.Ultimatix ?? string.Empty);

        return (registros, ultById);
    }

    private static List<HorasMensualesExcelDetailRow> BuildExcelDetailRows(IEnumerable<HorasMensualesTurnoRow> turnos, IReadOnlyDictionary<string,string> ultimatixByPersona)
    {
        var rows = new List<HorasMensualesExcelDetailRow>();

        foreach (var turno in turnos
                     .OrderBy(t => t.Nombre)
                     .ThenBy(t => t.Fecha)
                     .ThenBy(t => t.HoraInicio))
        {
            var (shiftStart, shiftEnd) = GetShiftRange(turno.Fecha, turno.HoraInicio, turno.HoraFin);
            if (shiftEnd <= shiftStart)
            {
                continue;
            }

            var lastDay = DateOnly.FromDateTime(shiftEnd.AddTicks(-1));
            var currentDay = DateOnly.FromDateTime(shiftStart);

            while (currentDay <= lastDay)
            {
                var dayStart = currentDay.ToDateTime(TimeOnly.MinValue);
                var dayEnd = currentDay.AddDays(1).ToDateTime(TimeOnly.MinValue);

                var effectiveStart = shiftStart > dayStart ? shiftStart : dayStart;
                var effectiveEnd = shiftEnd < dayEnd ? shiftEnd : dayEnd;

                if (effectiveEnd > effectiveStart)
                {
                    var earlyStart = dayStart;
                    var earlyEnd = dayStart.AddHours(6);
                    var lateStart = dayStart.AddHours(20);
                    var lateEnd = dayEnd;

                    var earlySegmentStart = effectiveStart > earlyStart ? effectiveStart : earlyStart;
                    var earlySegmentEnd = effectiveEnd < earlyEnd ? effectiveEnd : earlyEnd;
                    if (earlySegmentEnd > earlySegmentStart)
                    {
                        var hours = (decimal)(earlySegmentEnd - earlySegmentStart).TotalHours;
                        var ult = ultimatixByPersona.TryGetValue(turno.PersonaId, out var u) ? u : string.Empty;
                        rows.Add(new HorasMensualesExcelDetailRow(
                            ult,
                            turno.PersonaId,
                            turno.Nombre,
                            currentDay,
                            TimeOnly.FromDateTime(earlySegmentStart),
                            TimeOnly.FromDateTime(earlySegmentEnd),
                            Math.Round(hours, 2, MidpointRounding.AwayFromZero)));
                    }

                    var lateSegmentStart = effectiveStart > lateStart ? effectiveStart : lateStart;
                    var lateSegmentEnd = effectiveEnd < lateEnd ? effectiveEnd : lateEnd;
                    if (lateSegmentEnd > lateSegmentStart)
                    {
                        var hours = (decimal)(lateSegmentEnd - lateSegmentStart).TotalHours;
                        var ult2 = ultimatixByPersona.TryGetValue(turno.PersonaId, out var u2) ? u2 : string.Empty;
                        rows.Add(new HorasMensualesExcelDetailRow(
                            ult2,
                            turno.PersonaId,
                            turno.Nombre,
                            currentDay,
                            TimeOnly.FromDateTime(lateSegmentStart),
                            TimeOnly.FromDateTime(lateSegmentEnd),
                            Math.Round(hours, 2, MidpointRounding.AwayFromZero)));
                    }
                }

                currentDay = currentDay.AddDays(1);
            }
        }

        return rows;
    }

    private static byte[] BuildExcelReport(
        HorasMensualesViewModel model,
        IReadOnlyList<HorasMensualesExcelDetailRow> detailRows,
        string title,
        string filterSummary)
    {
        using var workbook = new XLWorkbook();
        var detailSheet = workbook.Worksheets.Add("RecargosNocturnos");
        BuildDetailSheet(detailSheet, detailRows, title, filterSummary);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void BuildDetailSheet(
        IXLWorksheet sheet,
        IReadOnlyList<HorasMensualesExcelDetailRow> detailRows,
        string title,
        string filterSummary)
    {
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(2, 1).Value = filterSummary;
        sheet.Range(1, 1, 1, 8).Merge();
        sheet.Range(2, 1, 2, 8).Merge();
        sheet.Range(1, 1, 2, 8).Style.Font.Bold = true;
        sheet.Range(1, 1, 2, 8).Style.Fill.BackgroundColor = XLColor.FromArgb(239, 246, 255);

        var headerRow = 4;
        sheet.Cell(headerRow, 1).Value = "Ultimatix";
        sheet.Cell(headerRow, 2).Value = "Nombre persona";
        sheet.Cell(headerRow, 3).Value = "Día";
        sheet.Cell(headerRow, 4).Value = "Fecha";
        sheet.Cell(headerRow, 5).Value = "Hora entrada";
        sheet.Cell(headerRow, 6).Value = "Hora salida";
        sheet.Cell(headerRow, 7).Value = "Total horas";
        sheet.Cell(headerRow, 8).Value = "Actividad realizada";

        var headerRange = sheet.Range(headerRow, 1, headerRow, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 64, 175);
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var outputRow = 5;
        foreach (var row in detailRows.OrderBy(row => row.Nombre).ThenBy(row => row.Fecha).ThenBy(row => row.HoraIngreso))
        {
            sheet.Cell(outputRow, 1).Value = row.Ultimatix;
            sheet.Cell(outputRow, 2).Value = row.Nombre;
            sheet.Cell(outputRow, 3).Value = FormatSpanishDayName(row.Fecha);
            sheet.Cell(outputRow, 4).Value = row.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            sheet.Cell(outputRow, 5).Value = row.HoraIngreso.ToString("HH:mm", CultureInfo.InvariantCulture);
            sheet.Cell(outputRow, 6).Value = row.HoraSalida.ToString("HH:mm", CultureInfo.InvariantCulture);
            sheet.Cell(outputRow, 7).Value = (double)row.HorasRecargo;
            sheet.Cell(outputRow, 7).Style.NumberFormat.Format = "0.##";
            sheet.Cell(outputRow, 8).Value = "Recargo nocturno";
            outputRow++;
        }

        if (outputRow == 5)
        {
            sheet.Cell(5, 1).Value = "No hay recargos para exportar.";
            sheet.Range(5, 1, 5, 8).Merge();
        }

        sheet.SheetView.FreezeRows(4);
        sheet.Columns(1, 8).AdjustToContents();
    }

    private async Task<HorasMensualesViewModel> BuildModelAsync(
        HorasMensualesReportRequest request,
        HorasMensualesActorContext actor,
        bool applyPaging,
        string? sortField,
        string? sortDirection)
    {
        var (periodStart, periodEnd) = ResolvePeriod(request);

        var firstWeekStart = GetWeekStart(periodStart);
        var monthQueryStart = firstWeekStart;
        var feriadoWindowStart = firstWeekStart;
        var feriadoWindowEnd = periodEnd.AddDays(1);

        var feriados = await _repository.GetFeriadosAsync();
        var feriadoDates = BuildFeriadoDateSet(feriados, feriadoWindowStart, feriadoWindowEnd);

        var equipoId = request.EquipoId;
        var grupoId = request.GrupoId;
        var personaId = request.PersonaId;

        var resolvedEquipoId = await ResolveEquipoIdAsync(equipoId, actor);
        if (actor.IsUsuario)
        {
            var currentPersona = await ResolvePersonaAsync(actor);
            resolvedEquipoId = currentPersona?.EquipoId;
            personaId = currentPersona?.PersonaId;
            grupoId = null;
        }

        var equipos = await _repository.GetEquiposAsync(
            !actor.IsAdmin && !string.IsNullOrWhiteSpace(resolvedEquipoId) ? resolvedEquipoId : null);

        var grupos = string.IsNullOrWhiteSpace(resolvedEquipoId)
            ? new List<Grupo>()
            : (await _repository.GetActiveGruposByEquipoAsync(resolvedEquipoId)).ToList();

        var personas = (await _repository.GetActivePersonasByEquipoAsync(resolvedEquipoId)).ToList();

        if (!string.IsNullOrWhiteSpace(grupoId) && grupos.All(g => g.GrupoId != grupoId))
        {
            grupoId = null;
        }

        if (!string.IsNullOrWhiteSpace(personaId) && personas.All(p => p.PersonaId != personaId))
        {
            personaId = null;
        }

        var personaIds = personas.Select(p => p.PersonaId).ToList();
        if (actor.IsUsuario && !string.IsNullOrWhiteSpace(personaId))
        {
            personaIds = personaIds.Where(id => id == personaId).ToList();
        }

        var personaGrupos = (await _repository.GetPersonaGruposAsync(personaIds)).ToList();

        if (!string.IsNullOrWhiteSpace(grupoId))
        {
            personaIds = personaGrupos
                .Where(pg => pg.GrupoId == grupoId)
                .Select(pg => pg.PersonaId)
                .Distinct()
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(personaId))
        {
            personaIds = personaIds.Where(id => id == personaId).ToList();
        }

        var registros = (await _repository.GetTurnosAsync(personaIds, monthQueryStart, periodEnd))
            .Where(r => !r.NoLaboradoPorFeriado)
            .ToList();

        if (registros.Count > 0)
        {
            var turnoIds = registros
                .Select(r => r.TurnoId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (turnoIds.Count > 0)
            {
                var cambiosTurno = await _repository.GetCambiosTurnoAsync(turnoIds);
                registros = FilterTurnosByCambioEstado(registros, cambiosTurno);
            }
        }

        var equipoLookup = equipos.ToDictionary(e => e.EquipoId, e => e.NombreEquipo);
        var gruposLookup = personaGrupos
            .GroupBy(pg => pg.PersonaId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ", group.Select(x => x.GrupoNombre).Distinct()));

        var items = registros
            .GroupBy(r => r.PersonaId)
            .Select(grp =>
            {
                var weeklyGroups = grp
                    .GroupBy(r => GetWeekStart(r.Fecha))
                    .Select(weekGrp =>
                    {
                        var weekStart = weekGrp.Key;
                        var weekEnd = weekGrp.Key.AddDays(6);
                        var horas = weekGrp.Sum(r => GetHorasTurnoEnMes(r.Fecha, r.HoraInicio, r.HoraFin, periodStart, periodEnd));

                        var periodBase = weekGrp.Sum(r =>
                        {
                            var horasTurnoEnMes = GetHorasTurnoEnMes(r.Fecha, r.HoraInicio, r.HoraFin, periodStart, periodEnd);
                            var horasFeriadoEnTurno = GetHorasFeriadoTrabajadasEnMes(
                                r.Fecha,
                                r.HoraInicio,
                                r.HoraFin,
                                periodStart,
                                periodEnd,
                                feriadoDates,
                                aplicarDescuentoColacion: true);

                            return Math.Max(0, horasTurnoEnMes - horasFeriadoEnTurno);
                        });

                        double normales;
                        if (weekStart < periodStart)
                        {
                            var prePeriodEnd = periodStart.AddDays(-1);
                            var prePeriodBase = weekGrp.Sum(r =>
                            {
                                var horasTurnoPrevio = GetHorasTurnoEnMes(r.Fecha, r.HoraInicio, r.HoraFin, weekStart, prePeriodEnd);
                                var horasFeriadoPrevio = GetHorasFeriadoTrabajadasEnMes(
                                    r.Fecha,
                                    r.HoraInicio,
                                    r.HoraFin,
                                    weekStart,
                                    prePeriodEnd,
                                    feriadoDates,
                                    aplicarDescuentoColacion: true);

                                return Math.Max(0, horasTurnoPrevio - horasFeriadoPrevio);
                            });

                            var capacidadRestante = Math.Max(0, 40 - prePeriodBase);
                            normales = Math.Min(periodBase, capacidadRestante);
                        }
                        else
                        {
                            normales = Math.Min(40, periodBase);
                        }

                        var extras = Math.Max(0, periodBase - normales);

                        return new SemanaHorasItem
                        {
                            WeekStart = weekStart,
                            WeekEnd = weekEnd,
                            HorasTotales = horas,
                            HorasNormales = normales,
                            HorasExtras = extras
                        };
                    })
                    .Where(s => s.HorasTotales > 0)
                    .OrderBy(s => s.WeekStart)
                    .ToList();

                var totalHoras = weeklyGroups.Sum(w => w.HorasTotales);
                var totalNormales = weeklyGroups.Sum(w => w.HorasNormales);
                var totalExtras = weeklyGroups.Sum(w => w.HorasExtras);
                var horasFeriadoTrabajadas = grp
                    .Where(r => GetOverlapsMonth(r.Fecha, r.HoraInicio, r.HoraFin, periodStart, periodEnd))
                    .Where(r => !r.NoLaboradoPorFeriado)
                    .Sum(r => GetHorasFeriadoTrabajadasEnMes(
                        r.Fecha,
                        r.HoraInicio,
                        r.HoraFin,
                        periodStart,
                        periodEnd,
                        feriadoDates,
                        aplicarDescuentoColacion: true));

                var horasNocturnas20a6 = grp
                    .Where(r => GetOverlapsMonth(r.Fecha, r.HoraInicio, r.HoraFin, periodStart, periodEnd))
                    .Where(r => !r.NoLaboradoPorFeriado)
                    .GroupBy(r => GetWeekStart(r.Fecha))
                    .Sum(wGrp =>
                    {
                        var nocturnasEnSemana = wGrp.Sum(r => GetHorasNocturnasNoFeriado20a6(
                            r.Fecha,
                            r.HoraInicio,
                            r.HoraFin,
                            periodStart,
                            periodEnd,
                            feriadoDates));

                        var semanaInfo = weeklyGroups.FirstOrDefault(w => w.WeekStart == wGrp.Key);
                        var extrasEnSemana = semanaInfo?.HorasExtras ?? 0;
                        if (extrasEnSemana <= 0)
                        {
                            return nocturnasEnSemana;
                        }

                        var cupoNormal = semanaInfo?.HorasNormales ?? 40d;
                        var turnosOrdenados = wGrp
                            .OrderBy(r => r.Fecha)
                            .ThenBy(r => r.HoraInicio)
                            .Select(r => new
                            {
                                BaseEnPeriodo = Math.Max(0,
                                    GetHorasTurnoEnMes(r.Fecha, r.HoraInicio, r.HoraFin, periodStart, periodEnd)
                                    - GetHorasFeriadoTrabajadasEnMes(r.Fecha, r.HoraInicio, r.HoraFin, periodStart, periodEnd, feriadoDates, true)),
                                NocturnasEnPeriodo = GetHorasNocturnasNoFeriado20a6(
                                    r.Fecha,
                                    r.HoraInicio,
                                    r.HoraFin,
                                    periodStart,
                                    periodEnd,
                                    feriadoDates)
                            })
                            .ToList();

                        var acumulado = 0d;
                        var nocturnalEnExtras = 0d;
                        foreach (var turno in turnosOrdenados)
                        {
                            if (turno.BaseEnPeriodo <= 0)
                            {
                                continue;
                            }

                            if (acumulado >= cupoNormal)
                            {
                                nocturnalEnExtras += turno.NocturnasEnPeriodo;
                            }
                            else if (acumulado + turno.BaseEnPeriodo > cupoNormal)
                            {
                                var fraccionExtra = (acumulado + turno.BaseEnPeriodo - cupoNormal) / turno.BaseEnPeriodo;
                                nocturnalEnExtras += turno.NocturnasEnPeriodo * fraccionExtra;
                            }

                            acumulado += turno.BaseEnPeriodo;
                        }

                        return Math.Max(0, nocturnasEnSemana - nocturnalEnExtras);
                    });

                var weekendHours = grp
                    .Where(r => GetOverlapsMonth(r.Fecha, r.HoraInicio, r.HoraFin, periodStart, periodEnd))
                    .Where(r => IsWeekend(r.Fecha))
                    .Sum(r => GetHorasTurnoEnMes(r.Fecha, r.HoraInicio, r.HoraFin, periodStart, periodEnd));

                var weekendPercent = totalHoras > 0 ? weekendHours / totalHoras * 100 : 0;
                var porcentajeNocturnas = totalHoras > 0 ? horasNocturnas20a6 / totalHoras * 100 : 0;
                var first = grp.First();

                return new PersonaHorasMensualesItem
                {
                    PersonaId = grp.Key,
                    Nombre = first.Nombre,
                    Equipo = !string.IsNullOrWhiteSpace(first.Equipo) && equipoLookup.TryGetValue(first.Equipo, out var equipoNombre)
                        ? equipoNombre
                        : first.EquipoNombre,
                    Grupo = gruposLookup.TryGetValue(grp.Key, out var grupoNombre) ? grupoNombre : first.GrupoNombre,
                    HorasTotales = totalHoras,
                    HorasNormales = totalNormales,
                    HorasExtras = totalExtras,
                    HorasFeriadoTrabajadas = horasFeriadoTrabajadas,
                    HorasNocturnas20a6 = horasNocturnas20a6,
                    PorcentajeFinSemana = weekendPercent,
                    HorasFinSemana = weekendHours,
                    PorcentajeNocturnas = porcentajeNocturnas,
                    Semanas = weeklyGroups
                };
            })
            .Where(item => item.HorasTotales > 0)
            .ToList();

        items = ApplyHorasSorting(items, sortField, sortDirection);

        var currentPageSize = AllowedPageSizes.Contains(request.PageSize) ? request.PageSize : DefaultPageSize;
        var totalCount = items.Count;
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)currentPageSize);
        var currentPage = totalPages == 0
            ? 1
            : Math.Min(Math.Max(request.Page, 1), totalPages);

        if (applyPaging && totalCount > 0)
        {
            items = items
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .ToList();
        }

        return new HorasMensualesViewModel
        {
            FromDate = FormatDateValue(periodStart),
            ToDate = FormatDateValue(periodEnd),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            PeriodLabel = BuildPeriodLabel(periodStart, periodEnd),
            EquipoId = resolvedEquipoId,
            GrupoId = grupoId,
            PersonaId = personaId,
            ShowAdvancedFilters = actor.IsAdmin || actor.IsLider,
            Page = currentPage,
            PageSize = currentPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Equipos = equipos.Select(e => new SelectListItem
            {
                Value = e.EquipoId,
                Text = e.NombreEquipo,
                Selected = e.EquipoId == resolvedEquipoId
            }),
            Grupos = grupos.Select(g => new SelectListItem
            {
                Value = g.GrupoId,
                Text = g.NombreGrupo,
                Selected = g.GrupoId == grupoId
            }),
            Personas = personas
                .Select(p => new SelectListItem
                {
                    Value = p.PersonaId,
                    Text = BuildPersonaName(p.Nombre, p.SegundoNombre, p.Apellido, p.SegundoApellido),
                    Selected = p.PersonaId == personaId
                })
                .OrderBy(item => item.Text, StringComparer.CurrentCultureIgnoreCase),
            Items = items
        };
    }

    private static List<PersonaHorasMensualesItem> ApplyHorasSorting(
        List<PersonaHorasMensualesItem> items,
        string? sortField,
        string? sortDirection)
    {
        var sortFieldLower = (sortField ?? string.Empty).Trim().ToLowerInvariant();
        var sortDirectionLower = (sortDirection ?? "asc").Trim().ToLowerInvariant();

        return sortFieldLower switch
        {
            "horasnocturnas" => sortDirectionLower == "desc"
                ? items.OrderByDescending(i => i.HorasNocturnas20a6).ThenBy(i => i.Nombre).ToList()
                : items.OrderBy(i => i.HorasNocturnas20a6).ThenBy(i => i.Nombre).ToList(),
            "horasfindesemana" => sortDirectionLower == "desc"
                ? items.OrderByDescending(i => i.HorasFinSemana).ThenBy(i => i.Nombre).ToList()
                : items.OrderBy(i => i.HorasFinSemana).ThenBy(i => i.Nombre).ToList(),
            _ => items
                .OrderByDescending(i => i.HorasExtras)
                .ThenByDescending(i => i.HorasTotales)
                .ThenBy(i => i.Nombre)
                .ToList()
        };
    }

    private static List<HorasMensualesTurnoRow> FilterTurnosByCambioEstado(
        List<HorasMensualesTurnoRow> registros,
        IReadOnlyList<HorasMensualesCambioTurnoRow> cambiosTurno)
    {
        var visualByTurno = new Dictionary<string, TurnoCambioVisualState>(StringComparer.OrdinalIgnoreCase);

        foreach (var cambio in cambiosTurno)
        {
            UpsertVisual(visualByTurno, cambio.TurnoOrigenId, "origen", cambio.EstadoSolicitud, cambio.ActualizadoEn);
            UpsertVisual(visualByTurno, cambio.TurnoDestinoId, "destino", cambio.EstadoSolicitud, cambio.ActualizadoEn);
        }

        return registros
            .Where(row => ShouldCountTurno(row, visualByTurno))
            .ToList();
    }

    private static void UpsertVisual(
        IDictionary<string, TurnoCambioVisualState> visualByTurno,
        string turnoId,
        string role,
        SolicitudEstado estado,
        DateTime updatedAt)
    {
        var visual = GetCambioVisual(estado);
        if (string.IsNullOrWhiteSpace(turnoId) || string.IsNullOrWhiteSpace(visual.State))
        {
            return;
        }

        if (!visualByTurno.TryGetValue(turnoId, out var current))
        {
            visualByTurno[turnoId] = new TurnoCambioVisualState(visual.State!, visual.Priority, updatedAt, role);
            return;
        }

        if (visual.Priority > current.Priority ||
            (visual.Priority == current.Priority && updatedAt > current.UpdatedAt))
        {
            visualByTurno[turnoId] = new TurnoCambioVisualState(visual.State!, visual.Priority, updatedAt, role);
        }
    }

    private static bool ShouldCountTurno(
        HorasMensualesTurnoRow row,
        IReadOnlyDictionary<string, TurnoCambioVisualState> visualByTurno)
    {
        if (!visualByTurno.TryGetValue(row.TurnoId, out var visual))
        {
            return true;
        }

        var role = visual.Role.Trim().ToLowerInvariant();
        var state = visual.State.Trim().ToLowerInvariant();

        if (role == "destino" && (state == "requested" || state == "inreview" || state == "rejected" || state == "cancelled"))
        {
            return false;
        }

        if (role == "origen" && state == "approved")
        {
            return false;
        }

        return true;
    }

    private static (string? State, int Priority) GetCambioVisual(SolicitudEstado estado)
    {
        return estado switch
        {
            SolicitudEstado.Pendiente => ("requested", 4),
            SolicitudEstado.AprobadoLider => ("inreview", 3),
            SolicitudEstado.AprobadoFinal => ("approved", 2),
            SolicitudEstado.Rechazado => ("rejected", 1),
            SolicitudEstado.Cancelado => ("cancelled", 1),
            _ => (null, 0)
        };
    }

    private async Task<string?> ResolveEquipoIdAsync(string? equipoId, HorasMensualesActorContext actor)
    {
        if (actor.IsAdmin)
        {
            return equipoId;
        }

        if (string.IsNullOrWhiteSpace(actor.UserId))
        {
            return null;
        }

        var persona = await _repository.GetPersonaByUserIdAsync(actor.UserId);
        return persona?.EquipoId;
    }

    private async Task<Persona?> ResolvePersonaAsync(HorasMensualesActorContext actor)
    {
        if (string.IsNullOrWhiteSpace(actor.UserId))
        {
            return null;
        }

        return await _repository.GetPersonaByUserIdAsync(actor.UserId);
    }

    private static (DateOnly PeriodStart, DateOnly PeriodEnd) ResolvePeriod(HorasMensualesReportRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var defaultStart = new DateOnly(today.Year, today.Month, 1);
        var defaultEnd = GetLastDayOfMonth(defaultStart);
        var parsedFromDate = ParseDateValue(request.FromDate);
        var parsedToDate = ParseDateValue(request.ToDate);
        var parsedFromMonth = ParseMonthValue(request.FromMonth);
        var parsedToMonth = ParseMonthValue(request.ToMonth);

        var periodStart = parsedFromDate
            ?? parsedFromMonth
            ?? parsedToDate
            ?? parsedToMonth
            ?? defaultStart;

        var periodEnd = parsedToDate
            ?? (parsedToMonth.HasValue ? GetLastDayOfMonth(parsedToMonth.Value) : (DateOnly?)null)
            ?? parsedFromDate
            ?? (parsedFromMonth.HasValue ? GetLastDayOfMonth(parsedFromMonth.Value) : (DateOnly?)null)
            ?? defaultEnd;

        if (periodEnd < periodStart)
        {
            (periodStart, periodEnd) = (periodEnd, periodStart);
        }

        return (periodStart, periodEnd);
    }

    private static string BuildPersonaName(string? nombre, string? segundoNombre, string? apellido, string? segundoApellido)
    {
        return string.Join(" ", new[] { nombre, segundoNombre, apellido, segundoApellido }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static decimal NormalizarTarifa(decimal? valor, decimal valorDefault)
    {
        if (!valor.HasValue)
        {
            return valorDefault;
        }

        return Math.Clamp(valor.Value, 0m, 10_000m);
    }

    private static decimal NormalizarPorcentaje(decimal? valor, decimal valorDefault)
    {
        if (!valor.HasValue)
        {
            return valorDefault;
        }

        return Math.Clamp(valor.Value, 0m, 500m);
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var offset = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return date.AddDays(-offset);
    }

    private static bool IsWeekend(DateOnly date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    private static double GetHorasTurnoEnMes(DateOnly fecha, TimeOnly inicio, TimeOnly fin, DateOnly monthStart, DateOnly monthEnd)
    {
        var (shiftStart, shiftEnd) = GetShiftRange(fecha, inicio, fin);
        var (rangeStart, rangeEnd) = GetMonthRange(monthStart, monthEnd);

        var horasEnMes = GetOverlapHours(shiftStart, shiftEnd, rangeStart, rangeEnd);
        if (horasEnMes <= 0)
        {
            return 0;
        }

        if (horasEnMes > 6)
        {
            horasEnMes -= 1;
        }

        return Math.Max(0, horasEnMes);
    }

    private static bool GetOverlapsMonth(DateOnly fecha, TimeOnly inicio, TimeOnly fin, DateOnly monthStart, DateOnly monthEnd)
    {
        var (shiftStart, shiftEnd) = GetShiftRange(fecha, inicio, fin);
        var (rangeStart, rangeEnd) = GetMonthRange(monthStart, monthEnd);
        return GetOverlapHours(shiftStart, shiftEnd, rangeStart, rangeEnd) > 0;
    }

    private static double GetHorasNocturnasNoFeriado20a6(
        DateOnly fecha,
        TimeOnly inicio,
        TimeOnly fin,
        DateOnly monthStart,
        DateOnly monthEnd,
        ISet<DateOnly> feriadoDates)
    {
        var (shiftStart, shiftEnd) = GetShiftRange(fecha, inicio, fin);
        var (rangeStart, rangeEnd) = GetMonthRange(monthStart, monthEnd);

        var effectiveStart = shiftStart > rangeStart ? shiftStart : rangeStart;
        var effectiveEnd = shiftEnd < rangeEnd ? shiftEnd : rangeEnd;
        if (effectiveEnd <= effectiveStart)
        {
            return 0;
        }

        var total = 0d;
        var dayCursor = DateOnly.FromDateTime(effectiveStart.Date);
        var lastDay = DateOnly.FromDateTime(effectiveEnd.Date);

        while (dayCursor <= lastDay)
        {
            if (!feriadoDates.Contains(dayCursor))
            {
                var dayStart = dayCursor.ToDateTime(TimeOnly.MinValue);
                var dayEnd = dayCursor.AddDays(1).ToDateTime(TimeOnly.MinValue);
                var earlyNightEnd = dayStart.AddHours(6);
                var lateNightStart = dayStart.AddHours(20);

                total += GetOverlapHours(effectiveStart, effectiveEnd, dayStart, earlyNightEnd);
                total += GetOverlapHours(effectiveStart, effectiveEnd, lateNightStart, dayEnd);
            }

            dayCursor = dayCursor.AddDays(1);
        }

        return total;
    }

    private static (DateTime Start, DateTime End) GetShiftRange(DateOnly fecha, TimeOnly inicio, TimeOnly fin)
    {
        var start = fecha.ToDateTime(inicio);
        var end = fecha.ToDateTime(fin);
        if (end <= start)
        {
            end = end.AddDays(1);
        }

        return (start, end);
    }

    private static (DateTime Start, DateTime End) GetMonthRange(DateOnly monthStart, DateOnly monthEnd)
    {
        var start = monthStart.ToDateTime(TimeOnly.MinValue);
        var end = monthEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return (start, end);
    }

    private static double GetOverlapHours(DateTime startA, DateTime endA, DateTime startB, DateTime endB)
    {
        var start = startA > startB ? startA : startB;
        var end = endA < endB ? endA : endB;
        return end > start ? (end - start).TotalHours : 0;
    }

    private static HashSet<DateOnly> BuildFeriadoDateSet(IEnumerable<Feriado> feriados, DateOnly windowStart, DateOnly windowEnd)
    {
        var dates = new HashSet<DateOnly>();

        foreach (var feriado in feriados)
        {
            foreach (var (inicio, fin) in ExpandFeriadoOccurrences(feriado, windowStart, windowEnd))
            {
                for (var day = inicio; day <= fin; day = day.AddDays(1))
                {
                    dates.Add(day);
                }
            }
        }

        return dates;
    }

    private static IEnumerable<(DateOnly Inicio, DateOnly Fin)> ExpandFeriadoOccurrences(
        Feriado feriado,
        DateOnly windowStart,
        DateOnly windowEnd)
    {
        if (RangesOverlap(feriado.InicioFeriado, feriado.FinFeriado, windowStart, windowEnd))
        {
            yield return (feriado.InicioFeriado, feriado.FinFeriado);
        }
    }

    private static double GetHorasFeriadoTrabajadasEnMes(
        DateOnly fecha,
        TimeOnly inicio,
        TimeOnly fin,
        DateOnly monthStart,
        DateOnly monthEnd,
        ISet<DateOnly> feriadoDates,
        bool aplicarDescuentoColacion = true)
    {
        var (shiftStart, shiftEnd) = GetShiftRange(fecha, inicio, fin);
        var (rangeStart, rangeEnd) = GetMonthRange(monthStart, monthEnd);

        var effectiveStart = shiftStart > rangeStart ? shiftStart : rangeStart;
        var effectiveEnd = shiftEnd < rangeEnd ? shiftEnd : rangeEnd;
        if (effectiveEnd <= effectiveStart)
        {
            return 0;
        }

        var total = 0d;
        var dayCursor = DateOnly.FromDateTime(effectiveStart.Date);
        var lastDay = DateOnly.FromDateTime(effectiveEnd.Date);

        while (dayCursor <= lastDay)
        {
            if (feriadoDates.Contains(dayCursor))
            {
                var dayStart = dayCursor.ToDateTime(TimeOnly.MinValue);
                var dayEnd = dayCursor.AddDays(1).ToDateTime(TimeOnly.MinValue);
                var horasDiaFeriado = GetOverlapHours(effectiveStart, effectiveEnd, dayStart, dayEnd);
                if (aplicarDescuentoColacion && horasDiaFeriado > 6)
                {
                    horasDiaFeriado -= 1;
                }

                total += horasDiaFeriado;
            }

            dayCursor = dayCursor.AddDays(1);
        }

        return Math.Max(0, total);
    }

    private static bool RangesOverlap(DateOnly aStart, DateOnly aEnd, DateOnly bStart, DateOnly bEnd)
    {
        return aStart <= bEnd && aEnd >= bStart;
    }

    private static byte[] BuildPdfReport(HorasMensualesViewModel model, string title, string filterSummary)
    {
        var document = new PdfDocument();
        var titleFont = new XFont("Arial", 17, XFontStyle.Bold);
        var subtitleFont = new XFont("Arial", 9, XFontStyle.Regular);
        var sectionFont = new XFont("Arial", 8, XFontStyle.Bold);
        var metricValueFont = new XFont("Arial", 14, XFontStyle.Bold);
        var headerFont = new XFont("Arial", 8, XFontStyle.Bold);
        var cellFont = new XFont("Arial", 8, XFontStyle.Regular);
        var cellBoldFont = new XFont("Arial", 8, XFontStyle.Bold);
        var footerFont = new XFont("Arial", 8, XFontStyle.Regular);
        var inkBrush = new XSolidBrush(XColor.FromArgb(33, 37, 41));
        var mutedBrush = new XSolidBrush(XColor.FromArgb(94, 108, 122));
        var whiteBrush = XBrushes.White;
        var headerBrush = new XSolidBrush(XColor.FromArgb(21, 67, 96));
        var headerAccentBrush = new XSolidBrush(XColor.FromArgb(39, 97, 142));
        var panelBrush = new XSolidBrush(XColor.FromArgb(247, 249, 252));
        var rowAltBrush = new XSolidBrush(XColor.FromArgb(250, 252, 255));
        var tableHeaderBrush = new XSolidBrush(XColor.FromArgb(35, 88, 126));
        var extrasCellBrush = new XSolidBrush(XColor.FromArgb(255, 243, 230));
        var panelBorderPen = new XPen(XColor.FromArgb(222, 228, 235), 1);
        var rowBorderPen = new XPen(XColor.FromArgb(228, 234, 240), 0.8);
        var footerPen = new XPen(XColor.FromArgb(220, 226, 232), 1);

        const double marginX = 24;
        const double marginTop = 22;
        const double marginBottom = 20;
        const double footerHeight = 18;
        const double headerHeight = 58;
        const double rowHeight = 20;
        const double cardHeight = 56;
        const double cardGap = 10;
        const double filterLineHeight = 12;

        var generatedAt = DateTime.Now;
        var totalHoras = model.Items.Sum(i => i.HorasTotales);
        var totalExtras = model.Items.Sum(i => i.HorasExtras);
        var totalNocturnas = model.Items.Sum(i => i.HorasNocturnas20a6Display);
        var totalFeriado = model.Items.Sum(i => i.HorasFeriadoTrabajadas);

        PdfPage? page = null;
        XGraphics? gfx = null;
        var y = 0d;
        var pageNumber = 0;

        var tableX = 0d;
        var tableWidth = 0d;

        var widthIndex = 28d;
        var widthPersona = 0d;
        var widthEquipo = 92d;
        var widthGrupo = 104d;
        var widthHoras = 60d;
        var widthNormales = 62d;
        var widthExtra = 58d;
        var widthPorcentajeNocturnas = 72d;
        var widthNocturnas = 72d;
        var widthFeriado = 62d;
        var widthFinSemanaPercent = 60d;
        var widthFinSemanaHoras = 72d;

        var colIndex = 0d;
        var colPersona = 0d;
        var colEquipo = 0d;
        var colGrupo = 0d;
        var colHoras = 0d;
        var colNormales = 0d;
        var colExtra = 0d;
        var colPorcentajeNocturnas = 0d;
        var colNocturnas = 0d;
        var colFeriado = 0d;
        var colFinSemanaPercent = 0d;
        var colFinSemanaHoras = 0d;

        void DrawFooter()
        {
            if (page is null || gfx is null)
            {
                return;
            }

            var footerY = page.Height - marginBottom - footerHeight;
            gfx.DrawLine(footerPen, marginX, footerY, page.Width - marginX, footerY);
            gfx.DrawString($"Generado: {generatedAt:yyyy-MM-dd HH:mm}", footerFont, mutedBrush, new XRect(marginX, footerY + 3, 240, footerHeight), XStringFormats.TopLeft);
            gfx.DrawString($"Pagina {pageNumber}", footerFont, mutedBrush, new XRect(page.Width - marginX - 120, footerY + 3, 120, footerHeight), XStringFormats.TopRight);
        }

        void DrawMetricCard(double x, double cardWidth, string label, string value, XColor accentColor)
        {
            if (gfx is null)
            {
                return;
            }

            gfx.DrawRectangle(XBrushes.White, x, y, cardWidth, cardHeight);
            gfx.DrawRectangle(panelBorderPen, x, y, cardWidth, cardHeight);
            gfx.DrawRectangle(new XSolidBrush(accentColor), x, y, 4, cardHeight);
            gfx.DrawString(label, sectionFont, mutedBrush, new XRect(x + 10, y + 10, cardWidth - 16, 14), XStringFormats.TopLeft);
            gfx.DrawString(value, metricValueFont, inkBrush, new XRect(x + 10, y + 24, cardWidth - 16, 22), XStringFormats.TopLeft);
        }

        void ComputeTableColumns()
        {
            if (page is null)
            {
                return;
            }

            tableX = marginX;
            tableWidth = page.Width - 2 * marginX;

            var fixedColumnsWidth =
                widthIndex + widthEquipo + widthGrupo + widthHoras + widthNormales + widthExtra + widthPorcentajeNocturnas + widthNocturnas + widthFeriado + widthFinSemanaPercent + widthFinSemanaHoras;

            widthPersona = Math.Max(155, tableWidth - fixedColumnsWidth);

            colIndex = tableX;
            colPersona = colIndex + widthIndex;
            colEquipo = colPersona + widthPersona;
            colGrupo = colEquipo + widthEquipo;
            colHoras = colGrupo + widthGrupo;
            colNormales = colHoras + widthHoras;
            colExtra = colNormales + widthNormales;
            colPorcentajeNocturnas = colExtra + widthExtra;
            colNocturnas = colPorcentajeNocturnas + widthPorcentajeNocturnas;
            colFeriado = colNocturnas + widthNocturnas;
            colFinSemanaPercent = colFeriado + widthFeriado;
            colFinSemanaHoras = colFinSemanaPercent + widthFinSemanaPercent;
        }

        void DrawTableHeader()
        {
            if (gfx is null)
            {
                return;
            }

            gfx.DrawRectangle(tableHeaderBrush, tableX, y, tableWidth, rowHeight);
            gfx.DrawString("#", headerFont, whiteBrush, new XRect(colIndex + 4, y + 5, widthIndex - 8, rowHeight), XStringFormats.TopLeft);
            gfx.DrawString("Persona", headerFont, whiteBrush, new XRect(colPersona + 4, y + 5, widthPersona - 8, rowHeight), XStringFormats.TopLeft);
            gfx.DrawString("Equipo", headerFont, whiteBrush, new XRect(colEquipo + 4, y + 5, widthEquipo - 8, rowHeight), XStringFormats.TopLeft);
            gfx.DrawString("Grupo", headerFont, whiteBrush, new XRect(colGrupo + 4, y + 5, widthGrupo - 8, rowHeight), XStringFormats.TopLeft);
            gfx.DrawString("Totales", headerFont, whiteBrush, new XRect(colHoras + 3, y + 5, widthHoras - 6, rowHeight), XStringFormats.TopRight);
            gfx.DrawString("Normales", headerFont, whiteBrush, new XRect(colNormales + 3, y + 5, widthNormales - 6, rowHeight), XStringFormats.TopRight);
            gfx.DrawString("Extra", headerFont, whiteBrush, new XRect(colExtra + 3, y + 5, widthExtra - 6, rowHeight), XStringFormats.TopRight);
            gfx.DrawString("% Noct.", headerFont, whiteBrush, new XRect(colPorcentajeNocturnas + 3, y + 5, widthPorcentajeNocturnas - 6, rowHeight), XStringFormats.TopRight);
            gfx.DrawString("Noct. 20-6", headerFont, whiteBrush, new XRect(colNocturnas + 3, y + 5, widthNocturnas - 6, rowHeight), XStringFormats.TopRight);
            gfx.DrawString("Feriado", headerFont, whiteBrush, new XRect(colFeriado + 3, y + 5, widthFeriado - 6, rowHeight), XStringFormats.TopRight);
            gfx.DrawString("% F/S", headerFont, whiteBrush, new XRect(colFinSemanaPercent + 3, y + 5, widthFinSemanaPercent - 6, rowHeight), XStringFormats.TopRight);
            gfx.DrawString("F/S (Hrs)", headerFont, whiteBrush, new XRect(colFinSemanaHoras + 3, y + 5, widthFinSemanaHoras - 6, rowHeight), XStringFormats.TopRight);
            y += rowHeight;
        }

        void DrawPageHeader(bool includeOverview)
        {
            if (page is null || gfx is null)
            {
                return;
            }

            var contentWidth = page.Width - 2 * marginX;

            gfx.DrawRectangle(headerBrush, marginX, y, contentWidth, headerHeight);
            gfx.DrawRectangle(headerAccentBrush, marginX, y + headerHeight - 6, contentWidth, 6);

            gfx.DrawString(title, titleFont, whiteBrush, new XRect(marginX + 14, y + 10, contentWidth - 250, 20), XStringFormats.TopLeft);
            gfx.DrawString("Resumen de horas por colaborador", subtitleFont, whiteBrush, new XRect(marginX + 14, y + 33, contentWidth - 250, 14), XStringFormats.TopLeft);
            gfx.DrawString(model.PeriodLabel, sectionFont, whiteBrush, new XRect(marginX + contentWidth - 220, y + 12, 206, 14), XStringFormats.TopRight);
            gfx.DrawString($"{model.Items.Count} personas", subtitleFont, whiteBrush, new XRect(marginX + contentWidth - 190, y + 30, 176, 14), XStringFormats.TopRight);
            y += headerHeight + 10;

            if (includeOverview)
            {
                var filterLines = SplitTextToLines(filterSummary, gfx, subtitleFont, contentWidth - 16);
                var filterHeight = 20 + (filterLines.Count * filterLineHeight) + 8;
                gfx.DrawRectangle(panelBrush, marginX, y, contentWidth, filterHeight);
                gfx.DrawRectangle(panelBorderPen, marginX, y, contentWidth, filterHeight);
                gfx.DrawString("Filtros aplicados", sectionFont, mutedBrush, new XRect(marginX + 8, y + 6, contentWidth - 16, 12), XStringFormats.TopLeft);

                var textY = y + 18;
                foreach (var line in filterLines)
                {
                    gfx.DrawString(line, subtitleFont, inkBrush, new XRect(marginX + 8, textY, contentWidth - 16, filterLineHeight), XStringFormats.TopLeft);
                    textY += filterLineHeight;
                }

                y += filterHeight + 10;

                var cardWidth = (contentWidth - (3 * cardGap)) / 4;
                DrawMetricCard(marginX, cardWidth, "Personas", model.Items.Count.ToString(CultureInfo.InvariantCulture), XColor.FromArgb(39, 97, 142));
                DrawMetricCard(marginX + cardWidth + cardGap, cardWidth, "Horas Totales", totalHoras.ToString("0.##", CultureInfo.InvariantCulture), XColor.FromArgb(46, 125, 50));
                DrawMetricCard(marginX + (2 * (cardWidth + cardGap)), cardWidth, "Horas Extra", totalExtras.ToString("0.##", CultureInfo.InvariantCulture), XColor.FromArgb(211, 84, 0));
                DrawMetricCard(marginX + (3 * (cardWidth + cardGap)), cardWidth, "Nocturnas 18-6", totalNocturnas.ToString(CultureInfo.InvariantCulture), XColor.FromArgb(86, 101, 115));
                y += cardHeight + 12;
            }
            else
            {
                gfx.DrawRectangle(panelBrush, marginX, y, contentWidth, 22);
                gfx.DrawRectangle(panelBorderPen, marginX, y, contentWidth, 22);
                gfx.DrawString("Detalle por persona (continuacion)", sectionFont, mutedBrush, new XRect(marginX + 8, y + 6, contentWidth - 16, 12), XStringFormats.TopLeft);
                y += 28;
            }

            gfx.DrawString($"Total horas feriado trabajadas: {totalFeriado:0.##}", sectionFont, mutedBrush, new XRect(marginX, y, contentWidth, 12), XStringFormats.TopLeft);
            y += 14;
        }

        void StartPage(bool includeOverview)
        {
            if (gfx is not null)
            {
                DrawFooter();
                gfx.Dispose();
            }

            page = document.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            page.Orientation = PdfSharpCore.PageOrientation.Landscape;
            gfx = XGraphics.FromPdfPage(page);
            pageNumber++;
            y = marginTop;

            DrawPageHeader(includeOverview);
            ComputeTableColumns();
            DrawTableHeader();
        }

        StartPage(includeOverview: true);

        if (!model.Items.Any())
        {
            if (page is not null && gfx is not null)
            {
                var emptyHeight = 42d;
                var contentWidth = page.Width - 2 * marginX;
                if (y + emptyHeight > page.Height - marginBottom - footerHeight)
                {
                    StartPage(includeOverview: false);
                }

                gfx.DrawRectangle(panelBrush, marginX, y, contentWidth, emptyHeight);
                gfx.DrawRectangle(panelBorderPen, marginX, y, contentWidth, emptyHeight);
                gfx.DrawString("No hay registros para el periodo seleccionado.", cellBoldFont, mutedBrush, new XRect(marginX + 10, y + 14, contentWidth - 20, 14), XStringFormats.TopLeft);
                y += emptyHeight;
            }
        }
        else
        {
            for (var i = 0; i < model.Items.Count; i++)
            {
                var item = model.Items[i];

                if (page is null || gfx is null)
                {
                    break;
                }

                var nextRowBottom = y + rowHeight;
                var availableBottom = page.Height - marginBottom - footerHeight;
                if (nextRowBottom > availableBottom)
                {
                    StartPage(includeOverview: false);
                }

                if (page is null || gfx is null)
                {
                    break;
                }

                if (i % 2 == 1)
                {
                    gfx.DrawRectangle(rowAltBrush, tableX, y, tableWidth, rowHeight);
                }

                if (item.HorasExtras > 0)
                {
                    gfx.DrawRectangle(extrasCellBrush, colExtra, y + 1, widthExtra, rowHeight - 2);
                }

                gfx.DrawString((i + 1).ToString(CultureInfo.InvariantCulture), cellFont, mutedBrush, new XRect(colIndex + 4, y + 5, widthIndex - 8, rowHeight), XStringFormats.TopLeft);
                gfx.DrawString(TrimTextToWidth(item.Nombre, gfx, cellFont, widthPersona - 8), cellFont, inkBrush, new XRect(colPersona + 4, y + 5, widthPersona - 8, rowHeight), XStringFormats.TopLeft);
                gfx.DrawString(TrimTextToWidth(item.Equipo, gfx, cellFont, widthEquipo - 8), cellFont, inkBrush, new XRect(colEquipo + 4, y + 5, widthEquipo - 8, rowHeight), XStringFormats.TopLeft);
                gfx.DrawString(TrimTextToWidth(item.Grupo, gfx, cellFont, widthGrupo - 8), cellFont, inkBrush, new XRect(colGrupo + 4, y + 5, widthGrupo - 8, rowHeight), XStringFormats.TopLeft);
                gfx.DrawString(item.HorasTotales.ToString("0.##", CultureInfo.InvariantCulture), cellFont, inkBrush, new XRect(colHoras + 3, y + 5, widthHoras - 6, rowHeight), XStringFormats.TopRight);
                gfx.DrawString(item.HorasNormales.ToString("0.##", CultureInfo.InvariantCulture), cellFont, inkBrush, new XRect(colNormales + 3, y + 5, widthNormales - 6, rowHeight), XStringFormats.TopRight);
                gfx.DrawString(item.HorasExtras.ToString("0.##", CultureInfo.InvariantCulture), cellBoldFont, inkBrush, new XRect(colExtra + 3, y + 5, widthExtra - 6, rowHeight), XStringFormats.TopRight);
                gfx.DrawString($"{item.PorcentajeNocturnas:0.#}%", cellFont, inkBrush, new XRect(colPorcentajeNocturnas + 3, y + 5, widthPorcentajeNocturnas - 6, rowHeight), XStringFormats.TopRight);
                gfx.DrawString(item.HorasNocturnas20a6Display.ToString(CultureInfo.InvariantCulture), cellFont, inkBrush, new XRect(colNocturnas + 3, y + 5, widthNocturnas - 6, rowHeight), XStringFormats.TopRight);
                gfx.DrawString(item.HorasFeriadoTrabajadas.ToString("0.##", CultureInfo.InvariantCulture), cellFont, inkBrush, new XRect(colFeriado + 3, y + 5, widthFeriado - 6, rowHeight), XStringFormats.TopRight);
                gfx.DrawString($"{item.PorcentajeFinSemana:0.#}%", cellFont, inkBrush, new XRect(colFinSemanaPercent + 3, y + 5, widthFinSemanaPercent - 6, rowHeight), XStringFormats.TopRight);
                gfx.DrawString(item.HorasFinSemana.ToString("0.##", CultureInfo.InvariantCulture), cellFont, inkBrush, new XRect(colFinSemanaHoras + 3, y + 5, widthFinSemanaHoras - 6, rowHeight), XStringFormats.TopRight);
                y += rowHeight;
                gfx.DrawLine(rowBorderPen, tableX, y, tableX + tableWidth, y);
            }
        }

        if (gfx is not null)
        {
            DrawFooter();
            gfx.Dispose();
        }

        using var ms = new MemoryStream();
        document.Save(ms, false);
        return ms.ToArray();
    }

    private static List<string> SplitTextToLines(string text, XGraphics gfx, XFont font, double maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string> { "-" };
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            if (gfx.MeasureString(candidate, font).Width <= maxWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            if (gfx.MeasureString(word, font).Width <= maxWidth)
            {
                currentLine = word;
                continue;
            }

            lines.Add(TrimTextToWidth(word, gfx, font, maxWidth));
            currentLine = string.Empty;
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }

        return lines.Count > 0 ? lines : new List<string> { "-" };
    }

    private static string TrimTextToWidth(string text, XGraphics gfx, XFont font, double maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "-";
        }

        var trimmed = text.Trim();
        if (gfx.MeasureString(trimmed, font).Width <= maxWidth)
        {
            return trimmed;
        }

        const string ellipsis = "...";
        var ellipsisWidth = gfx.MeasureString(ellipsis, font).Width;
        if (ellipsisWidth >= maxWidth)
        {
            return ellipsis;
        }

        var allowedWidth = maxWidth - ellipsisWidth;
        var length = trimmed.Length;
        while (length > 0 && gfx.MeasureString(trimmed[..length], font).Width > allowedWidth)
        {
            length--;
        }

        return length <= 0 ? ellipsis : $"{trimmed[..length].TrimEnd()}{ellipsis}";
    }

    private static string BuildFilterSummary(HorasMensualesViewModel model)
    {
        var parts = new List<string>
        {
            $"Periodo: {model.PeriodLabel}"
        };

        if (!string.IsNullOrWhiteSpace(model.EquipoId))
        {
            parts.Add($"Equipo: {GetSelectedText(model.Equipos, model.EquipoId)}");
        }

        if (!string.IsNullOrWhiteSpace(model.GrupoId))
        {
            parts.Add($"Grupo: {GetSelectedText(model.Grupos, model.GrupoId)}");
        }

        if (!string.IsNullOrWhiteSpace(model.PersonaId))
        {
            parts.Add($"Persona: {GetSelectedText(model.Personas, model.PersonaId)}");
        }

        return string.Join(" | ", parts);
    }

    private static string GetSelectedText(IEnumerable<SelectListItem> items, string value)
    {
        return items.FirstOrDefault(i => i.Value == value)?.Text ?? value;
    }

    private static DateOnly? ParseDateValue(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (DateOnly.TryParseExact(
            rawValue.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static DateOnly? ParseMonthValue(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (DateTime.TryParseExact(
            rawValue.Trim(),
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed))
        {
            return new DateOnly(parsed.Year, parsed.Month, 1);
        }

        return null;
    }

    private static DateOnly GetLastDayOfMonth(DateOnly value)
    {
        return new DateOnly(value.Year, value.Month, DateTime.DaysInMonth(value.Year, value.Month));
    }

    private static string FormatDateValue(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string BuildPeriodLabel(DateOnly start, DateOnly end)
    {
        if (start == end)
        {
            return FormatDayLabel(start);
        }

        if (IsWholeMonthRange(start, end))
        {
            if (start.Year == end.Year && start.Month == end.Month)
            {
                return FormatMonthYear(start);
            }

            return $"{FormatMonthYear(start)} - {FormatMonthYear(end)}";
        }

        return $"{FormatDayLabel(start)} - {FormatDayLabel(end)}";
    }

    private static string BuildPeriodFileToken(DateOnly start, DateOnly end)
    {
        var startToken = FormatDateValue(start);
        var endToken = FormatDateValue(end);
        return startToken == endToken ? startToken : $"{startToken}_a_{endToken}";
    }

    private static bool IsWholeMonthRange(DateOnly start, DateOnly end)
    {
        return start.Day == 1
            && end == GetLastDayOfMonth(end)
            && new DateOnly(start.Year, start.Month, 1) <= new DateOnly(end.Year, end.Month, 1);
    }

    private static string FormatMonthYear(DateOnly value)
    {
        return value.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    }

    private static string FormatDayLabel(DateOnly value)
    {
        return value.ToDateTime(TimeOnly.MinValue).ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
    }

    private static string FormatSpanishDayName(DateOnly value)
    {
        var culture = new CultureInfo("es-EC");
        var dayName = value.ToDateTime(TimeOnly.MinValue).ToString("dddd", culture);
        if (string.IsNullOrWhiteSpace(dayName))
        {
            return string.Empty;
        }

        return char.ToUpper(dayName[0], culture) + dayName[1..];
    }

    private sealed record HorasMensualesExcelDetailRow(
        string Ultimatix,
        string PersonaId,
        string Nombre,
        DateOnly Fecha,
        TimeOnly HoraIngreso,
        TimeOnly HoraSalida,
        decimal HorasRecargo);

    private sealed record TurnoCambioVisualState(string State, int Priority, DateTime UpdatedAt, string Role);
}
