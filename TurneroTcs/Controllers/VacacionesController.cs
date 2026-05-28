using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Controllers;

[Authorize(Policy = "UserAbove")]
public class VacacionesController : Controller
{
    private const int DefaultPageSize = 15;
    private static readonly int[] AllowedPageSizes = [15, 25, 50, 100];
    private readonly ApplicationDbContext _db;
    private readonly ILogger<VacacionesController> _logger;

    public VacacionesController(ApplicationDbContext db, ILogger<VacacionesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int? year, int? month, int page = 1, int pageSize = DefaultPageSize)
    {
        var accessContext = await BuildAccessContextAsync();
        if (accessContext is null)
        {
            return Unauthorized();
        }

        return View(await BuildIndexViewModelAsync(accessContext, search: search, year: year, month: month, page: page, pageSize: pageSize));
    }

    [HttpPost]
    [Authorize(Policy = "LiderAbove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Create")] VacacionCargaViewModel createModel)
    {
        var accessContext = await BuildAccessContextAsync();
        if (accessContext is null)
        {
            return Unauthorized();
        }

        if (!accessContext.CanCreate)
        {
            return Forbid();
        }

        if (createModel.FechaInicio.HasValue && createModel.FechaFin.HasValue &&
            createModel.FechaFin.Value < createModel.FechaInicio.Value)
        {
            ModelState.AddModelError("Create.FechaFin", "La fecha fin no puede ser menor a la fecha inicio.");
        }

        var targetPersona = await _db.Personas
            .AsNoTracking()
            .Where(p => p.PersonaId == createModel.PersonaId && !p.Borrado)
            .Select(p => new { p.PersonaId, p.EquipoId })
            .FirstOrDefaultAsync();

        if (targetPersona == null)
        {
            ModelState.AddModelError("Create.PersonaId", "La persona seleccionada no existe.");
        }
        else if (!CanManagePersona(accessContext, targetPersona.PersonaId, targetPersona.EquipoId))
        {
            ModelState.AddModelError("Create.PersonaId", "No tienes permisos para registrar vacaciones para esta persona.");
        }

        var tipoSolicitudId = await ResolveVacacionTipoSolicitudIdAsync();
        if (string.IsNullOrWhiteSpace(tipoSolicitudId))
        {
            ModelState.AddModelError("Create.PersonaId", "No se encontro el tipo de solicitud para vacaciones.");
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildIndexViewModelAsync(accessContext, createModel: createModel));
        }

        var fechaInicio = createModel.FechaInicio!.Value;
        var fechaFin = createModel.FechaFin!.Value;

        var hasOverlap = await (
            from v in _db.Vacaciones.AsNoTracking()
            join s in _db.Solicitudes.AsNoTracking() on v.SolicitudId equals s.SolicitudId
            where s.PersonaSolicitanteId == createModel.PersonaId
                  && s.EstadoSolicitud != SolicitudEstado.Rechazado
                  && s.EstadoSolicitud != SolicitudEstado.Cancelado
                  && v.FechaInicio <= fechaFin
                  && fechaInicio <= v.FechaFin
            select v.VacacionId)
            .AnyAsync();

        if (hasOverlap)
        {
            ModelState.AddModelError("Create.FechaInicio", "Ya existe un periodo de vacaciones superpuesto para la persona seleccionada.");
            return View(nameof(Index), await BuildIndexViewModelAsync(accessContext, createModel: createModel));
        }

        var now = DateTime.UtcNow;
        var solicitud = new Solicitud
        {
            SolicitudId = Guid.NewGuid().ToString("N"),
            PersonaSolicitanteId = createModel.PersonaId,
            TipoSolicitudId = tipoSolicitudId!,
            EstadoSolicitud = SolicitudEstado.AprobadoFinal,
            FechaSolicitud = now,
            FechaAprobacion1 = now,
            FechaAprobacion2 = now,
            PersonaAprobador1Id = accessContext.CurrentPersonaId,
            PersonaAprobador2Id = accessContext.CurrentPersonaId,
            CreadoEn = now,
            ActualizadoEn = now
        };

        var vacacion = new Vacacion
        {
            VacacionId = Guid.NewGuid().ToString("N"),
            SolicitudId = solicitud.SolicitudId,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin
        };

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Solicitudes.Add(solicitud);
            _db.Vacaciones.Add(vacacion);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            TempData["Success"] = "Vacacion registrada correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error al crear vacacion manual para persona {PersonaId}.", createModel.PersonaId);
            ModelState.AddModelError("Create.PersonaId", "No se pudo registrar la vacacion. Intenta nuevamente.");
            return View(nameof(Index), await BuildIndexViewModelAsync(accessContext, createModel: createModel));
        }
    }

    [HttpPost]
    [Authorize(Policy = "LiderAbove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBulk([Bind(Prefix = "BulkCreate")] VacacionCargaMasivaViewModel bulkModel)
    {
        var accessContext = await BuildAccessContextAsync();
        if (accessContext is null)
        {
            return Unauthorized();
        }

        if (!accessContext.CanCreate)
        {
            return Forbid();
        }

        var rows = (bulkModel.Items ?? new List<VacacionCargaItemViewModel>())
            .Where(item => !string.IsNullOrWhiteSpace(item.PersonaId))
            .ToList();

        if (rows.Count == 0)
        {
            ModelState.AddModelError("BulkCreate.Items", "Agrega al menos una persona.");
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!row.FechaInicio.HasValue)
            {
                ModelState.AddModelError($"BulkCreate.Items[{i}].FechaInicio", "La fecha inicio es requerida.");
            }

            if (!row.FechaFin.HasValue)
            {
                ModelState.AddModelError($"BulkCreate.Items[{i}].FechaFin", "La fecha fin es requerida.");
            }

            if (row.FechaInicio.HasValue && row.FechaFin.HasValue && row.FechaFin.Value < row.FechaInicio.Value)
            {
                ModelState.AddModelError($"BulkCreate.Items[{i}].FechaFin", "La fecha fin no puede ser menor a la fecha inicio.");
            }
        }

        var personaIds = rows
            .Select(item => item.PersonaId)
            .Distinct()
            .ToList();

        var targetPersonas = await _db.Personas
            .AsNoTracking()
            .Where(p => personaIds.Contains(p.PersonaId) && !p.Borrado)
            .Select(p => new { p.PersonaId, p.EquipoId })
            .ToListAsync();

        if (targetPersonas.Count != personaIds.Count)
        {
            ModelState.AddModelError("BulkCreate.Items", "Una o mas personas no existen o fueron borradas.");
        }

        var unauthorizedTargets = targetPersonas
            .Where(p => !CanManagePersona(accessContext, p.PersonaId, p.EquipoId))
            .ToList();

        if (unauthorizedTargets.Count > 0)
        {
            ModelState.AddModelError("BulkCreate.Items", "No tienes permisos para registrar vacaciones para una o mas personas seleccionadas.");
        }

        var tipoSolicitudId = await ResolveVacacionTipoSolicitudIdAsync();
        if (string.IsNullOrWhiteSpace(tipoSolicitudId))
        {
            ModelState.AddModelError("BulkCreate.Items", "No se encontro el tipo de solicitud para vacaciones.");
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildIndexViewModelAsync(accessContext, bulkCreateModel: bulkModel));
        }

        var existingRangesRows = await (
            from v in _db.Vacaciones.AsNoTracking()
            join s in _db.Solicitudes.AsNoTracking() on v.SolicitudId equals s.SolicitudId
            where personaIds.Contains(s.PersonaSolicitanteId)
                && s.EstadoSolicitud != SolicitudEstado.Rechazado
                && s.EstadoSolicitud != SolicitudEstado.Cancelado
            select new
            {
                PersonaId = s.PersonaSolicitanteId,
                v.FechaInicio,
                v.FechaFin
            })
            .ToListAsync();

        var existingRangesByPersona = existingRangesRows
            .GroupBy(x => x.PersonaId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => (x.FechaInicio, x.FechaFin)).ToList());

        static bool Overlaps(DateOnly start, DateOnly end, List<(DateOnly Start, DateOnly End)> ranges)
            => ranges.Any(r => r.Start <= end && start <= r.End);

        var now = DateTime.UtcNow;
        var solicitudes = new List<Solicitud>(rows.Count);
        var vacaciones = new List<Vacacion>(rows.Count);
        var omittedCount = 0;

        foreach (var row in rows)
        {
            var start = row.FechaInicio!.Value;
            var end = row.FechaFin!.Value;
            if (!existingRangesByPersona.TryGetValue(row.PersonaId, out var ranges))
            {
                ranges = new List<(DateOnly Start, DateOnly End)>();
                existingRangesByPersona[row.PersonaId] = ranges;
            }

            if (Overlaps(start, end, ranges))
            {
                omittedCount++;
                continue;
            }

            var solicitudId = Guid.NewGuid().ToString("N");
            solicitudes.Add(new Solicitud
            {
                SolicitudId = solicitudId,
                PersonaSolicitanteId = row.PersonaId,
                TipoSolicitudId = tipoSolicitudId!,
                EstadoSolicitud = SolicitudEstado.AprobadoFinal,
                FechaSolicitud = now,
                FechaAprobacion1 = now,
                FechaAprobacion2 = now,
                PersonaAprobador1Id = accessContext.CurrentPersonaId,
                PersonaAprobador2Id = accessContext.CurrentPersonaId,
                CreadoEn = now,
                ActualizadoEn = now
            });

            vacaciones.Add(new Vacacion
            {
                VacacionId = Guid.NewGuid().ToString("N"),
                SolicitudId = solicitudId,
                FechaInicio = start,
                FechaFin = end
            });

            ranges.Add((start, end));
        }

        if (solicitudes.Count == 0)
        {
            ModelState.AddModelError("BulkCreate.Items", "Ninguna persona pudo registrarse: todas tienen superposicion de vacaciones.");
            return View(nameof(Index), await BuildIndexViewModelAsync(accessContext, bulkCreateModel: bulkModel));
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Solicitudes.AddRange(solicitudes);
            _db.Vacaciones.AddRange(vacaciones);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            if (omittedCount > 0)
            {
                TempData["Success"] = $"Vacaciones registradas: {solicitudes.Count}. Omitidas {omittedCount} por superposicion.";
            }
            else
            {
                TempData["Success"] = $"Vacaciones registradas: {solicitudes.Count}.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error al crear vacaciones por lotes para {Cantidad} filas.", rows.Count);
            ModelState.AddModelError("BulkCreate.Items", "No se pudieron registrar las vacaciones. Intenta nuevamente.");
            return View(nameof(Index), await BuildIndexViewModelAsync(accessContext, bulkCreateModel: bulkModel));
        }
    }

    [HttpPost]
    [Authorize(Policy = "LiderAbove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(VacacionEditViewModel editModel, string? search, int? year, int? month, int page = 1, int pageSize = DefaultPageSize)
    {
        var accessContext = await BuildAccessContextAsync();
        if (accessContext is null)
        {
            return Unauthorized();
        }

        if (!accessContext.CanCreate)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(editModel.VacacionId))
        {
            TempData["Error"] = "La vacacion seleccionada no es valida.";
            return RedirectToIndexWithFilters(search, year, month, page, pageSize);
        }

        if (!editModel.FechaInicio.HasValue || !editModel.FechaFin.HasValue)
        {
            TempData["Error"] = "Las fechas de la vacacion son requeridas.";
            return RedirectToIndexWithFilters(search, year, month, page, pageSize);
        }

        if (editModel.FechaFin.Value < editModel.FechaInicio.Value)
        {
            TempData["Error"] = "La fecha fin no puede ser menor a la fecha inicio.";
            return RedirectToIndexWithFilters(search, year, month, page, pageSize);
        }

        var target = await (
            from v in _db.Vacaciones.AsNoTracking()
            join s in _db.Solicitudes.AsNoTracking() on v.SolicitudId equals s.SolicitudId
            join p in _db.Personas.AsNoTracking() on s.PersonaSolicitanteId equals p.PersonaId
            where v.VacacionId == editModel.VacacionId
            select new
            {
                v.SolicitudId,
                s.EstadoSolicitud,
                p.PersonaId,
                p.EquipoId
            })
            .SingleOrDefaultAsync();

        if (target == null)
        {
            TempData["Error"] = "La vacacion seleccionada no existe.";
            return RedirectToIndexWithFilters(search, year, month, page, pageSize);
        }

        if (!CanManagePersona(accessContext, target.PersonaId, target.EquipoId))
        {
            return Forbid();
        }

        if (target.EstadoSolicitud is SolicitudEstado.Rechazado or SolicitudEstado.Cancelado)
        {
            TempData["Error"] = "La vacacion ya no puede modificarse.";
            return RedirectToIndexWithFilters(search, year, month, page, pageSize);
        }

        var fechaInicio = editModel.FechaInicio.Value;
        var fechaFin = editModel.FechaFin.Value;

        var hasOverlap = await (
            from v in _db.Vacaciones.AsNoTracking()
            join s in _db.Solicitudes.AsNoTracking() on v.SolicitudId equals s.SolicitudId
            where v.VacacionId != editModel.VacacionId
                  && s.PersonaSolicitanteId == target.PersonaId
                  && s.EstadoSolicitud != SolicitudEstado.Rechazado
                  && s.EstadoSolicitud != SolicitudEstado.Cancelado
                  && v.FechaInicio <= fechaFin
                  && fechaInicio <= v.FechaFin
            select v.VacacionId)
            .AnyAsync();

        if (hasOverlap)
        {
            TempData["Error"] = "Ya existe un periodo de vacaciones superpuesto para la persona seleccionada.";
            return RedirectToIndexWithFilters(search, year, month, page, pageSize);
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var vacacionesActualizadas = await _db.Vacaciones
                .Where(v => v.VacacionId == editModel.VacacionId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.FechaInicio, fechaInicio)
                    .SetProperty(v => v.FechaFin, fechaFin));

            if (vacacionesActualizadas == 0)
            {
                await tx.RollbackAsync();
                TempData["Error"] = "La vacacion seleccionada no existe.";
                return RedirectToIndexWithFilters(search, year, month, page, pageSize);
            }

            await _db.Solicitudes
                .Where(s => s.SolicitudId == target.SolicitudId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.ActualizadoEn, DateTime.UtcNow));

            await tx.CommitAsync();
            TempData["Success"] = "Vacacion actualizada correctamente.";
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error al actualizar vacacion {VacacionId}.", editModel.VacacionId);
            TempData["Error"] = "No se pudo actualizar la vacacion.";
        }

        return RedirectToIndexWithFilters(search, year, month, page, pageSize);
    }

    [HttpPost]
    [Authorize(Policy = "LiderAbove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string vacacionId, string? search, int? year, int? month, int page = 1, int pageSize = DefaultPageSize)
    {
        var accessContext = await BuildAccessContextAsync();
        if (accessContext is null)
        {
            return Unauthorized();
        }

        if (!accessContext.CanCreate)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(vacacionId))
        {
            TempData["Error"] = "La vacacion seleccionada no es valida.";
            return RedirectToIndexWithFilters(search, year, month, page, pageSize);
        }

        var target = await (
            from v in _db.Vacaciones
            join s in _db.Solicitudes on v.SolicitudId equals s.SolicitudId
            join p in _db.Personas.AsNoTracking() on s.PersonaSolicitanteId equals p.PersonaId
            where v.VacacionId == vacacionId
            select new
            {
                Vacacion = v,
                Solicitud = s,
                p.PersonaId,
                p.EquipoId
            })
            .SingleOrDefaultAsync();

        if (target == null)
        {
            TempData["Error"] = "La vacacion seleccionada no existe.";
            return RedirectToIndexWithFilters(search, year, month, page, pageSize);
        }

        if (!CanManagePersona(accessContext, target.PersonaId, target.EquipoId))
        {
            return Forbid();
        }

        if (target.Solicitud.EstadoSolicitud is SolicitudEstado.Rechazado or SolicitudEstado.Cancelado)
        {
            TempData["Error"] = "La vacacion ya no puede eliminarse.";
            return RedirectToIndexWithFilters(search, year, month, page, pageSize);
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Vacaciones.Remove(target.Vacacion);
            _db.Solicitudes.Remove(target.Solicitud);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            TempData["Success"] = "Vacacion eliminada correctamente.";
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error al eliminar vacacion {VacacionId}.", vacacionId);
            TempData["Error"] = "No se pudo eliminar la vacacion.";
        }

        return RedirectToIndexWithFilters(search, year, month, page, pageSize);
    }

    private async Task<VacacionesIndexViewModel> BuildIndexViewModelAsync(
        AccessContext accessContext,
        string? search = null,
        int? year = null,
        int? month = null,
        int page = 1,
        int pageSize = DefaultPageSize,
        VacacionCargaViewModel? createModel = null,
        VacacionCargaMasivaViewModel? bulkCreateModel = null)
    {
        var principalGrupoRows = await (
            from pg in _db.PersonaGrupos.AsNoTracking()
            join g in _db.Grupos.AsNoTracking() on pg.GrupoId equals g.GrupoId
            where pg.EsPrincipal
            select new { pg.PersonaId, g.NombreGrupo })
            .ToListAsync();

        var principalGrupoByPersona = principalGrupoRows
            .GroupBy(x => x.PersonaId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ",
                    g.Select(x => x.NombreGrupo)
                        .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
                        .Distinct()
                        .OrderBy(nombre => nombre)));

        var query =
            from v in _db.Vacaciones.AsNoTracking()
            join s in _db.Solicitudes.AsNoTracking() on v.SolicitudId equals s.SolicitudId
            join p in _db.Personas.AsNoTracking() on s.PersonaSolicitanteId equals p.PersonaId
            join e in _db.Equipos.AsNoTracking() on p.EquipoId equals e.EquipoId into equipoJoin
            from e in equipoJoin.DefaultIfEmpty()
            select new
            {
                VacacionId = v.VacacionId,
                SolicitudId = s.SolicitudId,
                PersonaId = p.PersonaId,
                PersonaNombre = p.Nombre + " " + p.Apellido + (string.IsNullOrWhiteSpace(p.SegundoApellido) ? "" : " " + p.SegundoApellido),
                Ultimatix = p.Ultimatix,
                EquipoId = p.EquipoId,
                EquipoNombre = e != null ? e.NombreEquipo : "-",
                FechaInicio = v.FechaInicio,
                FechaFin = v.FechaFin,
                EstadoSolicitud = s.EstadoSolicitud,
                FechaSolicitud = s.FechaSolicitud
            };

        if (!accessContext.IsAdmin)
        {
            if (string.IsNullOrWhiteSpace(accessContext.CurrentPersonaId))
            {
                query = query.Where(_ => false);
            }
            else if (accessContext.IsLider)
            {
                query = query.Where(x => x.EquipoId == accessContext.CurrentEquipoId);
            }
            else
            {
                query = query.Where(x => x.PersonaId == accessContext.CurrentPersonaId);
            }
        }

        var searchTerm = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.PersonaNombre, pattern) ||
                EF.Functions.ILike(x.Ultimatix, pattern));
        }

        var normalizedYear = year is >= 1900 and <= 2200 ? year : null;
        var normalizedMonth = month is >= 1 and <= 12 ? month : null;
        if (normalizedMonth.HasValue && !normalizedYear.HasValue)
        {
            normalizedYear = DateTime.Today.Year;
        }

        if (normalizedYear.HasValue)
        {
            var fromDate = normalizedMonth.HasValue
                ? new DateOnly(normalizedYear.Value, normalizedMonth.Value, 1)
                : new DateOnly(normalizedYear.Value, 1, 1);
            var toDate = normalizedMonth.HasValue
                ? fromDate.AddMonths(1).AddDays(-1)
                : new DateOnly(normalizedYear.Value, 12, 31);

            query = query.Where(x => x.FechaInicio <= toDate && fromDate <= x.FechaFin);
        }

        var rows = await query
            .OrderByDescending(x => x.FechaInicio)
            .ThenByDescending(x => x.FechaSolicitud)
            .ToListAsync();

        static string MapEstado(SolicitudEstado estado)
        {
            return estado switch
            {
                SolicitudEstado.AprobadoFinal => "Aprobado",
                SolicitudEstado.Rechazado => "Rechazado",
                SolicitudEstado.Cancelado => "Cancelado",
                SolicitudEstado.AprobadoLider => "En aprobacion",
                _ => "Pendiente"
            };
        }

        var items = rows.Select(row =>
        {
            var dias = row.FechaFin.DayNumber - row.FechaInicio.DayNumber + 1;
            return new VacacionListItemViewModel
            {
                VacacionId = row.VacacionId,
                SolicitudId = row.SolicitudId,
                PersonaId = row.PersonaId,
                PersonaNombre = row.PersonaNombre.Trim(),
                EquipoNombre = row.EquipoNombre,
                GrupoNombre = principalGrupoByPersona.TryGetValue(row.PersonaId, out var grupoNombre)
                    ? grupoNombre
                    : "-",
                FechaInicio = row.FechaInicio,
                FechaFin = row.FechaFin,
                Dias = Math.Max(1, dias),
                Estado = MapEstado(row.EstadoSolicitud),
                FechaSolicitud = row.FechaSolicitud,
                CanManage = accessContext.CanCreate
                    && row.EstadoSolicitud != SolicitudEstado.Rechazado
                    && row.EstadoSolicitud != SolicitudEstado.Cancelado
            };
        }).ToList();

        var currentPageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;
        var orderedPersonaIds = items
            .GroupBy(item => item.PersonaId)
            .Select(group => group.Key)
            .ToList();

        var totalCount = orderedPersonaIds.Count;
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)currentPageSize);
        var currentPage = totalPages == 0
            ? 1
            : Math.Min(Math.Max(page, 1), totalPages);

        if (totalCount > 0)
        {
            var pagedPersonaIds = orderedPersonaIds
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .ToHashSet(StringComparer.Ordinal);

            items = items
                .Where(item => pagedPersonaIds.Contains(item.PersonaId))
                .ToList();
        }

        var personasDisponibles = await LoadPersonasDisponiblesAsync(accessContext);
        var create = createModel ?? new VacacionCargaViewModel();
        var bulkCreate = bulkCreateModel ?? new VacacionCargaMasivaViewModel();
        var defaultDate = DateOnly.FromDateTime(DateTime.Today);
        var currentYear = DateTime.Today.Year;
        var yearOptions = Enumerable
            .Range(currentYear - 5, 11)
            .OrderByDescending(y => y)
            .ToList();
        if (normalizedYear.HasValue && !yearOptions.Contains(normalizedYear.Value))
        {
            yearOptions.Add(normalizedYear.Value);
            yearOptions = yearOptions.OrderByDescending(y => y).ToList();
        }

        if (!create.FechaInicio.HasValue)
        {
            create.FechaInicio = defaultDate;
        }

        if (!create.FechaFin.HasValue)
        {
            create.FechaFin = create.FechaInicio;
        }

        if (string.IsNullOrWhiteSpace(create.PersonaId) && personasDisponibles.Count > 0)
        {
            create.PersonaId = personasDisponibles[0].PersonaId;
        }

        bulkCreate.Items ??= new List<VacacionCargaItemViewModel>();
        if (bulkCreate.Items.Count == 0)
        {
            bulkCreate.Items.Add(new VacacionCargaItemViewModel
            {
                PersonaId = personasDisponibles.FirstOrDefault()?.PersonaId ?? string.Empty,
                FechaInicio = defaultDate,
                FechaFin = defaultDate
            });
        }
        else
        {
            var fallbackPersonaId = personasDisponibles.FirstOrDefault()?.PersonaId ?? string.Empty;
            foreach (var row in bulkCreate.Items)
            {
                if (string.IsNullOrWhiteSpace(row.PersonaId))
                {
                    row.PersonaId = fallbackPersonaId;
                }

                if (!row.FechaInicio.HasValue)
                {
                    row.FechaInicio = defaultDate;
                }

                if (!row.FechaFin.HasValue)
                {
                    row.FechaFin = row.FechaInicio;
                }
            }
        }

        return new VacacionesIndexViewModel
        {
            ShowEquipoColumn = accessContext.IsAdmin || accessContext.IsLider,
            ShowGrupoColumn = accessContext.IsAdmin || accessContext.IsLider,
            CanCreate = accessContext.CanCreate,
            Search = searchTerm,
            Year = normalizedYear,
            Month = normalizedMonth,
            Page = currentPage,
            PageSize = currentPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            YearOptions = yearOptions,
            Create = create,
            BulkCreate = bulkCreate,
            PersonasDisponibles = personasDisponibles,
            Items = items
        };
    }

    private async Task<List<VacacionPersonaOptionViewModel>> LoadPersonasDisponiblesAsync(AccessContext accessContext)
    {
        if (!accessContext.CanCreate)
        {
            return new List<VacacionPersonaOptionViewModel>();
        }

        var personasQuery =
            from p in _db.Personas.AsNoTracking()
            join e in _db.Equipos.AsNoTracking() on p.EquipoId equals e.EquipoId into equipoJoin
            from e in equipoJoin.DefaultIfEmpty()
            where !p.Borrado
            select new
            {
                p.PersonaId,
                Nombre = p.Nombre + " " + p.Apellido + (string.IsNullOrWhiteSpace(p.SegundoApellido) ? "" : " " + p.SegundoApellido),
                EquipoId = p.EquipoId,
                EquipoNombre = e != null ? e.NombreEquipo : "-"
            };

        if (accessContext.IsLider)
        {
            if (string.IsNullOrWhiteSpace(accessContext.CurrentEquipoId))
            {
                return new List<VacacionPersonaOptionViewModel>();
            }

            personasQuery = personasQuery.Where(p => p.EquipoId == accessContext.CurrentEquipoId);
        }

        var personas = await personasQuery
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        return personas.Select(p => new VacacionPersonaOptionViewModel
        {
            PersonaId = p.PersonaId,
            DisplayName = accessContext.IsAdmin
                ? $"{p.Nombre.Trim()} ({p.EquipoNombre})"
                : p.Nombre.Trim()
        }).ToList();
    }

    private async Task<string?> ResolveVacacionTipoSolicitudIdAsync()
    {
        return await _db.TipoSolicitudes
            .AsNoTracking()
            .Where(t =>
                t.TipoSolicitudId.ToUpper().StartsWith("VAC") ||
                t.NombreSolicitud.ToUpper().Contains("VAC"))
            .OrderBy(t => t.TipoSolicitudId)
            .Select(t => t.TipoSolicitudId)
            .FirstOrDefaultAsync();
    }

    private IActionResult RedirectToIndexWithFilters(string? search, int? year, int? month, int page, int pageSize)
    {
        return RedirectToAction(nameof(Index), new
        {
            search,
            year,
            month,
            page,
            pageSize
        });
    }

    private static bool CanManagePersona(AccessContext context, string targetPersonaId, string? targetEquipoId)
    {
        if (context.IsAdmin)
        {
            return true;
        }

        if (!context.IsLider)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(context.CurrentEquipoId))
        {
            return false;
        }

        if (context.CurrentPersonaId == targetPersonaId)
        {
            return true;
        }

        return string.Equals(context.CurrentEquipoId, targetEquipoId, StringComparison.Ordinal);
    }

    private async Task<AccessContext?> BuildAccessContextAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
        var isLider = User.IsInRole("Lider") && !isAdmin;

        var currentPersona = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.Borrado)
            .Select(p => new { p.PersonaId, p.EquipoId })
            .FirstOrDefaultAsync();

        return new AccessContext
        {
            UserId = userId,
            CurrentPersonaId = currentPersona?.PersonaId,
            CurrentEquipoId = currentPersona?.EquipoId,
            IsAdmin = isAdmin,
            IsLider = isLider,
            CanCreate = isAdmin || isLider
        };
    }

    private sealed class AccessContext
    {
        public string UserId { get; set; } = string.Empty;
        public string? CurrentPersonaId { get; set; }
        public string? CurrentEquipoId { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsLider { get; set; }
        public bool CanCreate { get; set; }
    }
}
