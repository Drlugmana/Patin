using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Services;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Controllers;

[Authorize(Policy = "LiderAbove")]
public class PlanificacionController : Controller
{
    private const string DiaConfiguracionNocturnosMes = "__CFG_MAX_NOCTURNOS_MES__";

    private readonly IPlanificacionService _planificacionService;
    private readonly IRegistroTurnoService _registroTurnoService;
    private readonly IGrupoService _grupoService;
    private readonly IEquipoService _equipoService;
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly GenerationPreviewProgressTracker _generationProgressTracker;
    private readonly ILogger<PlanificacionController> _logger;

    public PlanificacionController(
        IPlanificacionService planificacionService,
        IRegistroTurnoService registroTurnoService,
        IGrupoService grupoService,
        IEquipoService equipoService,
        ApplicationDbContext db,
        IMemoryCache cache,
        GenerationPreviewProgressTracker generationProgressTracker,
        ILogger<PlanificacionController> logger)
    {
        _planificacionService = planificacionService;
        _registroTurnoService = registroTurnoService;
        _grupoService = grupoService;
        _equipoService = equipoService;
        _db = db;
        _cache = cache;
        _generationProgressTracker = generationProgressTracker;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string equipoId)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            TempData["Error"] = "No se especificó un equipo.";
            return RedirectToAction("Index", "Equipo");
        }

        var equipo = (await _equipoService.GetAllAsync()).FirstOrDefault(e => e.EquipoId == equipoId);
        if (equipo == null)
        {
            TempData["Error"] = "El equipo especificado no existe.";
            return RedirectToAction("Index", "Equipo");
        }

        var grupos = await _grupoService.GetAllAsync();
        var gruposDelEquipo = grupos.Where(g => g.EquipoId == equipoId && g.Activo).ToList();

        var tipoTurnos = await _planificacionService.GetTipoTurnosAsync();
        var auxiliaresEquipoTurnos = await _db.PlanificacionesAuxiliaresEquipo
            .AsNoTracking()
            .Where(a => a.EquipoId == equipoId
                && !(a.DesdeDia == DiaConfiguracionNocturnosMes && a.HastaDia == DiaConfiguracionNocturnosMes))
            .Select(a => a.TipoTurnoId)
            .Distinct()
            .ToListAsync();
        var mappedTurnos = await _db.EquipoTipoTurnos
            .AsNoTracking()
            .Where(et => et.EquipoId == equipoId)
            .Select(et => et.TipoTurnoId)
            .ToListAsync();
        var visibleTurnos = mappedTurnos
            .Concat(auxiliaresEquipoTurnos)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (visibleTurnos.Count > 0)
        {
            tipoTurnos = tipoTurnos
                .Where(t => visibleTurnos.Contains(t.TipoTurnoId, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        var vm = new PlanificacionViewModel
        {
            EquipoId = equipoId,
            EquipoNombre = equipo.NombreEquipo,
            TipoGeneracion = "Rotacion",
            Grupos = gruposDelEquipo.Select(g => new SelectListItem
            {
                Value = g.GrupoId,
                Text = g.NombreGrupo
            }),
            TipoTurnos = tipoTurnos,
            Planificaciones = new List<Models.Planificacion>()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveConfiguracionRotacion([FromBody] SaveConfiguracionRotacionDto dto)
    {
        return Json(new
        {
            success = false,
            error = "La configuracion V2 ya no usa una tabla separada. Se deriva de planificacion y tablas relacionadas."
        });
    }
#if false
        if (string.IsNullOrWhiteSpace(dto.EquipoId))
        {
            return Json(new { success = false, error = "Equipo no especificado" });
        }

        await EnsureConfiguracionRotacionCompatibilityAsync();

        if (!await CanManageEquipoAsync(dto.EquipoId))
        {
            return Json(new { success = false, error = "No tiene permiso para administrar este equipo." });
        }

        var equipoExiste = await _db.Equipos.AsNoTracking().AnyAsync(e => e.EquipoId == dto.EquipoId);
        if (!equipoExiste)
        {
            return Json(new { success = false, error = "Equipo no encontrado" });
        }

        var configuracion = await _db.ConfiguracionesRotacionEquipo
            .FirstOrDefaultAsync(c => c.EquipoId == dto.EquipoId);

        if (configuracion == null)
        {
            configuracion = new ConfiguracionRotacionEquipo
            {
                ConfiguracionRotacionEquipoId = Guid.NewGuid().ToString("N")[..12],
                EquipoId = dto.EquipoId
            };
            _db.ConfiguracionesRotacionEquipo.Add(configuracion);
        }

        configuracion.MinutosObjetivoSemanales = Math.Max(1, dto.HorasObjetivoSemanales) * 60;
        configuracion.MinutosMinimosDescansoEntreTurnos = Math.Max(1, dto.HorasMinimasDescansoEntreTurnos) * 60;
        configuracion.MinimoDiasDescansoConsecutivosPorSemana = Math.Max(1, dto.MinimoDiasDescansoConsecutivosPorSemana);
        configuracion.MaximoTurnosPorDia = Math.Max(1, dto.MaximoTurnosPorDia);
        configuracion.AplicarVacaciones = dto.AplicarVacaciones;
        configuracion.PermiteTurnosAuxiliares = dto.PermiteTurnosAuxiliares;
        configuracion.EvitarFinesSemanaConsecutivos = dto.EvitarFinesSemanaConsecutivos;
        configuracion.MaximoFinesSemanaConsecutivos = Math.Max(1, dto.MaximoFinesSemanaConsecutivos);
        configuracion.MaximoSlotsFinSemanaPorMes = dto.MaximoSlotsFinSemanaPorMes > 0 ? dto.MaximoSlotsFinSemanaPorMes : null;
        configuracion.MaximoTurnosNocturnosPorMes = dto.MaximoTurnosNocturnosPorMes > 0 ? dto.MaximoTurnosNocturnosPorMes : null;
        configuracion.BalancearHorasSemanales = dto.BalancearHorasSemanales;
        configuracion.BalancearTurnosNocturnos = dto.BalancearTurnosNocturnos;
        configuracion.BalancearCargaFeriados = dto.BalancearCargaFeriados;

        await UpdateEquipoTipoGeneracionAsync(dto.EquipoId, "Rotacion");
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "Configuración V2 guardada exitosamente" });
    }

#endif
    [HttpGet]
    public async Task<IActionResult> GetPlanificacion(string? grupoId, string? equipoId)
    {
        if (string.IsNullOrWhiteSpace(grupoId) && string.IsNullOrWhiteSpace(equipoId))
        {
            return Json(new { success = false, error = "Grupo o equipo no especificado" });
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(grupoId))
            {
                var grupo = await _db.Grupos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.GrupoId == grupoId);

                if (grupo == null)
                {
                    return Json(new { success = false, error = "Grupo no encontrado" });
                }

                equipoId = grupo.EquipoId;
            }

            var planificaciones = !string.IsNullOrWhiteSpace(grupoId)
                ? await _planificacionService.GetByGrupoIdAsync(grupoId)
                : Array.Empty<Models.Planificacion>();
            var apoyosGrupo = !string.IsNullOrWhiteSpace(grupoId)
                ? await _planificacionService.GetApoyosByGrupoIdAsync(grupoId)
                : Array.Empty<PlanificacionApoyoGrupo>();
            var turnosOpcionalesVacacion = !string.IsNullOrWhiteSpace(grupoId)
                ? await _planificacionService.GetTurnosOpcionalesVacacionByGrupoIdAsync(grupoId)
                : Array.Empty<PlanificacionTurnoOpcionalVacacionGrupo>();
            var auxiliaresEquipo = await _planificacionService.GetAuxiliaresByEquipoIdAsync(equipoId!);
            var configEquipo = await _planificacionService.GetEquipoPlanificacionConfigAsync(equipoId!);
            var personasEquipoActivas = await _db.Personas
                .AsNoTracking()
                .Where(p => p.EquipoId == equipoId && !p.Borrado)
                .CountAsync();

            var personasGrupo = new List<(string PersonaId, bool EsPrincipal)>();
            if (!string.IsNullOrWhiteSpace(grupoId))
            {
                var personasGrupoRaw = await (
                    from pg in _db.PersonaGrupos.AsNoTracking()
                    join p in _db.Personas.AsNoTracking() on pg.PersonaId equals p.PersonaId
                    where pg.GrupoId == grupoId && !p.Borrado
                    select new { pg.PersonaId, pg.EsPrincipal })
                    .Distinct()
                    .ToListAsync();
                personasGrupo = personasGrupoRaw
                    .Select(p => (p.PersonaId, p.EsPrincipal))
                    .ToList();
            }

            var personasPrincipalesGrupo = personasGrupo
                .Where(p => p.EsPrincipal)
                .Select(p => p.PersonaId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var personasSecundariasGrupo = personasGrupo
                .Where(p => !p.EsPrincipal)
                .Select(p => p.PersonaId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var personasActivasGrupo = personasGrupo
                .Select(p => p.PersonaId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var data = planificaciones
                .Where(p => !p.IsAuxiliar)
                .Select(p => new
                {
                    dia = p.Dia,
                    tipoTurnoId = p.TipoTurnoId,
                    numeroPersonas = p.NumeroPersonas
                });
            var configSecundarios = planificaciones
                .Where(p => !p.IsAuxiliar)
                .OrderBy(p => GetDiaOrden(p.Dia))
                .ThenBy(p => p.TipoTurnoId)
                .Select(p => new
                {
                    usaSoloSecundarios = p.UsaSoloSecundarios,
                    grupoFuenteSecundariosId = p.GrupoFuenteSecundariosId,
                    usarPersonaUnicaPorSemana = p.UsarPersonaUnicaPorSemana
                })
                .FirstOrDefault() ?? new
                {
                    usaSoloSecundarios = false,
                    grupoFuenteSecundariosId = (string?)null,
                    usarPersonaUnicaPorSemana = false
                };

            var auxiliares = auxiliaresEquipo
                .Select(a => new
                {
                    tipoTurnoId = a.TipoTurnoId,
                    desdeDia = a.DesdeDia,
                    hastaDia = a.HastaDia,
                    maxPorDia = a.MaxPorDia,
                    grupoIds = a.GruposPermitidos
                        .Select(g => g.GrupoId)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(id => id)
                        .ToList()
                })
                .ToList();

            var apoyos = apoyosGrupo
                .Select(a => new
                {
                    dia = a.Dia,
                    tipoTurnoId = a.TipoTurnoId,
                    cantidadApoyo = a.CantidadApoyo
                })
                .OrderBy(a => GetDiaOrden(a.dia))
                .ThenBy(a => a.tipoTurnoId)
                .ToList();

            var opcionalesVacacion = turnosOpcionalesVacacion
                .Select(a => new
                {
                    dia = a.Dia,
                    tipoTurnoId = a.TipoTurnoId
                })
                .OrderBy(a => GetDiaOrden(a.dia))
                .ThenBy(a => a.tipoTurnoId)
                .ToList();

            return Json(new
            {
                success = true,
                data,
                auxiliares,
                apoyos,
                opcionalesVacacion,
                metricas = new
                {
                    personasActivas = !string.IsNullOrWhiteSpace(grupoId)
                        ? personasActivasGrupo
                        : personasEquipoActivas,
                    personasEquipoActivas,
                    personasPrincipalesGrupo,
                    personasSecundariasGrupo
                },
                maximoTurnosNocturnosPorMes = configEquipo.MaximoTurnosNocturnosPorMes,
                maximoTurnosNocturnosPorSemana = configEquipo.MaximoTurnosNocturnosPorSemana,
                maximoSlotsFinSemanaPorMes = configEquipo.MaximoSlotsFinSemanaPorMes,
                configSecundarios
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener planificación del grupo {GrupoId}", grupoId);
            return Json(new { success = false, error = "Error al obtener la planificación" });
        }
    }

    [HttpGet]
    public Task<IActionResult> GetBlueprint(string equipoId, string? grupoId)
    {
        return Task.FromResult<IActionResult>(Json(new { success = false, error = "Blueprint legacy deshabilitado. Usa cobertura y configuracion V2." }));
    }
#if false
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return Json(new { success = false, error = "Equipo no especificado" });
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(grupoId))
            {
                // Si grupoId está especificado, filtrar directamente
                var entries = await _db.PlanificacionBlueprints
                    .AsNoTracking()
                    .Where(p => p.GrupoId == grupoId)
                    .Select(p => new
                    {
                        dia = p.Dia,
                        tipoTurnoId = p.TipoTurnoId,
                        etiquetas = p.Etiquetas,
                        grupoId = p.GrupoId,
                        minPersonasTurno = p.MinPersonasTurno
                    })
                    .ToListAsync();

                var usaGruposSecundarios = await _db.PlanificacionBlueprints
                    .AsNoTracking()
                    .Where(p => p.GrupoId == grupoId)
                    .Select(p => p.UsaGruposSecundarios)
                    .FirstOrDefaultAsync();

                return Json(new { success = true, data = entries, usaGruposSecundarios });
            }
            else
            {
                // Si no hay grupoId, obtener blueprints de todos los grupos del equipo
                var entries = await _db.PlanificacionBlueprints
                    .AsNoTracking()
                    .Include(p => p.Grupo)
                    .Where(p => p.Grupo != null && p.Grupo.EquipoId == equipoId)
                    .Select(p => new
                    {
                        dia = p.Dia,
                        tipoTurnoId = p.TipoTurnoId,
                        etiquetas = p.Etiquetas,
                        grupoId = p.GrupoId,
                        minPersonasTurno = p.MinPersonasTurno
                    })
                    .ToListAsync();

                return Json(new { success = true, data = entries, usaGruposSecundarios = false });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener blueprint del equipo {EquipoId}", equipoId);
            return Json(new { success = false, error = "Error al obtener el blueprint" });
        }
#endif
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEquipoPlanificacionConfig([FromBody] SaveEquipoPlanificacionConfigDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EquipoId))
        {
            return Json(new { success = false, error = "Equipo no especificado" });
        }

        if (!await CanManageEquipoAsync(dto.EquipoId))
        {
            return Json(new { success = false, error = "No tiene permiso para administrar este equipo." });
        }

        var result = await _planificacionService.SaveEquipoPlanificacionConfigAsync(
            dto.EquipoId,
            dto.MaximoSlotsFinSemanaPorMes,
            dto.MaximoTurnosNocturnosPorMes,
            dto.MaximoTurnosNocturnosPorSemana);

        return result.Succeeded
            ? Json(new { success = true, message = "Reglas del equipo guardadas." })
            : Json(new { success = false, error = result.Error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePlanificacion([FromBody] SavePlanificacionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EquipoId))
        {
            return Json(new { success = false, error = "Equipo no especificado" });
        }

        if ((dto.Planificaciones == null || !dto.Planificaciones.Any())
            && (dto.Auxiliares == null || !dto.Auxiliares.Any())
            && (dto.Apoyos == null || !dto.Apoyos.Any())
            && (dto.OpcionalesVacacion == null || !dto.OpcionalesVacacion.Any())
            && !dto.MaximoSlotsFinSemanaPorMes.HasValue
            && !dto.MaximoTurnosNocturnosPorMes.HasValue
            && !dto.MaximoTurnosNocturnosPorSemana.HasValue)
        {
            return Json(new { success = false, error = "No se proporcionaron datos de planificación" });
        }

        try
        {
            var requests = new List<PlanificacionSaveRequest>();
            var auxiliares = new List<PlanificacionAuxiliarSaveRequest>();
            var apoyos = new List<PlanificacionApoyoSaveRequest>();
            var opcionalesVacacion = new List<PlanificacionTurnoOpcionalVacacionSaveRequest>();

            if (dto.Planificaciones != null)
            {
                requests.AddRange(dto.Planificaciones.Select(p => new PlanificacionSaveRequest(
                    null,
                    dto.GrupoId,
                    p.Dia,
                    p.TipoTurnoId,
                    p.NumeroPersonas,
                    false
                )));
            }

            if (dto.Auxiliares != null)
            {
                foreach (var auxiliar in dto.Auxiliares)
                {
                    if (string.IsNullOrWhiteSpace(auxiliar.TipoTurnoId))
                    {
                        continue;
                    }

                    if (auxiliar.MaxPorDia <= 0
                        || string.IsNullOrWhiteSpace(auxiliar.DesdeDia)
                        || string.IsNullOrWhiteSpace(auxiliar.HastaDia))
                    {
                        continue;
                    }

                    var diasAuxiliares = ExpandirRangoDias(auxiliar.DesdeDia, auxiliar.HastaDia);
                    if (diasAuxiliares.Count == 0)
                    {
                        return Json(new { success = false, error = $"Rango auxiliar invÃ¡lido para turno {auxiliar.TipoTurnoId}" });
                    }

                    auxiliares.Add(new PlanificacionAuxiliarSaveRequest(
                        auxiliar.TipoTurnoId,
                        auxiliar.DesdeDia,
                        auxiliar.HastaDia,
                        auxiliar.MaxPorDia,
                        (auxiliar.GrupoIds ?? new List<string>())
                            .Where(id => !string.IsNullOrWhiteSpace(id))
                            .Select(id => id.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList()
                    ));
                }
            }

            if (dto.Apoyos != null)
            {
                apoyos.AddRange(dto.Apoyos
                    .Where(apoyo => !string.IsNullOrWhiteSpace(apoyo.Dia) && !string.IsNullOrWhiteSpace(apoyo.TipoTurnoId))
                    .Select(apoyo => new PlanificacionApoyoSaveRequest(
                        apoyo.Dia,
                        apoyo.TipoTurnoId,
                        apoyo.CantidadApoyo
                    )));
            }

            if (dto.OpcionalesVacacion != null)
            {
                opcionalesVacacion.AddRange(dto.OpcionalesVacacion
                    .Where(item => !string.IsNullOrWhiteSpace(item.Dia) && !string.IsNullOrWhiteSpace(item.TipoTurnoId))
                    .Select(item => new PlanificacionTurnoOpcionalVacacionSaveRequest(
                        item.Dia,
                        item.TipoTurnoId
                    )));
            }

            var result = await _planificacionService.SavePlanificacionAsync(
                requests,
                auxiliares,
                apoyos,
                opcionalesVacacion,
                dto.GrupoId,
                dto.EquipoId,
                dto.MaximoSlotsFinSemanaPorMes,
                dto.MaximoTurnosNocturnosPorMes,
                dto.MaximoTurnosNocturnosPorSemana,
                dto.UsaSoloSecundarios,
                dto.GrupoFuenteSecundariosId,
                dto.UsarPersonaUnicaPorSemana);

            if (result.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(dto.GrupoId))
                {
                    await UpdateEquipoTipoGeneracionByGrupoAsync(dto.GrupoId, "Rotacion");
                }
                await SyncEquipoTipoTurnosByEquipoAsync(dto.EquipoId);
                return Json(new { success = true, message = "Planificación guardada exitosamente" });
            }
            else
            {
                return Json(new { success = false, error = result.Error });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar planificación del grupo {GrupoId}", dto.GrupoId);
            return Json(new { success = false, error = "Error al guardar la planificación" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveBlueprint([FromBody] SaveBlueprintDto dto)
    {
        return Task.FromResult<IActionResult>(Json(new { success = false, error = "Blueprint legacy deshabilitado. Usa cobertura y configuracion V2." }));
    }
#if false
        if (string.IsNullOrWhiteSpace(dto.EquipoId))
        {
            return Json(new { success = false, error = "Equipo no especificado" });
        }

        if (dto.Entries == null || !dto.Entries.Any())
        {
            return Json(new { success = false, error = "No se proporcionaron datos de blueprint" });
        }

        try
        {
            var tipoTurnosValidos = await _db.EquipoTipoTurnos
                .AsNoTracking()
                .Where(et => et.EquipoId == dto.EquipoId)
                .Join(
                    _db.TipoTurnos.AsNoTracking().Where(t => t.Activo),
                    et => et.TipoTurnoId,
                    t => t.TipoTurnoId,
                    (et, t) => t.TipoTurnoId)
                .ToListAsync();

            if (tipoTurnosValidos.Count == 0)
            {
                return Json(new { success = false, error = "El equipo no tiene tipos de turno activos" });
            }

            var existentes = await _db.PlanificacionBlueprints
                .Where(p => p.GrupoId == dto.GrupoId)
                .ToListAsync();

            if (existentes.Any())
            {
                _db.PlanificacionBlueprints.RemoveRange(existentes);
            }

            foreach (var entry in dto.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Etiquetas))
                {
                    continue;
                }

                if (!tipoTurnosValidos.Contains(entry.TipoTurnoId))
                {
                    return Json(new
                    {
                        success = false,
                        error = $"El turno {entry.TipoTurnoId} no pertenece al equipo seleccionado"
                    });
                }

                _db.PlanificacionBlueprints.Add(new Models.PlanificacionBlueprint
                {
                    PlanificacionBlueprintId = Guid.NewGuid().ToString("N")[..12],
                    Dia = entry.Dia,
                    TipoTurnoId = entry.TipoTurnoId,
                    Etiquetas = entry.Etiquetas.Trim(),
                    GrupoId = string.IsNullOrWhiteSpace(dto.GrupoId) ? null : dto.GrupoId,
                    MinPersonasTurno = entry.MinPersonasTurno,
                    UsaGruposSecundarios = dto.UsaGruposSecundarios
                });
            }

            await _db.SaveChangesAsync();
            await UpdateEquipoTipoGeneracionAsync(dto.EquipoId, "Blueprint");
            return Json(new { success = true, message = "Blueprint guardado exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar blueprint del equipo {EquipoId}", dto.EquipoId);
            return Json(new { success = false, error = "Error al guardar el blueprint" });
        }
#endif
    [HttpGet]
    public async Task<IActionResult> GetFeriadoCobertura(string equipoId, string grupoId)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return Json(new { success = false, error = "Equipo no especificado" });
        }
        if (string.IsNullOrWhiteSpace(grupoId))
        {
            return Json(new { success = false, error = "Grupo no especificado" });
        }

        try
        {
            var grupoValido = await _db.Grupos
                .AsNoTracking()
                .AnyAsync(g => g.GrupoId == grupoId && g.EquipoId == equipoId);
            if (!grupoValido)
            {
                return Json(new { success = false, error = "Grupo invalido para el equipo seleccionado" });
            }

            var configs = await _db.FeriadoCoberturaConfigs
                .AsNoTracking()
                .Where(c => c.EquipoId == equipoId && c.GrupoId == grupoId)
                .ToListAsync();

            var turnos = configs
                .Select(c => new
                {
                    tipoTurnoId = c.TipoTurnoId,
                    cantidadVisible = Math.Max(0, c.CantidadVisible)
                })
                .OrderBy(c => c.tipoTurnoId)
                .ToList();

            return Json(new
            {
                success = true,
                data = new
                {
                    turnos
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar configuracion de cobertura feriado para equipo {EquipoId}", equipoId);
            return Json(new { success = false, error = "Error al cargar configuracion de feriados" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFeriadoCobertura([FromBody] SaveFeriadoCoberturaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EquipoId))
        {
            return Json(new { success = false, error = "Equipo no especificado" });
        }
        if (string.IsNullOrWhiteSpace(dto.GrupoId))
        {
            return Json(new { success = false, error = "Grupo no especificado" });
        }

        var equipoExiste = await _db.Equipos.AsNoTracking().AnyAsync(e => e.EquipoId == dto.EquipoId);
        if (!equipoExiste)
        {
            return Json(new { success = false, error = "Equipo no existe" });
        }

        var grupoValido = await _db.Grupos
            .AsNoTracking()
            .AnyAsync(g => g.GrupoId == dto.GrupoId && g.EquipoId == dto.EquipoId);
        if (!grupoValido)
        {
            return Json(new { success = false, error = "Grupo invalido para el equipo seleccionado" });
        }

        try
        {
            await EnsureFeriadoCoberturaIndexCompatibilityAsync();

            var existentes = await _db.FeriadoCoberturaConfigs
                .Where(c => c.EquipoId == dto.EquipoId && c.GrupoId == dto.GrupoId)
                .ToListAsync();
            if (existentes.Count > 0)
            {
                _db.FeriadoCoberturaConfigs.RemoveRange(existentes);
            }

            var payload = (dto.Turnos ?? new List<FeriadoCoberturaItemDto>())
                .Where(t => !string.IsNullOrWhiteSpace(t.TipoTurnoId))
                .GroupBy(t => t.TipoTurnoId!, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    TipoTurnoId = g.Key,
                    CantidadVisible = Math.Max(0, g.Last().CantidadVisible)
                })
                .ToList();

            foreach (var item in payload)
            {
                _db.FeriadoCoberturaConfigs.Add(new FeriadoCoberturaConfig
                {
                    FeriadoCoberturaConfigId = Guid.NewGuid().ToString("N")[..12],
                    EquipoId = dto.EquipoId,
                    GrupoId = dto.GrupoId,
                    TipoTurnoId = item.TipoTurnoId,
                    CantidadVisible = item.CantidadVisible
                });
            }

            await _db.SaveChangesAsync();
            await UpdateEquipoTipoGeneracionAsync(dto.EquipoId, "Rotacion");
            return Json(new { success = true, message = "Cobertura de feriados por turno guardada." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar cobertura de feriados para equipo {EquipoId}", dto.EquipoId);
            return Json(new { success = false, error = "Error al guardar configuracion de feriados" });
        }
    }

    private async Task EnsureFeriadoCoberturaIndexCompatibilityAsync()
    {
        const string dropLegacyUnique = """
            DROP INDEX IF EXISTS "IX_feriado_cobertura_config_equipo_id_grupo_id";
            """;

        const string createGroupIndex = """
            CREATE INDEX IF NOT EXISTS "IX_feriado_cobertura_config_equipo_id_grupo_id"
            ON feriado_cobertura_config (equipo_id, grupo_id);
            """;

        const string createUniqueTrio = """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_feriado_cobertura_config_equipo_id_grupo_id_tipo_turno_id"
            ON feriado_cobertura_config (equipo_id, grupo_id, tipo_turno_id);
            """;

        await _db.Database.ExecuteSqlRawAsync(dropLegacyUnique);
        await _db.Database.ExecuteSqlRawAsync(createGroupIndex);
        await _db.Database.ExecuteSqlRawAsync(createUniqueTrio);
    }

    private async Task UpdateEquipoTipoGeneracionByGrupoAsync(string grupoId, string tipoGeneracion)
    {
        var equipoId = await _db.Grupos
            .Where(g => g.GrupoId == grupoId)
            .Select(g => g.EquipoId)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(equipoId))
        {
            await UpdateEquipoTipoGeneracionAsync(equipoId, tipoGeneracion);
        }
    }

    private async Task UpdateEquipoTipoGeneracionAsync(string equipoId, string tipoGeneracion)
    {
        var equipo = await _db.Equipos.FirstOrDefaultAsync(e => e.EquipoId == equipoId);
        if (equipo == null)
        {
            return;
        }

        const string tipoGeneracionNormalizado = "Rotacion";
        if (!string.Equals(equipo.TipoGeneracion, tipoGeneracionNormalizado, StringComparison.OrdinalIgnoreCase))
        {
            equipo.TipoGeneracion = tipoGeneracionNormalizado;
            await _db.SaveChangesAsync();
        }
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidarGeneracionTurnosPreview([FromBody] JsonElement payload)
    {
        var dto = ParseGenerarTurnosPayload(payload);

        if (dto.GruposIds == null || !dto.GruposIds.Any())
        {
            return Json(new { success = false, error = "No se proporcionaron grupos del equipo" });
        }

        var equipoId = dto.EquipoId;
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            equipoId = await _db.Grupos
                .AsNoTracking()
                .Where(g => dto.GruposIds.Contains(g.GrupoId))
                .Select(g => g.EquipoId)
                .FirstOrDefaultAsync();
        }

        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return Json(new { success = false, error = "No se pudo resolver el equipo para la validación" });
        }

        var scope = await ValidateGruposScopeAsync(dto.GruposIds, equipoId);
        if (!scope.Succeeded)
        {
            return Json(new { success = false, error = scope.Error });
        }

        var equipo = await _db.Equipos.AsNoTracking().FirstOrDefaultAsync(e => e.EquipoId == equipoId);
        if (equipo == null)
        {
            return Json(new { success = false, error = "Equipo no encontrado para la validación" });
        }

        var result = await _planificacionService.ValidarGeneracionRotacionPreviewAsync(dto.GruposIds);
        if (!result.Succeeded)
        {
            return Json(new { success = false, error = result.Error });
        }

        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerarTurnosPreview([FromBody] JsonElement payload)
    {
        var dto = ParseGenerarTurnosPayload(payload);
        var operationId = dto.OperationId;

        if (dto.GruposIds == null || !dto.GruposIds.Any())
        {
            return Json(new { success = false, error = "No se proporcionaron grupos del equipo" });
        }

        if (dto.NumeroSemanas < 1 || dto.NumeroSemanas > 52)
        {
            return Json(new { success = false, error = "El numero de semanas debe estar entre 1 y 52" });
        }

        var equipoId = dto.EquipoId;
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            equipoId = await _db.Grupos
                .AsNoTracking()
                .Where(g => dto.GruposIds.Contains(g.GrupoId))
                .Select(g => g.EquipoId)
                .FirstOrDefaultAsync();
        }
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return Json(new { success = false, error = "No se pudo resolver el equipo para la previsualizacion" });
        }

        var scope = await ValidateGruposScopeAsync(dto.GruposIds, equipoId);
        if (!scope.Succeeded)
        {
            return Json(new { success = false, error = scope.Error });
        }

        var equipo = await _db.Equipos.AsNoTracking().FirstOrDefaultAsync(e => e.EquipoId == equipoId);
        if (equipo == null)
        {
            return Json(new { success = false, error = "Equipo no encontrado para la previsualizacion" });
        }

        var lunesActual = ObtenerLunesSemana(DateTime.Today);
        DateTime fechaInicio = lunesActual;
        if (!string.IsNullOrWhiteSpace(dto.FechaInicio))
        {
            if (!DateTime.TryParseExact(dto.FechaInicio, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaSeleccionada))
            {
                return Json(new { success = false, error = "Fecha de inicio inválida" });
            }
            fechaInicio = ObtenerLunesSemana(fechaSeleccionada);
            if (fechaInicio < lunesActual)
            {
                return Json(new { success = false, error = "La semana de inicio no puede ser anterior a la semana actual" });
            }
        }

        if (!string.IsNullOrWhiteSpace(operationId))
        {
            _generationProgressTracker.Start(
                operationId,
                dto.NumeroSemanas,
                "Generando Turnos... 0%");
        }

        Action<GenerationProgressUpdate>? reportProgress = null;
        if (!string.IsNullOrWhiteSpace(operationId))
        {
            reportProgress = update =>
                _generationProgressTracker.ReportProgress(
                    operationId,
                    update.CompletedWeeks,
                    update.TotalWeeks,
                    update.Message ?? $"Generando Turnos... {Math.Clamp(update.CompletedWeeks * 100 / Math.Max(1, update.TotalWeeks), 0, 100)}%");
        }

        var result = await _planificacionService.GenerarTurnosRotacionGeneradorPreviewAsync(
            dto.GruposIds,
            dto.NumeroSemanas,
            fechaInicio,
            reportProgress,
            dto.AutorizarSobrecupoSemanalFeriado,
            dto.NivelUsoDescanso7Horas,
            dto.NivelEvitarFinesSemanaConsecutivos,
            dto.BalancearHorasSemanales);
        if (!result.Succeeded)
        {
            if (result.Error?.StartsWith(PlanificacionService.HolidayOvertimeApprovalRequiredPrefix, StringComparison.Ordinal) == true)
            {
                var approvalMessage = result.Error[PlanificacionService.HolidayOvertimeApprovalRequiredPrefix.Length..];
                if (!string.IsNullOrWhiteSpace(operationId))
                {
                    _generationProgressTracker.Complete(operationId, "Aprobacion requerida para feriado.");
                }

                return Json(new
                {
                    success = false,
                    requiresHolidayOvertimeApproval = true,
                    approvalMessage,
                    error = approvalMessage
                });
            }

            if (!string.IsNullOrWhiteSpace(operationId))
            {
                _generationProgressTracker.Fail(operationId, result.Error ?? "Error al generar turnos.");
            }
            return Json(new { success = false, error = result.Error });
        }

        var preview = result.Value ?? new List<TurnoGeneradoPreview>();
        if (!string.IsNullOrWhiteSpace(operationId))
        {
            _generationProgressTracker.ReportProgress(operationId, dto.NumeroSemanas, dto.NumeroSemanas, "Aplicando visibilidad de feriados... 100%");
        }
        var cacheKey = GetPreviewCacheKey(equipoId);
        _cache.Set(cacheKey, preview, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(2)
        });
        AddPreviewKeyForUser(equipoId);

        if (!string.IsNullOrWhiteSpace(operationId))
        {
            _generationProgressTracker.Complete(operationId, "Generación Completada. 100%");
        }

        return Json(new { success = true, count = preview.Count, equipoId });
    }

    [HttpGet]
    public IActionResult GeneracionTurnosProgress(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return Json(new { success = false, error = "OperationId is required." });
        }

        if (!_generationProgressTracker.TryGet(operationId, out var snapshot))
        {
            return NotFound(new { success = false, error = "Generation progress not found." });
        }

        return Json(new
        {
            success = true,
            operationId = snapshot.OperationId,
            status = snapshot.Status,
            totalWeeks = snapshot.TotalWeeks,
            completedWeeks = snapshot.CompletedWeeks,
            percentage = snapshot.Percentage,
            message = snapshot.Message,
            error = snapshot.Error
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarTurnosPreview([FromBody] JsonElement payload)
    {
        var dto = ParsePreviewConfirmPayload(payload);

        var equipoId = dto.EquipoId;
        if (string.IsNullOrWhiteSpace(equipoId) && dto.GruposIds.Any())
        {
            equipoId = await _db.Grupos
                .AsNoTracking()
                .Where(g => dto.GruposIds.Contains(g.GrupoId))
                .Select(g => g.EquipoId)
                .FirstOrDefaultAsync();
        }
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return Json(new { success = false, error = "No se pudo resolver el equipo para guardar" });
        }

        var cacheKey = GetPreviewCacheKey(equipoId);
        if (!_cache.TryGetValue(cacheKey, out List<TurnoGeneradoPreview>? previewList)
            || previewList == null
            || previewList.Count == 0)
        {
            return Json(new { success = false, error = "No hay previsualizaciones para guardar." });
        }

        DateOnly? weekStart = null;
        if (!string.IsNullOrWhiteSpace(dto.WeekStart))
        {
            if (DateOnly.TryParseExact(dto.WeekStart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart))
            {
                weekStart = parsedStart;
            }
            else
            {
                return Json(new { success = false, error = "Semana de inicio invalida." });
            }
        }

        var toSave = dto.SaveAllWeeks
            ? previewList
            : FiltrarPreviewPorSemana(previewList, weekStart);

        if (toSave.Count == 0)
        {
            return Json(new { success = false, error = "No hay previsualizaciones para guardar en la semana seleccionada." });
        }

        var saveItems = toSave
            .Select(p => new RegistroTurnoPreviewItem(
                p.PersonaId,
                p.TipoTurnoId,
                p.FechaTurno,
                string.IsNullOrWhiteSpace(p.GrupoId) ? null : p.GrupoId,
                p.EsFeriado,
                p.NoLaboradoPorFeriado))
            .ToList();

        var saveResult = await _registroTurnoService.SavePreviewAsync(
            saveItems,
            GetCurrentUserRole(),
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        if (!saveResult.Succeeded)
        {
            return Json(new { success = false, error = saveResult.Error ?? "No se pudieron guardar los turnos previsualizados." });
        }

        var savedKeySet = toSave
            .Select(p => $"{p.PersonaId}|{p.TipoTurnoId}|{p.GrupoId}|{p.FechaTurno:yyyy-MM-dd}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var remaining = previewList
            .Where(p => !savedKeySet.Contains($"{p.PersonaId}|{p.TipoTurnoId}|{p.GrupoId}|{p.FechaTurno:yyyy-MM-dd}"))
            .ToList();

        if (remaining.Count == 0)
        {
            _cache.Remove(cacheKey);
            RemovePreviewKeyForEquipo(equipoId);
        }
        else
        {
            _cache.Set(cacheKey, remaining, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(2)
            });
            AddPreviewKeyForUser(equipoId);
        }

        var message = dto.SaveAllWeeks
            ? "Se guardaron todas las semanas previsualizadas."
            : "Se guardo la semana actual.";

        return Json(new
        {
            success = true,
            message,
            created = saveResult.CreatedCount,
            skipped = saveResult.SkippedCount,
            remainingPreviewCount = remaining.Count
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarTurnosPreview([FromBody] JsonElement payload)
    {
        var dto = ParsePreviewConfirmPayload(payload);

        var equipoId = dto.EquipoId;
        if (string.IsNullOrWhiteSpace(equipoId) && dto.GruposIds.Any())
        {
            equipoId = await _db.Grupos
                .AsNoTracking()
                .Where(g => dto.GruposIds.Contains(g.GrupoId))
                .Select(g => g.EquipoId)
                .FirstOrDefaultAsync();
        }
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return Json(new { success = false, error = "No se pudo resolver el equipo para cancelar" });
        }
        var cacheKey = GetPreviewCacheKey(equipoId ?? string.Empty);
        _cache.Remove(cacheKey);
        if (!string.IsNullOrWhiteSpace(equipoId))
        {
            RemovePreviewKeyForEquipo(equipoId);
        }
        if (dto.GruposIds.Any())
        {
            var equiposFromGrupos = await _db.Grupos
                .AsNoTracking()
                .Where(g => dto.GruposIds.Contains(g.GrupoId))
                .Select(g => g.EquipoId)
                .Distinct()
                .ToListAsync();
            foreach (var eqId in equiposFromGrupos)
            {
                RemovePreviewKeyForEquipo(eqId);
            }
        }
        ClearPreviewKeysForUser();
        return Json(new { success = true });
    }

    private static List<TurnoGeneradoPreview> FiltrarPreviewPorSemana(
        IReadOnlyCollection<TurnoGeneradoPreview> previewList,
        DateOnly? weekStart)
    {
        var targetStart = weekStart ?? DateOnly.FromDateTime(ObtenerLunesSemana(DateTime.Today));
        var targetEnd = targetStart.AddDays(8);
        return previewList
            .Where(p => p.FechaTurno >= targetStart && p.FechaTurno <= targetEnd)
            .ToList();
    }

    private static DateTime ObtenerLunesSemana(DateTime fecha)
    {
        var date = fecha.Date;
        int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    private static GenerarTurnosDto ParseGenerarTurnosPayload(JsonElement payload)
    {
        var dto = new GenerarTurnosDto
        {
            EquipoId = GetJsonString(payload, "equipoId"),
            GrupoId = GetJsonString(payload, "grupoId"),
            NumeroSemanas = GetJsonInt(payload, "numeroSemanas"),
            FechaInicio = GetJsonStringOrNull(payload, "fechaInicio"),
            OperationId = GetJsonStringOrNull(payload, "operationId"),
            AutorizarSobrecupoSemanalFeriado = GetJsonBool(payload, "autorizarSobrecupoSemanalFeriado"),
            NivelUsoDescanso7Horas = GetJsonStringOrNull(payload, "nivelUsoDescanso7Horas") ?? "low",
            NivelEvitarFinesSemanaConsecutivos = GetJsonStringOrNull(payload, "nivelEvitarFinesSemanaConsecutivos") ?? "low",
            BalancearHorasSemanales = !TryGetProperty(payload, "balancearHorasSemanales", out _) || GetJsonBool(payload, "balancearHorasSemanales")
        };

        if (TryGetProperty(payload, "gruposIds", out var gruposElement) && gruposElement.ValueKind == JsonValueKind.Array)
        {
            dto.GruposIds = gruposElement
                .EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();
        }

        return dto;
    }

    private static PreviewConfirmDto ParsePreviewConfirmPayload(JsonElement payload)
    {
        var dto = new PreviewConfirmDto
        {
            EquipoId = GetJsonString(payload, "equipoId"),
            SaveAllWeeks = GetJsonBool(payload, "saveAllWeeks"),
            WeekStart = GetJsonStringOrNull(payload, "weekStart")
        };

        if (TryGetProperty(payload, "gruposIds", out var gruposElement) && gruposElement.ValueKind == JsonValueKind.Array)
        {
            dto.GruposIds = gruposElement
                .EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();
        }

        return dto;
    }

    private static bool TryGetProperty(JsonElement payload, string propertyName, out JsonElement value)
    {
        value = default;
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in payload.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static string GetJsonString(JsonElement payload, string propertyName)
    {
        return TryGetProperty(payload, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? string.Empty)
            : string.Empty;
    }

    private static string? GetJsonStringOrNull(JsonElement payload, string propertyName)
    {
        if (!TryGetProperty(payload, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int GetJsonInt(JsonElement payload, string propertyName)
    {
        if (!TryGetProperty(payload, propertyName, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
        {
            return number;
        }

        return 0;
    }

    private static bool GetJsonBool(JsonElement payload, string propertyName)
    {
        if (!TryGetProperty(payload, propertyName, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return false;
    }

#if false
    private async Task EnsureConfiguracionRotacionCompatibilityAsync()
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS configuracion_rotacion_equipo (
                configuracion_rotacion_equipo_id varchar(12) PRIMARY KEY DEFAULT generate_short_id(),
                equipo_id varchar(12) NOT NULL REFERENCES equipo(equipo_id) ON DELETE CASCADE,
                minutos_objetivo_semanales integer NOT NULL DEFAULT 2400,
                minutos_minimos_descanso_entre_turnos integer NOT NULL DEFAULT 480,
                minimo_dias_descanso_consecutivos_por_semana integer NOT NULL DEFAULT 2,
                maximo_turnos_por_dia integer NOT NULL DEFAULT 1,
                aplicar_vacaciones boolean NOT NULL DEFAULT TRUE,
                permite_turnos_auxiliares boolean NOT NULL DEFAULT TRUE,
                evitar_fines_semana_consecutivos boolean NOT NULL DEFAULT TRUE,
                maximo_fines_semana_consecutivos integer NOT NULL DEFAULT 2,
                maximo_slots_fin_semana_por_mes integer NULL,
                maximo_turnos_nocturnos_por_mes integer NULL,
                balancear_horas_semanales boolean NOT NULL DEFAULT TRUE,
                balancear_turnos_nocturnos boolean NOT NULL DEFAULT TRUE,
                balancear_carga_feriados boolean NOT NULL DEFAULT TRUE
            );
            """;

        const string createUniqueIndexSql = """
            CREATE UNIQUE INDEX IF NOT EXISTS ix_configuracion_rotacion_equipo_equipo_id
            ON configuracion_rotacion_equipo (equipo_id);
            """;

        await _db.Database.ExecuteSqlRawAsync(createTableSql);
        await _db.Database.ExecuteSqlRawAsync(createUniqueIndexSql);
    }

    private async Task<ConfiguracionRotacionEquipoViewModel> ConstruirConfiguracionRotacionViewModelAsync(string equipoId)
    {
        await EnsureConfiguracionRotacionCompatibilityAsync();

        var configuracion = await _db.ConfiguracionesRotacionEquipo
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.EquipoId == equipoId);

        if (configuracion == null)
        {
            return new ConfiguracionRotacionEquipoViewModel();
        }

        return new ConfiguracionRotacionEquipoViewModel
        {
            HorasObjetivoSemanales = Math.Max(1, configuracion.MinutosObjetivoSemanales / 60),
            HorasMinimasDescansoEntreTurnos = Math.Max(1, configuracion.MinutosMinimosDescansoEntreTurnos / 60),
            MinimoDiasDescansoConsecutivosPorSemana = Math.Max(1, configuracion.MinimoDiasDescansoConsecutivosPorSemana),
            MaximoTurnosPorDia = Math.Max(1, configuracion.MaximoTurnosPorDia),
            AplicarVacaciones = configuracion.AplicarVacaciones,
            PermiteTurnosAuxiliares = configuracion.PermiteTurnosAuxiliares,
            EvitarFinesSemanaConsecutivos = configuracion.EvitarFinesSemanaConsecutivos,
            MaximoFinesSemanaConsecutivos = Math.Max(1, configuracion.MaximoFinesSemanaConsecutivos),
            MaximoSlotsFinSemanaPorMes = configuracion.MaximoSlotsFinSemanaPorMes,
            MaximoTurnosNocturnosPorMes = configuracion.MaximoTurnosNocturnosPorMes,
            BalancearHorasSemanales = configuracion.BalancearHorasSemanales,
            BalancearTurnosNocturnos = configuracion.BalancearTurnosNocturnos,
            BalancearCargaFeriados = configuracion.BalancearCargaFeriados
        };
    }
#endif

    private string GetCurrentUserRole()
    {
        if (User.IsInRole("SuperAdmin")) return "SuperAdmin";
        if (User.IsInRole("Admin")) return "Admin";
        if (User.IsInRole("Lider")) return "Lider";
        return string.Empty;
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

    private async Task<bool> CanManageEquipoAsync(string equipoId)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return false;
        }

        var role = GetCurrentUserRole();
        if (role == "SuperAdmin" || role == "Admin")
        {
            return true;
        }

        if (role != "Lider")
        {
            return false;
        }

        var liderEquipoId = await GetCurrentUserEquipoIdAsync();
        return !string.IsNullOrWhiteSpace(liderEquipoId)
            && string.Equals(liderEquipoId, equipoId, StringComparison.Ordinal);
    }

    private async Task<(bool Succeeded, string? Error, string? EquipoId)> ResolveGrupoScopeAsync(string grupoId, string? expectedEquipoId = null)
    {
        if (string.IsNullOrWhiteSpace(grupoId))
        {
            return (false, "Grupo no especificado", null);
        }

        var grupoEquipoId = await _db.Grupos
            .AsNoTracking()
            .Where(g => g.GrupoId == grupoId)
            .Select(g => g.EquipoId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(grupoEquipoId))
        {
            return (false, "Grupo no existe.", null);
        }

        if (!string.IsNullOrWhiteSpace(expectedEquipoId)
            && !string.Equals(grupoEquipoId, expectedEquipoId, StringComparison.Ordinal))
        {
            return (false, "Grupo invalido para el equipo seleccionado", null);
        }

        if (!await CanManageEquipoAsync(grupoEquipoId))
        {
            return (false, "No tiene permiso para administrar este grupo.", grupoEquipoId);
        }

        return (true, null, grupoEquipoId);
    }

    private async Task<(bool Succeeded, string? Error)> ValidateGruposScopeAsync(IReadOnlyCollection<string> grupoIds, string equipoId)
    {
        if (grupoIds == null || grupoIds.Count == 0)
        {
            return (true, null);
        }

        var grupos = await _db.Grupos
            .AsNoTracking()
            .Where(g => grupoIds.Contains(g.GrupoId))
            .Select(g => new { g.GrupoId, g.EquipoId, g.Activo, g.NombreGrupo })
            .ToListAsync();

        if (grupos.Count != grupoIds.Count)
        {
            return (false, "Uno o mas grupos no existen.");
        }

        var gruposInactivos = grupos
            .Where(g => !g.Activo)
            .Select(g => string.IsNullOrWhiteSpace(g.NombreGrupo) ? g.GrupoId : g.NombreGrupo)
            .ToList();
        if (gruposInactivos.Count > 0)
        {
            return (false, $"Uno o mas grupos estan inactivos y no se pueden usar para generar horarios: {string.Join(", ", gruposInactivos)}.");
        }

        if (grupos.Any(g => !string.Equals(g.EquipoId, equipoId, StringComparison.Ordinal)))
        {
            return (false, "Uno o mas grupos no pertenecen al equipo seleccionado.");
        }

        if (!await CanManageEquipoAsync(equipoId))
        {
            return (false, "No tiene permiso para administrar este equipo.");
        }

        return (true, null);
    }

    public class SavePlanificacionDto
    {
        public string EquipoId { get; set; } = string.Empty;
        public string GrupoId { get; set; } = string.Empty;
        public int? MaximoSlotsFinSemanaPorMes { get; set; }
        public int? MaximoTurnosNocturnosPorMes { get; set; }
        public int? MaximoTurnosNocturnosPorSemana { get; set; }
        public List<PlanificacionItemDto> Planificaciones { get; set; } = new();
        public List<PlanificacionAuxiliarItemDto> Auxiliares { get; set; } = new();
        public List<PlanificacionApoyoItemDto> Apoyos { get; set; } = new();
        public List<PlanificacionOpcionalVacacionItemDto> OpcionalesVacacion { get; set; } = new();
        public bool UsaSoloSecundarios { get; set; }
        public string? GrupoFuenteSecundariosId { get; set; }
        public bool UsarPersonaUnicaPorSemana { get; set; }
    }

    public class SaveEquipoPlanificacionConfigDto
    {
        public string EquipoId { get; set; } = string.Empty;
        public int? MaximoSlotsFinSemanaPorMes { get; set; }
        public int? MaximoTurnosNocturnosPorMes { get; set; }
        public int? MaximoTurnosNocturnosPorSemana { get; set; }
    }

    public class SaveBlueprintDto
    {
        public string EquipoId { get; set; } = string.Empty;
        public string? GrupoId { get; set; }
        public bool UsaGruposSecundarios { get; set; } = false;
        public List<BlueprintItemDto> Entries { get; set; } = new();
    }

    public class BlueprintItemDto
    {
        public string Dia { get; set; } = string.Empty;
        public string TipoTurnoId { get; set; } = string.Empty;
        public string Etiquetas { get; set; } = string.Empty;
        public int MinPersonasTurno { get; set; }
    }

    public class SaveFeriadoCoberturaDto
    {
        public string EquipoId { get; set; } = string.Empty;
        public string GrupoId { get; set; } = string.Empty;
        public List<FeriadoCoberturaItemDto> Turnos { get; set; } = new();
    }

    public class FeriadoCoberturaItemDto
    {
        public string TipoTurnoId { get; set; } = string.Empty;
        public int CantidadVisible { get; set; }
    }

    public class SaveConfiguracionRotacionDto
    {
        public string EquipoId { get; set; } = string.Empty;
        public int HorasObjetivoSemanales { get; set; }
        public int HorasMinimasDescansoEntreTurnos { get; set; }
        public int MinimoDiasDescansoConsecutivosPorSemana { get; set; }
        public int MaximoTurnosPorDia { get; set; }
        public bool AplicarVacaciones { get; set; } = true;
        public bool PermiteTurnosAuxiliares { get; set; } = true;
        public bool EvitarFinesSemanaConsecutivos { get; set; } = true;
        public int MaximoFinesSemanaConsecutivos { get; set; }
        public int? MaximoSlotsFinSemanaPorMes { get; set; }
        public int? MaximoTurnosNocturnosPorMes { get; set; }
        public int? MaximoTurnosNocturnosPorSemana { get; set; }
        public bool BalancearHorasSemanales { get; set; } = true;
        public bool BalancearTurnosNocturnos { get; set; } = true;
        public bool BalancearCargaFeriados { get; set; } = true;
    }

    public class PlanificacionItemDto
    {
        public string Dia { get; set; } = string.Empty;
        public string TipoTurnoId { get; set; } = string.Empty;
        public int NumeroPersonas { get; set; }
    }

    public class PlanificacionAuxiliarItemDto
    {
        public string TipoTurnoId { get; set; } = string.Empty;
        public string DesdeDia { get; set; } = string.Empty;
        public string HastaDia { get; set; } = string.Empty;
        public int MaxPorDia { get; set; }
        public List<string> GrupoIds { get; set; } = new();
    }

    public class PlanificacionApoyoItemDto
    {
        public string Dia { get; set; } = string.Empty;
        public string TipoTurnoId { get; set; } = string.Empty;
        public int CantidadApoyo { get; set; }
    }

    public class PlanificacionOpcionalVacacionItemDto
    {
        public string Dia { get; set; } = string.Empty;
        public string TipoTurnoId { get; set; } = string.Empty;
    }

    public class GenerarTurnosDto
    {
        public string EquipoId { get; set; } = string.Empty;
        public string GrupoId { get; set; } = string.Empty;
        public List<string> GruposIds { get; set; } = new();
        public int NumeroSemanas { get; set; }
        public string? FechaInicio { get; set; }
        public string? OperationId { get; set; }
        public bool AutorizarSobrecupoSemanalFeriado { get; set; }
        public string NivelUsoDescanso7Horas { get; set; } = "low";
        public string NivelEvitarFinesSemanaConsecutivos { get; set; } = "low";
        public bool BalancearHorasSemanales { get; set; } = true;
    }

    public class PreviewConfirmDto
    {
        public string EquipoId { get; set; } = string.Empty;
        public List<string> GruposIds { get; set; } = new List<string>();
        public bool SaveAllWeeks { get; set; }
        public string? WeekStart { get; set; }
    }

    private static int GetDiaOrden(string? dia)
    {
        if (string.IsNullOrWhiteSpace(dia))
        {
            return int.MaxValue;
        }

        var index = Array.FindIndex(
            PlanificacionViewModel.DiasSemana,
            d => string.Equals(d, dia, StringComparison.OrdinalIgnoreCase));

        return index >= 0 ? index : int.MaxValue;
    }

    private static List<string> ExpandirRangoDias(string desdeDia, string hastaDia)
    {
        var dias = PlanificacionViewModel.DiasSemana;
        var desde = Array.FindIndex(dias, d => string.Equals(d, desdeDia, StringComparison.OrdinalIgnoreCase));
        var hasta = Array.FindIndex(dias, d => string.Equals(d, hastaDia, StringComparison.OrdinalIgnoreCase));

        if (desde < 0 || hasta < 0)
        {
            return new List<string>();
        }

        var result = new List<string>();
        var index = desde;
        while (true)
        {
            result.Add(dias[index]);
            if (index == hasta)
            {
                break;
            }

            index = (index + 1) % dias.Length;
            if (index == desde)
            {
                return new List<string>();
            }
        }

        return result;
    }

    private static (string DesdeDia, string HastaDia) ResolverRangoAuxiliar(IEnumerable<string> diasConfigurados)
    {
        var dias = PlanificacionViewModel.DiasSemana;
        var diasSet = diasConfigurados
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (diasSet.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        if (diasSet.Count == 1)
        {
            var unico = diasSet.First();
            return (unico, unico);
        }

        for (var i = 0; i < dias.Length; i++)
        {
            if (!diasSet.Contains(dias[i]))
            {
                continue;
            }

            var prevIndex = (i - 1 + dias.Length) % dias.Length;
            if (diasSet.Contains(dias[prevIndex]))
            {
                continue;
            }

            var start = dias[i];
            var current = i;
            while (diasSet.Contains(dias[(current + 1) % dias.Length]))
            {
                current = (current + 1) % dias.Length;
                if (current == i)
                {
                    break;
                }
            }

            return (start, dias[current]);
        }

        var first = dias.First(d => diasSet.Contains(d));
        var last = dias.Last(d => diasSet.Contains(d));
        return (first, last);
    }

    private string GetPreviewCacheKey(string equipoId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon";
        return $"PlanificacionPreview:{userId}:{equipoId}";
    }

    private string GetPreviewIndexKey()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon";
        return $"PlanificacionPreviewIndex:{userId}";
    }

    private void AddPreviewKeyForUser(string equipoId)
    {
        var indexKey = GetPreviewIndexKey();
        var keys = _cache.TryGetValue(indexKey, out HashSet<string>? existing) && existing != null
            ? existing
            : new HashSet<string>();
        keys.Add(GetPreviewCacheKey(equipoId));
        _cache.Set(indexKey, keys, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(2)
        });
    }

    private void ClearPreviewKeysForUser()
    {
        var indexKey = GetPreviewIndexKey();
        if (_cache.TryGetValue(indexKey, out HashSet<string>? keys) && keys != null)
        {
            foreach (var key in keys)
            {
                _cache.Remove(key);
            }
        }
        _cache.Remove(indexKey);
    }

    private void RemovePreviewKeyForEquipo(string equipoId)
    {
        var key = GetPreviewCacheKey(equipoId);
        _cache.Remove(key);
    }

    private async Task SyncEquipoTipoTurnosAsync(string grupoId)
    {
        var equipoId = await _db.Grupos
            .AsNoTracking()
            .Where(g => g.GrupoId == grupoId)
            .Select(g => g.EquipoId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return;
        }

        await SyncEquipoTipoTurnosByEquipoAsync(equipoId);
    }

    private async Task SyncEquipoTipoTurnosByEquipoAsync(string equipoId)
    {
        var grupoIds = await _db.Grupos
            .AsNoTracking()
            .Where(g => g.EquipoId == equipoId && g.Activo)
            .Select(g => g.GrupoId)
            .ToListAsync();

        var usadosEnPlanificacion = grupoIds.Count == 0
            ? new List<string>()
            : await _db.Planificaciones
                .AsNoTracking()
                .Where(p => grupoIds.Contains(p.GrupoId) && p.NumeroPersonas > 0)
                .Select(p => p.TipoTurnoId)
                .Distinct()
                .ToListAsync();

        var usadosEnAuxiliaresCompartidos = await _db.PlanificacionesAuxiliaresEquipo
            .AsNoTracking()
            .Where(p => p.EquipoId == equipoId
                && !(p.DesdeDia == DiaConfiguracionNocturnosMes && p.HastaDia == DiaConfiguracionNocturnosMes))
            .Select(p => p.TipoTurnoId)
            .Distinct()
            .ToListAsync();

        var actuales = await _db.EquipoTipoTurnos
            .Where(et => et.EquipoId == equipoId)
            .ToListAsync();

        var actualesSet = actuales.Select(a => a.TipoTurnoId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usadosSet = usadosEnPlanificacion
            .Concat(usadosEnAuxiliaresCompartidos)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var agregar = usadosSet
            .Where(t => !actualesSet.Contains(t))
            .Select(t => new Models.EquipoTipoTurno { EquipoId = equipoId, TipoTurnoId = t })
            .ToList();

        if (agregar.Count > 0)
        {
            _db.EquipoTipoTurnos.AddRange(agregar);
        }

        if (agregar.Count > 0)
        {
            await _db.SaveChangesAsync();
        }
    }

}
