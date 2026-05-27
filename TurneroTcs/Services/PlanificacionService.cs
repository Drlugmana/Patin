using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Services.Interfaces;
using TurneroTcs.Services.RotacionV2.Application;
using TurneroTcs.Services.RotacionV2.Constraints;
using TurneroTcs.Services.RotacionV2.Domain;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Services;

public class PlanificacionService : IPlanificacionService
{
    public const string HolidayOvertimeApprovalRequiredPrefix = "HOLIDAY_OVERTIME_APPROVAL_REQUIRED|";

    private const string DiaConfiguracionNocturnosMes = "__CFG_MAX_NOCTURNOS_MES__";
    private const int MaximoTurnosNocturnosPorMesDefault = 15;
    private const int MaximoTurnosNocturnosPorMesMin = 7;
    private const int MaximoTurnosNocturnosPorMesMax = 20;
    private const int MaximoTurnosNocturnosPorSemanaDefault = 2;
    private const int MaximoTurnosNocturnosPorSemanaMin = 1;
    private const int MaximoTurnosNocturnosPorSemanaMax = 7;
    private const int MaximoSlotsFinSemanaPorMesDefault = 10;
    private const int MaximoSlotsFinSemanaPorMesMin = 1;
    private const int MaximoSlotsFinSemanaPorMesMax = 12;
    private static readonly JsonSerializerOptions PlanificacionLogJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateOnly FechaBaseCalculoTurno = new(2000, 1, 1);

    private readonly ApplicationDbContext _db;
    private readonly ILogger<PlanificacionService> _logger;
    private readonly ServicioRotacion _rotacionV2;

    public PlanificacionService(ApplicationDbContext db, ILogger<PlanificacionService> logger)
    {
        _db = db;
        _logger = logger;
        _rotacionV2 = new ServicioRotacion();
    }

    public async Task<IReadOnlyList<Planificacion>> GetByGrupoIdAsync(string grupoId)
    {
        try
        {
            var planificaciones = await _db.Planificaciones
                .Where(p => p.GrupoId == grupoId)
                .AsNoTracking()
                .OrderBy(p => p.Dia)
                .ThenBy(p => p.TipoTurnoId)
                .ToListAsync();

            _logger.LogDebug("Lista de {Count} planificaciones para grupo {GrupoId}", planificaciones.Count, grupoId);
            return planificaciones;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener planificaciones del grupo {GrupoId}", grupoId);
            throw;
        }
    }

    public async Task<IReadOnlyList<Planificacion>> GetByEquipoIdAsync(string equipoId)
    {
        try
        {
            var planificaciones = await _db.Planificaciones
                .Include(p => p.Grupo)
                .Where(p => p.Grupo != null && p.Grupo.EquipoId == equipoId)
                .AsNoTracking()
                .OrderBy(p => p.Dia)
                .ThenBy(p => p.TipoTurnoId)
                .ToListAsync();

            _logger.LogDebug("Lista de {Count} planificaciones para equipo {EquipoId}", planificaciones.Count, equipoId);
            return planificaciones;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener planificaciones del equipo {EquipoId}", equipoId);
            throw;
        }
    }

    public async Task<IReadOnlyList<PlanificacionAuxiliarEquipo>> GetAuxiliaresByEquipoIdAsync(string equipoId)
    {
        try
        {
            var auxiliares = await _db.PlanificacionesAuxiliaresEquipo
                .Include(p => p.GruposPermitidos)
                .Where(p => p.EquipoId == equipoId
                    && !(p.DesdeDia == DiaConfiguracionNocturnosMes && p.HastaDia == DiaConfiguracionNocturnosMes))
                .AsNoTracking()
                .OrderBy(p => p.TipoTurnoId)
                .ToListAsync();

            return auxiliares;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener auxiliares del equipo {EquipoId}", equipoId);
            throw;
        }
    }

    public async Task<IReadOnlyList<PlanificacionApoyoGrupo>> GetApoyosByGrupoIdAsync(string grupoId)
    {
        try
        {
            var apoyos = await _db.PlanificacionesApoyoGrupo
                .Where(p => p.GrupoId == grupoId)
                .AsNoTracking()
                .OrderBy(p => p.Dia)
                .ThenBy(p => p.TipoTurnoId)
                .ToListAsync();

            return apoyos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener apoyos del grupo {GrupoId}", grupoId);
            throw;
        }
    }

    public async Task<IReadOnlyList<PlanificacionTurnoOpcionalVacacionGrupo>> GetTurnosOpcionalesVacacionByGrupoIdAsync(string grupoId)
    {
        try
        {
            var turnosOpcionales = await _db.PlanificacionesTurnosOpcionalesVacacionGrupo
                .Where(p => p.GrupoId == grupoId)
                .AsNoTracking()
                .OrderBy(p => p.Dia)
                .ThenBy(p => p.TipoTurnoId)
                .ToListAsync();

            return turnosOpcionales;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener turnos opcionales por vacaciones del grupo {GrupoId}", grupoId);
            throw;
        }
    }

    public async Task<int> GetMaximoTurnosNocturnosPorMesAsync(string equipoId)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return MaximoTurnosNocturnosPorMesDefault;
        }

        var configuracionEquipo = await _db.EquipoPlanificacionConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(config => config.EquipoId == equipoId);
        if (configuracionEquipo?.MaximoTurnosNocturnosPorMes is > 0)
        {
            return NormalizarMaximoTurnosNocturnosPorMes(configuracionEquipo.MaximoTurnosNocturnosPorMes);
        }

        var configuracion = await _db.PlanificacionesAuxiliaresEquipo
            .AsNoTracking()
            .Where(p => p.EquipoId == equipoId
                && p.DesdeDia == DiaConfiguracionNocturnosMes
                && p.HastaDia == DiaConfiguracionNocturnosMes)
            .OrderByDescending(p => p.PlanificacionAuxiliarEquipoId)
            .FirstOrDefaultAsync();

        return NormalizarMaximoTurnosNocturnosPorMes(configuracion?.MaxPorDia);
    }

    public async Task<EquipoPlanificacionConfigResult> GetEquipoPlanificacionConfigAsync(string equipoId)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return new EquipoPlanificacionConfigResult(
                MaximoSlotsFinSemanaPorMesDefault,
                MaximoTurnosNocturnosPorMesDefault,
                MaximoTurnosNocturnosPorSemanaDefault);
        }

        var configuracion = await _db.EquipoPlanificacionConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(config => config.EquipoId == equipoId);

        var maximoNocturnos = configuracion?.MaximoTurnosNocturnosPorMes is > 0
            ? NormalizarMaximoTurnosNocturnosPorMes(configuracion.MaximoTurnosNocturnosPorMes)
            : await GetMaximoTurnosNocturnosPorMesAsync(equipoId);
        var maximoNocturnosSemana = NormalizarMaximoTurnosNocturnosPorSemana(configuracion?.MaximoTurnosNocturnosPorSemana);

        return new EquipoPlanificacionConfigResult(
            NormalizarMaximoSlotsFinSemanaPorMes(configuracion?.MaximoSlotsFinSemanaPorMes),
            maximoNocturnos,
            maximoNocturnosSemana);
    }

    public async Task<Result> SaveEquipoPlanificacionConfigAsync(
        string equipoId,
        int? maximoSlotsFinSemanaPorMes,
        int? maximoTurnosNocturnosPorMes,
        int? maximoTurnosNocturnosPorSemana)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return Result.Fail("Equipo no especificado.");
        }

        var equipoExiste = await _db.Equipos
            .AsNoTracking()
            .AnyAsync(equipo => equipo.EquipoId == equipoId);
        if (!equipoExiste)
        {
            return Result.Fail("Equipo no encontrado.");
        }

        await GuardarEquipoPlanificacionConfigNormalizadaAsync(
            equipoId,
            maximoSlotsFinSemanaPorMes,
            maximoTurnosNocturnosPorMes,
            maximoTurnosNocturnosPorSemana);
        await _db.SaveChangesAsync();

        return Result.Ok();
    }

    public async Task<Result> SavePlanificacionAsync(
        IEnumerable<PlanificacionSaveRequest> requests,
        IEnumerable<PlanificacionAuxiliarSaveRequest> auxiliares,
        IEnumerable<PlanificacionApoyoSaveRequest> apoyos,
        IEnumerable<PlanificacionTurnoOpcionalVacacionSaveRequest> turnosOpcionalesVacacion,
        string grupoId,
        string equipoId,
        int? maximoSlotsFinSemanaPorMes,
        int? maximoTurnosNocturnosPorMes,
        int? maximoTurnosNocturnosPorSemana,
        bool usaSoloSecundarios = false,
        string? grupoFuenteSecundariosId = null,
        bool usarPersonaUnicaPorSemana = false)
    {
        var normalRequests = (requests ?? Enumerable.Empty<PlanificacionSaveRequest>()).ToList();
        var auxiliarRequests = (auxiliares ?? Enumerable.Empty<PlanificacionAuxiliarSaveRequest>()).ToList();
        var apoyoRequests = (apoyos ?? Enumerable.Empty<PlanificacionApoyoSaveRequest>()).ToList();
        var turnosOpcionalesVacacionRequests = (turnosOpcionalesVacacion ?? Enumerable.Empty<PlanificacionTurnoOpcionalVacacionSaveRequest>()).ToList();

        _logger.LogInformation(
            "Payload recibido SavePlanificacion grupo={GrupoId} equipo={EquipoId}: {Payload}",
            grupoId,
            equipoId,
            JsonSerializer.Serialize(new
            {
                GrupoId = grupoId,
                EquipoId = equipoId,
                MaximoSlotsFinSemanaPorMes = maximoSlotsFinSemanaPorMes,
                MaximoTurnosNocturnosPorMes = maximoTurnosNocturnosPorMes,
                MaximoTurnosNocturnosPorSemana = maximoTurnosNocturnosPorSemana,
                UsaSoloSecundarios = usaSoloSecundarios,
                GrupoFuenteSecundariosId = grupoFuenteSecundariosId,
                UsarPersonaUnicaPorSemana = usarPersonaUnicaPorSemana,
                Planificaciones = normalRequests,
                Auxiliares = auxiliarRequests,
                Apoyos = apoyoRequests,
                OpcionalesVacacion = turnosOpcionalesVacacionRequests
            }, PlanificacionLogJsonOptions));

        if (!normalRequests.Any()
            && !auxiliarRequests.Any()
            && !apoyoRequests.Any()
            && !turnosOpcionalesVacacionRequests.Any()
            && !maximoSlotsFinSemanaPorMes.HasValue
            && !maximoTurnosNocturnosPorMes.HasValue
            && !maximoTurnosNocturnosPorSemana.HasValue
            && !usaSoloSecundarios
            && !usarPersonaUnicaPorSemana
            && string.IsNullOrWhiteSpace(grupoFuenteSecundariosId))
        {
            return Result.Fail("No se proporcionaron datos de planificacion");
        }

        try
        {
            Grupo? grupo = null;
            var equipoSeleccionado = await _db.Equipos
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EquipoId == equipoId);
            if (!string.IsNullOrWhiteSpace(grupoId))
            {
                grupo = await _db.Grupos
                    .Include(g => g.Equipo)
                    .FirstOrDefaultAsync(g => g.GrupoId == grupoId);
                if (grupo == null)
                {
                    return Result.Fail($"No existe el grupo seleccionado. Referencia recibida: {grupoId}.");
                }

                if (!grupo.Activo)
                {
                    return Result.Fail($"El {DescribirGrupo(grupo)} esta inactivo y no se puede usar en planificacion.");
                }

                if (!string.Equals(grupo.EquipoId, equipoId, StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Fail(
                        $"El {DescribirGrupo(grupo)} pertenece al {DescribirEquipo(grupo.Equipo, grupo.EquipoId)}, no al {DescribirEquipo(equipoSeleccionado, equipoId)}.");
                }
            }
            else if (normalRequests.Any())
            {
                return Result.Fail("No se puede guardar cobertura normal sin un grupo seleccionado");
            }

            if (apoyoRequests.Any() && grupo == null)
            {
                return Result.Fail("No se puede guardar apoyo eventual sin un grupo seleccionado");
            }

            if (turnosOpcionalesVacacionRequests.Any() && grupo == null)
            {
                return Result.Fail("No se puede guardar turnos opcionales por vacaciones sin un grupo seleccionado");
            }

            var gruposEquipo = await _db.Grupos
                .AsNoTracking()
                .Where(g => g.EquipoId == equipoId && g.Activo)
                .ToListAsync();

            var grupoIdsEquipo = gruposEquipo
                .Select(g => g.GrupoId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            grupoFuenteSecundariosId = string.IsNullOrWhiteSpace(grupoFuenteSecundariosId)
                ? null
                : grupoFuenteSecundariosId.Trim();
            if (usaSoloSecundarios)
            {
                if (grupo == null)
                {
                    return Result.Fail("Selecciona un grupo para activar uso de secundarios.");
                }

                if (string.IsNullOrWhiteSpace(grupoFuenteSecundariosId))
                {
                    return Result.Fail("Selecciona el grupo fuente para usar solo secundarios.");
                }

                if (string.Equals(grupoFuenteSecundariosId, grupoId, StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Fail("El grupo fuente debe ser diferente al grupo especial.");
                }

                if (!grupoIdsEquipo.Contains(grupoFuenteSecundariosId))
                {
                    return Result.Fail("El grupo fuente debe pertenecer al mismo equipo y estar activo.");
                }
            }
            else
            {
                grupoFuenteSecundariosId = null;
                usarPersonaUnicaPorSemana = false;
            }

            var normalizedRequests = normalRequests
                .Where(r => !string.IsNullOrWhiteSpace(r.Dia))
                .Where(r => !string.IsNullOrWhiteSpace(r.TipoTurnoId))
                .Select(r => new
                {
                    Dia = r.Dia.Trim(),
                    TipoTurnoId = r.TipoTurnoId.Trim(),
                    NumeroPersonas = Math.Max(0, r.NumeroPersonas)
                })
                .Where(r => r.NumeroPersonas > 0)
                .GroupBy(r => new { r.Dia, r.TipoTurnoId })
                .Select(g => g.Last())
                .ToList();

            if (usaSoloSecundarios && normalizedRequests.Count == 0)
            {
                return Result.Fail("El grupo especial debe tener cobertura normal configurada.");
            }

            if (usarPersonaUnicaPorSemana && !PuedeUsarPersonaUnicaPorSemana(
                    normalizedRequests.Select(r => (r.TipoTurnoId, r.NumeroPersonas))))
            {
                usarPersonaUnicaPorSemana = false;
            }

            var normalizedAuxiliares = auxiliarRequests
                .Where(a => !string.IsNullOrWhiteSpace(a.TipoTurnoId))
                .Select(a => new
                {
                    TipoTurnoId = a.TipoTurnoId.Trim(),
                    DesdeDia = a.DesdeDia.Trim(),
                    HastaDia = a.HastaDia.Trim(),
                    MaxPorDia = Math.Max(0, a.MaxPorDia),
                    GrupoIds = (a.GrupoIds ?? Array.Empty<string>())
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => id.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                })
                .Where(a => a.MaxPorDia > 0
                    && !string.IsNullOrWhiteSpace(a.DesdeDia)
                    && !string.IsNullOrWhiteSpace(a.HastaDia)
                    && a.GrupoIds.Count > 0)
                .GroupBy(a => a.TipoTurnoId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToList();

            var normalizedApoyos = apoyoRequests
                .Where(a => !string.IsNullOrWhiteSpace(a.Dia))
                .Where(a => !string.IsNullOrWhiteSpace(a.TipoTurnoId))
                .Select(a => new
                {
                    Dia = a.Dia.Trim(),
                    TipoTurnoId = a.TipoTurnoId.Trim(),
                    CantidadApoyo = Math.Max(0, a.CantidadApoyo)
                })
                .Where(a => a.CantidadApoyo > 0)
                .GroupBy(a => new { a.Dia, a.TipoTurnoId })
                .Select(g => g.Last())
                .ToList();

            var normalizedTurnosOpcionalesVacacion = turnosOpcionalesVacacionRequests
                .Where(t => !string.IsNullOrWhiteSpace(t.Dia))
                .Where(t => !string.IsNullOrWhiteSpace(t.TipoTurnoId))
                .Select(t => new
                {
                    Dia = t.Dia.Trim(),
                    TipoTurnoId = t.TipoTurnoId.Trim()
                })
                .GroupBy(t => new { t.Dia, t.TipoTurnoId })
                .Select(g => g.Last())
                .ToList();
            var configuracionNocturnosExistente = await _db.PlanificacionesAuxiliaresEquipo
                .AsNoTracking()
                .Where(p => p.EquipoId == equipoId
                    && p.DesdeDia == DiaConfiguracionNocturnosMes
                    && p.HastaDia == DiaConfiguracionNocturnosMes)
                .OrderByDescending(p => p.PlanificacionAuxiliarEquipoId)
                .FirstOrDefaultAsync();

            var maximoTurnosNocturnosPorMesActual = NormalizarMaximoTurnosNocturnosPorMes(configuracionNocturnosExistente?.MaxPorDia);
            var maximoTurnosNocturnosPorMesNormalizado = maximoTurnosNocturnosPorMes.HasValue
                ? NormalizarMaximoTurnosNocturnosPorMes(maximoTurnosNocturnosPorMes)
                : maximoTurnosNocturnosPorMesActual;
            var maximoTurnosNocturnosPorSemanaNormalizado = maximoTurnosNocturnosPorSemana.HasValue
                ? NormalizarMaximoTurnosNocturnosPorSemana(maximoTurnosNocturnosPorSemana)
                : MaximoTurnosNocturnosPorSemanaDefault;
            var maximoSlotsFinSemanaPorMesNormalizado = maximoSlotsFinSemanaPorMes.HasValue
                ? NormalizarMaximoSlotsFinSemanaPorMes(maximoSlotsFinSemanaPorMes)
                : MaximoSlotsFinSemanaPorMesDefault;
            var guardadoSoloAuxiliares = grupo == null
                && normalizedRequests.Count == 0
                && normalizedApoyos.Count == 0
                && normalizedTurnosOpcionalesVacacion.Count == 0
                && normalizedAuxiliares.Count > 0;
            var debeGuardarMaximoTurnosNocturnosPorMes = !guardadoSoloAuxiliares
                && maximoTurnosNocturnosPorMes.HasValue
                && (configuracionNocturnosExistente == null
                    || maximoTurnosNocturnosPorMesNormalizado != maximoTurnosNocturnosPorMesActual);

            _logger.LogInformation(
                "Payload normalizado SavePlanificacion grupo={GrupoId} equipo={EquipoId}: {Payload}",
                grupoId,
                equipoId,
                JsonSerializer.Serialize(new
                {
                    GrupoId = grupoId,
                    EquipoId = equipoId,
                    MaximoSlotsFinSemanaPorMes = maximoSlotsFinSemanaPorMes,
                    MaximoSlotsFinSemanaPorMesNormalizado = maximoSlotsFinSemanaPorMesNormalizado,
                    MaximoTurnosNocturnosPorMes = maximoTurnosNocturnosPorMes,
                    MaximoTurnosNocturnosPorMesActual = maximoTurnosNocturnosPorMesActual,
                    MaximoTurnosNocturnosPorMesNormalizado = maximoTurnosNocturnosPorMesNormalizado,
                    MaximoTurnosNocturnosPorSemana = maximoTurnosNocturnosPorSemana,
                    MaximoTurnosNocturnosPorSemanaNormalizado = maximoTurnosNocturnosPorSemanaNormalizado,
                    GuardadoSoloAuxiliares = guardadoSoloAuxiliares,
                    DebeGuardarMaximoTurnosNocturnosPorMes = debeGuardarMaximoTurnosNocturnosPorMes,
                    Planificaciones = normalizedRequests,
                    Auxiliares = normalizedAuxiliares,
                    Apoyos = normalizedApoyos,
                    OpcionalesVacacion = normalizedTurnosOpcionalesVacacion
                }, PlanificacionLogJsonOptions));

            var grupoIdsInvalidosAuxiliares = normalizedAuxiliares
                .SelectMany(a => a.GrupoIds)
                .Where(id => !grupoIdsEquipo.Contains(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (grupoIdsInvalidosAuxiliares.Count > 0)
            {
                var gruposInvalidos = await _db.Grupos
                    .Include(g => g.Equipo)
                    .AsNoTracking()
                    .Where(g => grupoIdsInvalidosAuxiliares.Contains(g.GrupoId))
                    .ToListAsync();
                var gruposInvalidosPorId = gruposInvalidos.ToDictionary(g => g.GrupoId, StringComparer.OrdinalIgnoreCase);
                var detalleGrupos = string.Join(", ", grupoIdsInvalidosAuxiliares.Select(id => DescribirGrupo(id, gruposInvalidosPorId)));

                return Result.Fail($"Los grupos del auxiliar no pertenecen al {DescribirEquipo(equipoSeleccionado, equipoId)}: {detalleGrupos}.");
            }

            var tipoTurnoIdsMensajes = normalizedRequests
                .Select(r => r.TipoTurnoId)
                .Concat(normalizedApoyos.Select(a => a.TipoTurnoId))
                .Concat(normalizedTurnosOpcionalesVacacion.Select(t => t.TipoTurnoId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var tiposTurnoMensajes = await _db.TipoTurnos
                .AsNoTracking()
                .Where(t => tipoTurnoIdsMensajes.Contains(t.TipoTurnoId))
                .ToListAsync();
            var tiposTurnoMensajesPorId = tiposTurnoMensajes.ToDictionary(t => t.TipoTurnoId, StringComparer.OrdinalIgnoreCase);

            if (grupo != null && normalizedApoyos.Count > 0)
            {
                var coberturaPorCelda = normalizedRequests.ToDictionary(
                    item => $"{item.Dia}|{item.TipoTurnoId}",
                    item => item.NumeroPersonas,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var apoyo in normalizedApoyos)
                {
                    var key = $"{apoyo.Dia}|{apoyo.TipoTurnoId}";
                    var cobertura = coberturaPorCelda.TryGetValue(key, out var totalCobertura) ? totalCobertura : 0;
                    if (apoyo.CantidadApoyo > cobertura)
                    {
                        return Result.Fail(
                            $"El apoyo eventual para {apoyo.Dia} / {DescribirTipoTurno(apoyo.TipoTurnoId, tiposTurnoMensajesPorId)} del {DescribirGrupo(grupo)} no puede exceder la cobertura normal configurada.");
                    }
                }
            }

            if (grupo != null && normalizedTurnosOpcionalesVacacion.Count > 0)
            {
                var coberturaPorCelda = normalizedRequests.ToDictionary(
                    item => $"{item.Dia}|{item.TipoTurnoId}",
                    item => item.NumeroPersonas,
                    StringComparer.OrdinalIgnoreCase);

                normalizedTurnosOpcionalesVacacion = normalizedTurnosOpcionalesVacacion
                    .Where(item =>
                    {
                        var key = $"{item.Dia}|{item.TipoTurnoId}";
                        return coberturaPorCelda.TryGetValue(key, out var cobertura) && cobertura > 0;
                    })
                    .ToList();
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();

            await GuardarEquipoPlanificacionConfigNormalizadaAsync(
                equipoId,
                maximoSlotsFinSemanaPorMes,
                maximoTurnosNocturnosPorMes,
                maximoTurnosNocturnosPorSemana);

            if (grupo != null)
            {
                var existentes = await _db.Planificaciones
                    .Where(p => p.GrupoId == grupoId)
                    .ToListAsync();

                if (existentes.Count > 0)
                {
                    _db.Planificaciones.RemoveRange(existentes);
                }

                var apoyosExistentes = await _db.PlanificacionesApoyoGrupo
                    .Where(p => p.GrupoId == grupoId)
                    .ToListAsync();

                if (apoyosExistentes.Count > 0)
                {
                    _db.PlanificacionesApoyoGrupo.RemoveRange(apoyosExistentes);
                }

                var turnosOpcionalesExistentes = await _db.PlanificacionesTurnosOpcionalesVacacionGrupo
                    .Where(p => p.GrupoId == grupoId)
                    .ToListAsync();

                if (turnosOpcionalesExistentes.Count > 0)
                {
                    _db.PlanificacionesTurnosOpcionalesVacacionGrupo.RemoveRange(turnosOpcionalesExistentes);
                }
            }

            var legacyAuxiliares = await _db.Planificaciones
                .Include(p => p.Grupo)
                .Where(p => p.IsAuxiliar && p.Grupo != null && p.Grupo.EquipoId == equipoId)
                .ToListAsync();

            if (legacyAuxiliares.Count > 0)
            {
                _db.Planificaciones.RemoveRange(legacyAuxiliares);
            }

            foreach (var request in normalizedRequests)
            {
                await _db.Planificaciones.AddAsync(new Planificacion
                {
                    PlanificacionId = Guid.NewGuid().ToString("N")[..12],
                    GrupoId = grupoId,
                    Dia = request.Dia,
                    TipoTurnoId = request.TipoTurnoId,
                    NumeroPersonas = request.NumeroPersonas,
                    IsAuxiliar = false,
                    UsaSoloSecundarios = usaSoloSecundarios,
                    GrupoFuenteSecundariosId = grupoFuenteSecundariosId,
                    UsarPersonaUnicaPorSemana = usarPersonaUnicaPorSemana
                });
            }

            if (grupo != null)
            {
                foreach (var apoyo in normalizedApoyos)
                {
                    await _db.PlanificacionesApoyoGrupo.AddAsync(new PlanificacionApoyoGrupo
                    {
                        PlanificacionApoyoGrupoId = Guid.NewGuid().ToString("N")[..12],
                        GrupoId = grupoId,
                        Dia = apoyo.Dia,
                        TipoTurnoId = apoyo.TipoTurnoId,
                        CantidadApoyo = apoyo.CantidadApoyo
                    });
                }

                foreach (var turnoOpcional in normalizedTurnosOpcionalesVacacion)
                {
                    await _db.PlanificacionesTurnosOpcionalesVacacionGrupo.AddAsync(new PlanificacionTurnoOpcionalVacacionGrupo
                    {
                        PlanificacionTurnoOpcionalVacacionGrupoId = Guid.NewGuid().ToString("N")[..12],
                        GrupoId = grupoId,
                        Dia = turnoOpcional.Dia,
                        TipoTurnoId = turnoOpcional.TipoTurnoId
                    });
                }
            }

            var tiposAuxiliaresPorGrupo = normalizedAuxiliares
                .SelectMany(a => a.GrupoIds.Select(grupoPermitido => new { GrupoId = grupoPermitido, a.TipoTurnoId }))
                .GroupBy(x => x.GrupoId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.TipoTurnoId).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            if (tiposAuxiliaresPorGrupo.Count > 0)
            {
                var gruposAfectados = tiposAuxiliaresPorGrupo.Keys.ToList();

                var planificacionesNormalesAuxiliares = await _db.Planificaciones
                    .Where(p => gruposAfectados.Contains(p.GrupoId))
                    .ToListAsync();

                var planificacionesNormalesEliminar = planificacionesNormalesAuxiliares
                    .Where(p => tiposAuxiliaresPorGrupo[p.GrupoId].Contains(p.TipoTurnoId))
                    .ToList();

                if (planificacionesNormalesEliminar.Count > 0)
                {
                    _db.Planificaciones.RemoveRange(planificacionesNormalesEliminar);
                }

            }

            var configuracionesExistentes = await _db.PlanificacionesAuxiliaresEquipo
                .Include(p => p.GruposPermitidos)
                .Where(p => p.EquipoId == equipoId
                    && (!(p.DesdeDia == DiaConfiguracionNocturnosMes && p.HastaDia == DiaConfiguracionNocturnosMes)
                        || normalizedAuxiliares.Select(a => a.TipoTurnoId).Contains(p.TipoTurnoId)))
                .ToListAsync();

            if (configuracionesExistentes.Count > 0)
            {
                _db.PlanificacionesAuxiliaresEquipo.RemoveRange(configuracionesExistentes);
            }

            foreach (var auxiliar in normalizedAuxiliares)
            {
                var config = new PlanificacionAuxiliarEquipo
                {
                    PlanificacionAuxiliarEquipoId = Guid.NewGuid().ToString("N")[..12],
                    EquipoId = equipoId,
                    TipoTurnoId = auxiliar.TipoTurnoId,
                    DesdeDia = auxiliar.DesdeDia,
                    HastaDia = auxiliar.HastaDia,
                    MaxPorDia = auxiliar.MaxPorDia
                };

                foreach (var grupoPermitidoId in auxiliar.GrupoIds)
                {
                    config.GruposPermitidos.Add(new PlanificacionAuxiliarEquipoGrupo
                    {
                        PlanificacionAuxiliarEquipoGrupoId = Guid.NewGuid().ToString("N")[..12],
                        GrupoId = grupoPermitidoId
                    });
                }

                await _db.PlanificacionesAuxiliaresEquipo.AddAsync(config);
            }

            if (debeGuardarMaximoTurnosNocturnosPorMes)
            {
                var configuracionesNocturnosExistentes = await _db.PlanificacionesAuxiliaresEquipo
                    .Include(p => p.GruposPermitidos)
                    .Where(p => p.EquipoId == equipoId
                        && p.DesdeDia == DiaConfiguracionNocturnosMes
                        && p.HastaDia == DiaConfiguracionNocturnosMes)
                    .ToListAsync();

                if (configuracionesNocturnosExistentes.Count > 0)
                {
                    _db.PlanificacionesAuxiliaresEquipo.RemoveRange(configuracionesNocturnosExistentes);
                }

                var tipoTurnoNocturnoConfiguracion = await ResolverTipoTurnoNocturnoConfiguracionAsync(
                    equipoId,
                    normalizedRequests.Select(item => item.TipoTurnoId)
                        .Concat(normalizedAuxiliares.Select(item => item.TipoTurnoId)));

                if (string.IsNullOrWhiteSpace(tipoTurnoNocturnoConfiguracion))
                {
                    _logger.LogInformation(
                        "No se guarda configuracion legacy nocturnos equipo={EquipoId}; no hay turno nocturno resoluble. El limite queda guardado en equipo_planificacion_config.",
                        equipoId);
                }
                else if (normalizedAuxiliares.Any(auxiliar =>
                    string.Equals(auxiliar.TipoTurnoId, tipoTurnoNocturnoConfiguracion, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogInformation(
                        "No se guarda configuracion nocturnos equipo={EquipoId}; tipoTurno={TipoTurnoId} ya existe como auxiliar y chocaria con indice unico.",
                        equipoId,
                        tipoTurnoNocturnoConfiguracion);
                }
                else
                {
                    _logger.LogInformation(
                        "Payload PlanificacionesAuxiliaresEquipo a guardar equipo={EquipoId}: {Payload}",
                        equipoId,
                        JsonSerializer.Serialize(new
                        {
                            Auxiliares = normalizedAuxiliares,
                            ConfiguracionNocturnosMes = new
                            {
                                EquipoId = equipoId,
                                TipoTurnoId = tipoTurnoNocturnoConfiguracion,
                                DesdeDia = DiaConfiguracionNocturnosMes,
                                HastaDia = DiaConfiguracionNocturnosMes,
                                MaxPorDia = maximoTurnosNocturnosPorMesNormalizado
                            }
                        }, PlanificacionLogJsonOptions));

                    await _db.PlanificacionesAuxiliaresEquipo.AddAsync(new PlanificacionAuxiliarEquipo
                    {
                        PlanificacionAuxiliarEquipoId = Guid.NewGuid().ToString("N")[..12],
                        EquipoId = equipoId,
                        TipoTurnoId = tipoTurnoNocturnoConfiguracion,
                        DesdeDia = DiaConfiguracionNocturnosMes,
                        HastaDia = DiaConfiguracionNocturnosMes,
                        MaxPorDia = maximoTurnosNocturnosPorMesNormalizado
                    });
                }
            }
            else
            {
                _logger.LogInformation(
                    "No se guarda configuracion nocturnos equipo={EquipoId}; limite sin cambios o guardado solo auxiliares. actual={Actual} recibido={Recibido}",
                    equipoId,
                    maximoTurnosNocturnosPorMesActual,
                    maximoTurnosNocturnosPorMes);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogInformation("Planificacion guardada exitosamente para grupo {GrupoId} y equipo {EquipoId}", grupoId, equipoId);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar planificacion para grupo {GrupoId} y equipo {EquipoId}", grupoId, equipoId);
            return Result.Fail("Error al guardar la planificacion");
        }
    }

    public async Task<Result> SavePlanificacionAsync(IEnumerable<PlanificacionSaveRequest> requests, string grupoId)
    {
        if (requests == null || !requests.Any())
        {
            return Result.Fail("No se proporcionaron datos de planificación");
        }

        try
        {
            var grupo = await _db.Grupos.FindAsync(grupoId);
            if (grupo == null)
            {
                return Result.Fail("El grupo especificado no existe");
            }

            var existentes = await _db.Planificaciones
                .Where(p => p.GrupoId == grupoId)
                .ToListAsync();

            if (existentes.Count > 0)
            {
                _db.Planificaciones.RemoveRange(existentes);
            }

            var normalizedRequests = requests
                .Where(r => !string.IsNullOrWhiteSpace(r.Dia))
                .Where(r => !string.IsNullOrWhiteSpace(r.TipoTurnoId))
                .Select(r => new
                {
                    Dia = r.Dia.Trim(),
                    TipoTurnoId = r.TipoTurnoId.Trim(),
                    NumeroPersonas = Math.Max(0, r.NumeroPersonas),
                    r.IsAuxiliar
                })
                .Where(r => r.NumeroPersonas > 0)
                .GroupBy(r => new { r.Dia, r.TipoTurnoId, r.IsAuxiliar })
                .Select(g => g.Last())
                .ToList();

            foreach (var request in normalizedRequests)
            {
                await _db.Planificaciones.AddAsync(new Planificacion
                {
                    PlanificacionId = Guid.NewGuid().ToString("N")[..12],
                    GrupoId = grupoId,
                    Dia = request.Dia,
                    TipoTurnoId = request.TipoTurnoId,
                    NumeroPersonas = request.NumeroPersonas,
                    IsAuxiliar = request.IsAuxiliar,
                    UsaSoloSecundarios = false,
                    GrupoFuenteSecundariosId = null,
                    UsarPersonaUnicaPorSemana = false
                });
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Planificación guardada exitosamente para grupo {GrupoId}", grupoId);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar planificación para grupo {GrupoId}", grupoId);
            return Result.Fail("Error al guardar la planificación");
        }
    }

    public async Task<IReadOnlyList<TipoTurno>> GetTipoTurnosAsync()
    {
        try
        {
            var tipoTurnos = await _db.TipoTurnos
                .Where(t => t.Activo)
                .AsNoTracking()
                .OrderBy(t => t.HoraInicio)
                .ToListAsync();

            return tipoTurnos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener tipos de turno");
            throw;
        }
    }

    public async Task<Result> ValidarGeneracionRotacionPreviewAsync(List<string> gruposIds)
    {
        try
        {
            if (gruposIds == null || gruposIds.Count == 0)
            {
                return Result.Fail("No se proporcionaron grupos.");
            }

            var grupos = new List<Grupo>();
            foreach (var grupoId in gruposIds)
            {
                var grupo = await _db.Grupos
                    .Include(g => g.Equipo)
                    .FirstOrDefaultAsync(g => g.GrupoId == grupoId);

                if (grupo == null)
                {
                    return Result.Fail($"No existe uno de los grupos seleccionados. Referencia recibida: {grupoId}.");
                }

                if (!grupo.Activo)
                {
                    _logger.LogInformation(
                        "Grupo {GrupoId} omitido en validacion de rotacion porque esta inactivo.",
                        grupo.GrupoId);
                    continue;
                }

                grupos.Add(grupo);
            }

            if (grupos.Count == 0)
            {
                return Result.Fail("No se proporcionaron grupos activos para generar horarios.");
            }

            var gruposPorId = grupos
                .GroupBy(g => g.GrupoId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var equipo = grupos.FirstOrDefault()?.Equipo;
            var equipoId = grupos.FirstOrDefault()?.EquipoId;
            if (string.IsNullOrWhiteSpace(equipoId))
            {
                return Result.Fail("No se pudo determinar el equipo de los grupos seleccionados");
            }

            var reglasGeneracion = await ConstruirReglasRotacionDesdePlanificacionAsync(equipoId);
            var personaIdsActivas = await ObtenerPersonaIdsActivasAsync();
            var gruposActivosIds = grupos.Select(g => g.GrupoId).ToArray();

            var auxiliaresEquipo = await _db.PlanificacionesAuxiliaresEquipo
                .Include(a => a.GruposPermitidos)
                .Where(a => a.EquipoId == equipoId
                    && !(a.DesdeDia == DiaConfiguracionNocturnosMes && a.HastaDia == DiaConfiguracionNocturnosMes))
                .AsNoTracking()
                .ToListAsync();
            var tiposTurno = await _db.TipoTurnos
                .AsNoTracking()
                .Where(t => t.Activo)
                .ToDictionaryAsync(t => t.TipoTurnoId, t => t);
            var planificacionesPorGrupo = new Dictionary<string, List<Planificacion>>();
            foreach (var grupoId in gruposIds)
            {
                var planificaciones = await GetByGrupoIdAsync(grupoId);
                if (!planificaciones.Any())
                {
                    return Result.Fail($"No hay planificacion configurada para el {DescribirGrupo(grupoId, gruposPorId)}.");
                }

                planificacionesPorGrupo[grupoId] = planificaciones.ToList();
            }

            var configuracionesEspeciales = ResolverConfiguracionesGruposEspeciales(planificacionesPorGrupo, gruposPorId);
            var resultadoReduccionEspeciales = AplicarReduccionesGruposEspeciales(
                planificacionesPorGrupo,
                configuracionesEspeciales,
                gruposPorId);
            if (!resultadoReduccionEspeciales.Succeeded)
            {
                return Result.Fail(resultadoReduccionEspeciales.Error ?? "Configuracion de grupos especiales invalida.");
            }

            var personaIdsGeneracion = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var grupoId in gruposActivosIds)
            {
                var personasIds = configuracionesEspeciales.TryGetValue(grupoId, out var configEspecial)
                    ? await ObtenerPersonaIdsElegiblesGrupoEspecialAsync(configEspecial, personaIdsActivas, gruposPorId)
                    : await _db.PersonaGrupos
                        .Where(pg => pg.GrupoId == grupoId && pg.EsPrincipal)
                        .Where(pg => personaIdsActivas.Contains(pg.PersonaId))
                        .Select(pg => pg.PersonaId)
                        .Distinct()
                        .ToListAsync();

                if (personasIds.Count == 0)
                {
                    if (configuracionesEspeciales.TryGetValue(grupoId, out var configSinElegibles))
                    {
                        return Result.Fail(
                            $"El {DescribirGrupo(grupoId, gruposPorId)} esta configurado para usar secundarios del {DescribirGrupo(configSinElegibles.GrupoFuenteId, gruposPorId)}, pero no hay personas activas que sean principales del grupo fuente y tengan el grupo especial como secundario.");
                    }

                    return Result.Fail($"El {DescribirGrupo(grupoId, gruposPorId)} no tiene personas activas asignadas.");
                }

                foreach (var personaId in personasIds)
                {
                    personaIdsGeneracion.Add(personaId);
                }
            }

            var gruposSeleccionados = gruposIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var auxiliaresPermitidos = reglasGeneracion.Configurables.PermiteTurnosAuxiliares;
            var auxiliaresCompartidos = auxiliaresEquipo
                .Select(auxiliar => new
                {
                    auxiliar.TipoTurnoId,
                    auxiliar.DesdeDia,
                    auxiliar.HastaDia,
                    auxiliar.MaxPorDia,
                    GruposPermitidos = auxiliar.GruposPermitidos
                        .Select(g => g.GrupoId)
                        .Where(gruposSeleccionados.Contains)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                })
                .Where(auxiliar => auxiliaresPermitidos && auxiliar.GruposPermitidos.Count > 0)
                .SelectMany(auxiliar =>
                    ExpandirRangoDias(auxiliar.DesdeDia, auxiliar.HastaDia)
                        .Select(_ => auxiliar.MaxPorDia))
                .ToList();

            int totalPlazasPorSemana = planificacionesPorGrupo.Values
                .SelectMany(p => p)
                .Where(p => !p.IsAuxiliar)
                .Sum(p => p.NumeroPersonas);

            var tipoTurnoIdsConfigurados = planificacionesPorGrupo.Values
                .SelectMany(planificaciones => planificaciones)
                .Where(planificacion => !planificacion.IsAuxiliar)
                .Select(planificacion => planificacion.TipoTurnoId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var minutosPorTurnoBase = ResolverMinutosPorTurnoBase(tiposTurno, tipoTurnoIdsConfigurados);
            var turnosObjetivoSemanales = ResolverTurnosObjetivoSemanales(reglasGeneracion.Obligatorias.MinutosObjetivoSemanales, minutosPorTurnoBase);
            var totalPersonas = personaIdsGeneracion.Count;
            int capacidadSemanalBase = totalPersonas * turnosObjetivoSemanales;
            int capacidadAuxiliarSemanal = auxiliaresCompartidos.Sum();

            if (totalPlazasPorSemana < capacidadSemanalBase)
            {
                var faltantes = capacidadSemanalBase - totalPlazasPorSemana;
                if (capacidadAuxiliarSemanal == 0)
                {
                    return Result.Fail(
                        $"La configuracion semanal del {DescribirEquipo(equipo, equipoId)} deja {faltantes} plazas sin cubrir para completar los {turnosObjetivoSemanales} turnos objetivo por persona. Configura al menos un turno auxiliar o ajusta la cobertura semanal.");
                }

                if (capacidadAuxiliarSemanal < faltantes)
                {
                    return Result.Fail(
                        $"La configuracion semanal del {DescribirEquipo(equipo, equipoId)} necesita {faltantes} plazas auxiliares, pero los turnos auxiliares configurados solo aportan {capacidadAuxiliarSemanal}. Ajusta el auxiliar o cambia la cobertura semanal.");
                }
            }
            else if (totalPlazasPorSemana > capacidadSemanalBase)
            {
                var excedente = totalPlazasPorSemana - capacidadSemanalBase;
                return Result.Fail(
                    $"La cobertura semanal excede la capacidad base del {DescribirEquipo(equipo, equipoId)} en {excedente} plazas. Ajusta la planificacion, agrega personas elegibles o configura apoyo entre grupos; el turno auxiliar no corrige este exceso obligatorio.");
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar la configuración previa de generación");
            return Result.Fail("No se pudo validar la configuración de planificación.");
        }
    }


    public async Task<Result<List<TurnoGeneradoPreview>>> GenerarTurnosRotacionGeneradorPreviewAsync(
        List<string> gruposIds,
        int numeroSemanas,
        DateTime fechaInicio,
        Action<GenerationProgressUpdate>? reportProgress = null,
        bool autorizarSobrecupoSemanalFeriado = false,
        string nivelUsoDescanso7Horas = "low",
        string nivelEvitarFinesSemanaConsecutivos = "low",
        bool balancearHorasSemanales = true)
    {
        try
        {
            if (gruposIds == null || gruposIds.Count == 0)
            {
                return Result<List<TurnoGeneradoPreview>>.Fail("No se proporcionaron grupos.");
            }

            // 1. Validar que todos los grupos existan y obtener sus datos
            var grupos = new List<Grupo>();
            foreach (var grupoId in gruposIds)
            {
                var grupo = await _db.Grupos
                    .Include(g => g.Equipo)
                    .FirstOrDefaultAsync(g => g.GrupoId == grupoId);

                if (grupo == null)
                {
                    return Result<List<TurnoGeneradoPreview>>.Fail($"No existe uno de los grupos seleccionados. Referencia recibida: {grupoId}.");
                }
                if (!grupo.Activo)
                {
                    _logger.LogInformation(
                        "Grupo {GrupoId} omitido en generacion de rotacion porque esta inactivo.",
                        grupo.GrupoId);
                    continue;
                }
                grupos.Add(grupo);
            }

            if (grupos.Count == 0)
            {
                return Result<List<TurnoGeneradoPreview>>.Fail("No se proporcionaron grupos activos para generar horarios.");
            }

            string? nombreEquipo = grupos.FirstOrDefault()?.Equipo?.NombreEquipo;
            var equipo = grupos.FirstOrDefault()?.Equipo;
            var gruposPorId = grupos
                .GroupBy(g => g.GrupoId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var equipoId = grupos.FirstOrDefault()?.EquipoId;
            var gruposActivosIds = grupos.Select(g => g.GrupoId).ToArray();

            if (string.IsNullOrWhiteSpace(equipoId))
            {
                return Result<List<TurnoGeneradoPreview>>.Fail("No se pudo determinar el equipo de los grupos seleccionados");
            }
            var personaIdsActivas = await ObtenerPersonaIdsActivasAsync();

            var auxiliaresEquipo = await _db.PlanificacionesAuxiliaresEquipo
                .Include(a => a.GruposPermitidos)
                .Where(a => a.EquipoId == equipoId
                    && !(a.DesdeDia == DiaConfiguracionNocturnosMes && a.HastaDia == DiaConfiguracionNocturnosMes))
                .AsNoTracking()
                .ToListAsync();
            var apoyosGrupo = await _db.PlanificacionesApoyoGrupo
                .Where(a => gruposIds.Contains(a.GrupoId))
                .AsNoTracking()
                .ToListAsync();
            var turnosOpcionalesVacacionGrupo = await _db.PlanificacionesTurnosOpcionalesVacacionGrupo
                .Where(a => gruposIds.Contains(a.GrupoId))
                .AsNoTracking()
                .ToListAsync();

            // 2. Obtener planificaciones de todos los grupos
            var planificacionesPorGrupo = new Dictionary<string, List<Planificacion>>();
            foreach (var grupoId in gruposActivosIds)
            {
                var planificaciones = await GetByGrupoIdAsync(grupoId);
                if (!planificaciones.Any())
                {
                    return Result<List<TurnoGeneradoPreview>>.Fail($"No hay planificacion configurada para el {DescribirGrupo(grupoId, gruposPorId)}.");
                }
                planificacionesPorGrupo[grupoId] = planificaciones.ToList();
            }
            var configuracionesEspeciales = ResolverConfiguracionesGruposEspeciales(planificacionesPorGrupo, gruposPorId);
            var resultadoReduccionEspeciales = AplicarReduccionesGruposEspeciales(
                planificacionesPorGrupo,
                configuracionesEspeciales,
                gruposPorId);
            if (!resultadoReduccionEspeciales.Succeeded)
            {
                return Result<List<TurnoGeneradoPreview>>.Fail(resultadoReduccionEspeciales.Error ?? "Configuracion de grupos especiales invalida.");
            }

            // 3. Obtener personas principales de todos los grupos
            var secundariosRaw = await _db.PersonaGrupos
                .Where(pg => gruposActivosIds.Contains(pg.GrupoId) && !pg.EsPrincipal)
                .Where(pg => personaIdsActivas.Contains(pg.PersonaId))
                .Select(pg => new { pg.PersonaId, pg.GrupoId })
                .ToListAsync();

            var secundariosPorPersonaId = secundariosRaw
                .GroupBy(x => x.PersonaId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.GrupoId).ToHashSet(StringComparer.OrdinalIgnoreCase));

            var personasPorGrupo = new Dictionary<string, List<Persona>>();
            foreach (var grupoId in gruposActivosIds)
            {
                var personas = configuracionesEspeciales.TryGetValue(grupoId, out var configEspecial)
                    ? await ObtenerPersonasElegiblesGrupoEspecialAsync(configEspecial, personaIdsActivas, gruposPorId)
                    : await _db.PersonaGrupos
                        .Where(pg => pg.GrupoId == grupoId && pg.EsPrincipal)
                        .Where(pg => personaIdsActivas.Contains(pg.PersonaId))
                        .Include(pg => pg.Persona)
                        .Where(pg => pg.Persona != null)
                        .Select(pg => pg.Persona!)
                        .ToListAsync();

                if (!personas.Any())
                {
                    if (configuracionesEspeciales.TryGetValue(grupoId, out var configSinElegibles))
                    {
                        return Result<List<TurnoGeneradoPreview>>.Fail(
                            $"El {DescribirGrupo(grupoId, gruposPorId)} esta configurado para usar secundarios del {DescribirGrupo(configSinElegibles.GrupoFuenteId, gruposPorId)}, pero no hay personas activas que sean principales del grupo fuente y tengan el grupo especial como secundario.");
                    }

                    return Result<List<TurnoGeneradoPreview>>.Fail($"El {DescribirGrupo(grupoId, gruposPorId)} no tiene personas activas asignadas.");
                }
                personasPorGrupo[grupoId] = personas;
            }

            var personaIdsGeneracion = personasPorGrupo.Values
                .SelectMany(personas => personas)
                .Select(persona => persona.PersonaId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var totalPersonas = personaIdsGeneracion.Count;
            var personaIdsActivasSet = personaIdsActivas.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var personaIdsPrincipalesSeleccionadas = await _db.PersonaGrupos
                .AsNoTracking()
                .Where(pg => gruposActivosIds.Contains(pg.GrupoId) && pg.EsPrincipal)
                .Select(pg => pg.PersonaId)
                .Distinct()
                .ToListAsync();
            var personasBorradasOmitidas = personaIdsPrincipalesSeleccionadas
                .Count(personaId => !personaIdsActivasSet.Contains(personaId));

            _logger.LogInformation(
                "Generacion RotacionV2 personas activas: seleccionadas={Seleccionadas}, activasGeneracion={Activas}, borradasOmitidas={BorradasOmitidas}",
                personaIdsPrincipalesSeleccionadas.Count,
                personaIdsGeneracion.Count,
                personasBorradasOmitidas);

            var reglasGeneracion = AplicarReglasGruposEspeciales(
                await ConstruirReglasRotacionDesdePlanificacionAsync(equipoId, balancearHorasSemanales),
                configuracionesEspeciales);
            var vacacionesPorPersona = !reglasGeneracion.Configurables.AplicarVacaciones
                ? new Dictionary<string, HashSet<DateOnly>>(StringComparer.OrdinalIgnoreCase)
                : await ObtenerVacacionesAprobadasPorPersonaAsync(
                    personaIdsGeneracion,
                    DateOnly.FromDateTime(fechaInicio.Date),
                    DateOnly.FromDateTime(fechaInicio.Date.AddDays((numeroSemanas * 7) - 1)));
            var turnosObjetivoSemanales = ResolverTurnosObjetivoSemanales(reglasGeneracion.Obligatorias.MinutosObjetivoSemanales, 8 * 60);
            var capacidadSemanalBase = totalPersonas * turnosObjetivoSemanales;

            var gruposSeleccionados = gruposActivosIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var auxiliaresPermitidos = reglasGeneracion.Configurables.PermiteTurnosAuxiliares;
            var auxiliaresCompartidos = auxiliaresEquipo
                .Select(auxiliar => new
                {
                    auxiliar.TipoTurnoId,
                    auxiliar.DesdeDia,
                    auxiliar.HastaDia,
                    auxiliar.MaxPorDia,
                    GruposPermitidos = auxiliar.GruposPermitidos
                        .Select(g => g.GrupoId)
                        .Where(gruposSeleccionados.Contains)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                })
                .Where(auxiliar => auxiliaresPermitidos && auxiliar.GruposPermitidos.Count > 0)
                .SelectMany(auxiliar =>
                    ExpandirRangoDias(auxiliar.DesdeDia, auxiliar.HastaDia)
                        .Select(dia => new PlanificacionAuxiliarCompartida
                        {
                            SharedKey = $"{auxiliar.TipoTurnoId}|{NormalizarNombreDia(dia)}",
                            Dia = NormalizarNombreDia(dia),
                            TipoHorario = auxiliar.TipoTurnoId,
                            Cantidad = auxiliar.MaxPorDia,
                            GruposPermitidos = new HashSet<string>(auxiliar.GruposPermitidos, StringComparer.OrdinalIgnoreCase)
                        }))
                .ToList();

            // 4. Validar factibilidad basica
            int totalPlazasPorSemana = planificacionesPorGrupo.Values
                .SelectMany(p => p)
                .Where(p => !p.IsAuxiliar)
                .Sum(p => p.NumeroPersonas);
            int capacidadAuxiliarSemanal = auxiliaresCompartidos.Sum(a => a.Cantidad);

            if (totalPlazasPorSemana != capacidadSemanalBase)
            {
                _logger.LogWarning("ADVERTENCIA: Plazas ({TotalPlazas}) != Capacidad ({Capacidad})",
                    totalPlazasPorSemana, capacidadSemanalBase);
            }

            if (totalPlazasPorSemana < capacidadSemanalBase)
            {
                var faltantes = capacidadSemanalBase - totalPlazasPorSemana;
                if (auxiliaresCompartidos.Count == 0)
                {
                    return Result<List<TurnoGeneradoPreview>>.Fail(
                        $"La configuracion semanal del {DescribirEquipo(equipo, equipoId)} deja {faltantes} plazas sin cubrir para completar los {turnosObjetivoSemanales} turnos objetivo por persona. Configura al menos un turno auxiliar o ajusta la cobertura semanal.");
                }

                if (capacidadAuxiliarSemanal < faltantes)
                {
                    return Result<List<TurnoGeneradoPreview>>.Fail(
                        $"La configuracion semanal del {DescribirEquipo(equipo, equipoId)} necesita {faltantes} plazas auxiliares, pero los turnos auxiliares configurados solo aportan {capacidadAuxiliarSemanal}. Ajusta el auxiliar o cambia la cobertura semanal.");
                }
            }
            else if (totalPlazasPorSemana > capacidadSemanalBase)
            {
                var excedente = totalPlazasPorSemana - capacidadSemanalBase;
                return Result<List<TurnoGeneradoPreview>>.Fail(
                    $"La cobertura semanal excede la capacidad base del {DescribirEquipo(equipo, equipoId)} en {excedente} plazas. Ajusta la planificacion, agrega personas elegibles o configura apoyo entre grupos; el turno auxiliar no corrige este exceso obligatorio.");
            }

            // 5. Obtener tipos de turno activos
            var tiposTurno = await _db.TipoTurnos
                .AsNoTracking()
                .Where(t => t.Activo)
                .ToDictionaryAsync(t => t.TipoTurnoId, t => t);

            // 6. Construir estructura de entrada para RotacionV2 con TODOS los grupos
            var gruposEquipo = new List<GrupoEquipo>();
            int numeroPersonaGlobal = 1;

            foreach (var grupoId in gruposIds)
            {
                var grupo = grupos.First(g => g.GrupoId == grupoId);
                var personas = personasPorGrupo[grupoId];
                var planificaciones = planificacionesPorGrupo[grupoId];
                var planificacionesNormales = planificaciones
                    .Where(p => !p.IsAuxiliar)
                    .ToList();
                var apoyoPorCelda = apoyosGrupo
                    .Where(a => a.GrupoId == grupoId)
                    .GroupBy(a => $"{NormalizarNombreDia(a.Dia)}|{a.TipoTurnoId}", StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Last().CantidadApoyo,
                        StringComparer.OrdinalIgnoreCase);
                var opcionalVacacionPorCelda = turnosOpcionalesVacacionGrupo
                    .Where(a => a.GrupoId == grupoId)
                    .GroupBy(a => $"{NormalizarNombreDia(a.Dia)}|{a.TipoTurnoId}", StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        _ => true,
                        StringComparer.OrdinalIgnoreCase);

                gruposEquipo.Add(new GrupoEquipo
                {
                    GrupoId = grupoId,
                    Personas = personas.Select(p => new PersonaTurno
                    {
                        PersonaId = p.PersonaId,
                        Nombre = p.Nombre,
                        Numero = numeroPersonaGlobal++,
                        Grupo = configuracionesEspeciales.TryGetValue(grupoId, out var configEspecialGrupo) &&
                                gruposPorId.TryGetValue(configEspecialGrupo.GrupoFuenteId, out var grupoFuente)
                            ? grupoFuente.NombreGrupo
                            : grupo.NombreGrupo,
                        GrupoId = configuracionesEspeciales.TryGetValue(grupoId, out var configEspecialPersona)
                            ? configEspecialPersona.GrupoFuenteId
                            : grupoId,
                        GruposSecundarios = secundariosPorPersonaId.TryGetValue(p.PersonaId, out var secundarios)
                            ? new HashSet<string>(secundarios, StringComparer.OrdinalIgnoreCase)
                            : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    }).ToList(),
                    PlanificacionSemanal = planificacionesNormales.Select(p => new PlanificacionTurno
                    {
                        Dia = NormalizarNombreDia(p.Dia),
                        TipoHorario = p.TipoTurnoId,
                        Cantidad = p.NumeroPersonas,
                        CantidadApoyo = apoyoPorCelda.TryGetValue($"{NormalizarNombreDia(p.Dia)}|{p.TipoTurnoId}", out var cantidadApoyo)
                            ? cantidadApoyo
                            : 0,
                        PuedeOmitirsePorVacacion = opcionalVacacionPorCelda.ContainsKey($"{NormalizarNombreDia(p.Dia)}|{p.TipoTurnoId}")
                    }).ToList()
                });
            }

            var equipoPlantilla = new EquipoPlantilla
            {
                Nombre = nombreEquipo ?? "Equipo",
                Grupos = gruposEquipo,
                PlanificacionAuxiliarCompartidaSemanal = auxiliaresCompartidos
            };

            var turnosNormales = new List<Turno>();
            var turnosAuxiliares = new List<Turno>();
            var numeroTurnoGlobal = 1;

            foreach (var grupo in equipoPlantilla.Grupos)
            {
                var planificacionOrdenada = grupo.PlanificacionSemanal
                    .OrderBy(p => ObtenerIndiceDiaSemana(p.Dia))
                    .ThenBy(p => tiposTurno.TryGetValue(p.TipoHorario, out var tipo) ? tipo.HoraInicio : TimeOnly.MinValue)
                    .ToList();

                foreach (var planificacion in planificacionOrdenada)
                {
                    if (!tiposTurno.TryGetValue(planificacion.TipoHorario, out var tipoTurno))
                    {
                        _logger.LogWarning("Tipo de turno {TipoTurnoId} no encontrado para generación RotacionV2", planificacion.TipoHorario);
                        continue;
                    }

                    var fechaBase = fechaInicio.Date.AddDays(ObtenerIndiceDiaSemana(planificacion.Dia));
                    var inicio = fechaBase.Add(tipoTurno.HoraInicio.ToTimeSpan());
                    var fin = fechaBase.Add(tipoTurno.HoraFin.ToTimeSpan());
                    if (fin <= inicio)
                    {
                        fin = fin.AddDays(1);
                    }

                    turnosNormales.Add(new Turno
                    {
                        NumeroTurno = numeroTurnoGlobal++,
                        GrupoId = grupo.GrupoId,
                        Dia = NormalizarNombreDia(planificacion.Dia),
                        TipoHorario = planificacion.TipoHorario,
                        TipoTurnoId = planificacion.TipoHorario,
                        Inicio = inicio,
                        Fin = fin,
                        MinimoPersTurno = planificacion.Cantidad,
                        CapacidadPlanificada = planificacion.Cantidad,
                        MaximoApoyoCedible = Math.Min(planificacion.Cantidad, Math.Max(0, planificacion.CantidadApoyo)),
                        IsAuxiliar = false,
                        PuedeOmitirsePorVacacion = planificacion.PuedeOmitirsePorVacacion,
                        MinutosTrabajoComputables = CalcularMinutosTrabajoComputables(tipoTurno.HoraInicio, tipoTurno.HoraFin),
                        PersonaTurnoTurno = new List<PersonaTurno>()
                    });
                }

                var auxiliaresGrupo = equipoPlantilla.PlanificacionAuxiliarCompartidaSemanal
                    .Where(aux => aux.GruposPermitidos.Contains(grupo.GrupoId))
                    .OrderBy(aux => ObtenerIndiceDiaSemana(aux.Dia))
                    .ThenBy(aux => tiposTurno.TryGetValue(aux.TipoHorario, out var tipo) ? tipo.HoraInicio : TimeOnly.MinValue)
                    .ToList();

                foreach (var auxiliar in auxiliaresGrupo)
                {
                    if (!tiposTurno.TryGetValue(auxiliar.TipoHorario, out var tipoTurnoAux))
                    {
                        _logger.LogWarning("Tipo de turno auxiliar {TipoTurnoId} no encontrado para generación RotacionV2", auxiliar.TipoHorario);
                        continue;
                    }

                    var fechaBaseAux = fechaInicio.Date.AddDays(ObtenerIndiceDiaSemana(auxiliar.Dia));
                    var inicioAux = fechaBaseAux.Add(tipoTurnoAux.HoraInicio.ToTimeSpan());
                    var finAux = fechaBaseAux.Add(tipoTurnoAux.HoraFin.ToTimeSpan());
                    if (finAux <= inicioAux)
                    {
                        finAux = finAux.AddDays(1);
                    }

                    turnosAuxiliares.Add(new Turno
                    {
                        NumeroTurno = numeroTurnoGlobal++,
                        GrupoId = grupo.GrupoId,
                        Dia = NormalizarNombreDia(auxiliar.Dia),
                        TipoHorario = auxiliar.TipoHorario,
                        TipoTurnoId = auxiliar.TipoHorario,
                        Inicio = inicioAux,
                        Fin = finAux,
                        MinimoPersTurno = auxiliar.Cantidad,
                        CapacidadPlanificada = auxiliar.Cantidad,
                        MaximoApoyoCedible = 0,
                        IsAuxiliar = true,
                        PuedeOmitirsePorVacacion = false,
                        AuxiliarSharedKey = auxiliar.SharedKey,
                        AuxiliarMaxCompartido = auxiliar.Cantidad,
                        MinutosTrabajoComputables = CalcularMinutosTrabajoComputables(tipoTurnoAux.HoraInicio, tipoTurnoAux.HoraFin),
                        PersonaTurnoTurno = new List<PersonaTurno>()
                    });
                }
            }

            var personasUnificadas = equipoPlantilla.Grupos
                .SelectMany(grupo => grupo.Personas)
                .GroupBy(persona => persona.PersonaId, StringComparer.OrdinalIgnoreCase)
                .Select(grupoPersonas =>
                {
                    var basePersona = grupoPersonas.First();
                    var secundarios = grupoPersonas
                        .SelectMany(persona => persona.GruposSecundarios)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    return new PersonaTurno
                    {
                        PersonaId = basePersona.PersonaId,
                        Nombre = basePersona.Nombre,
                        Numero = basePersona.Numero,
                        Grupo = basePersona.Grupo,
                        GrupoId = basePersona.GrupoId,
                        GruposSecundarios = secundarios
                    };
                })
                .OrderBy(persona => persona.Numero)
                .ToList();

            personasUnificadas = personasUnificadas
                .Where(persona => personaIdsActivasSet.Contains(persona.PersonaId))
                .ToList();

            if (personasUnificadas.Count == 0 || turnosNormales.Count == 0)
            {
                return Result<List<TurnoGeneradoPreview>>.Fail("No se pudo construir la entrada de RotacionV2 para la generación.");
            }

            var plantillaV2 = new Plantilla
            {
                Nombre = equipoPlantilla.Nombre,
                GrupoId = equipoId,
                PersonaTurno = personasUnificadas,
                Turnos = turnosNormales,
                TurnosAuxiliares = turnosAuxiliares
            };

            var minutosPorTurnoBase = ResolverMinutosPorTurnoBase(
                tiposTurno,
                turnosNormales.Select(turno => turno.TipoTurnoId));
            turnosObjetivoSemanales = ResolverTurnosObjetivoSemanales(reglasGeneracion.Obligatorias.MinutosObjetivoSemanales, minutosPorTurnoBase);
            capacidadSemanalBase = totalPersonas * turnosObjetivoSemanales;

            if (totalPlazasPorSemana != capacidadSemanalBase)
            {
                _logger.LogWarning("ADVERTENCIA: Plazas ({TotalPlazas}) != Capacidad ({Capacidad})",
                    totalPlazasPorSemana, capacidadSemanalBase);
            }

            if (totalPlazasPorSemana < capacidadSemanalBase)
            {
                var faltantes = capacidadSemanalBase - totalPlazasPorSemana;
                if (auxiliaresCompartidos.Count == 0)
                {
                    return Result<List<TurnoGeneradoPreview>>.Fail(
                        $"La configuracion semanal del {DescribirEquipo(equipo, equipoId)} deja {faltantes} plazas sin cubrir para completar los {turnosObjetivoSemanales} turnos objetivo por persona. Configura al menos un turno auxiliar o ajusta la cobertura semanal.");
                }

                if (capacidadAuxiliarSemanal < faltantes)
                {
                    return Result<List<TurnoGeneradoPreview>>.Fail(
                        $"La configuracion semanal del {DescribirEquipo(equipo, equipoId)} necesita {faltantes} plazas auxiliares, pero los turnos auxiliares configurados solo aportan {capacidadAuxiliarSemanal}. Ajusta el auxiliar o cambia la cobertura semanal.");
                }
            }
            else if (totalPlazasPorSemana > capacidadSemanalBase)
            {
                var excedente = totalPlazasPorSemana - capacidadSemanalBase;
                return Result<List<TurnoGeneradoPreview>>.Fail(
                    $"La cobertura semanal excede la capacidad base del {DescribirEquipo(equipo, equipoId)} en {excedente} plazas. Ajusta la planificacion, agrega personas elegibles o configura apoyo entre grupos; el turno auxiliar no corrige este exceso obligatorio.");
            }

            var feriadosRango = await ObtenerFeriadosEnRangoAsync(
                DateOnly.FromDateTime(fechaInicio.Date),
                DateOnly.FromDateTime(fechaInicio.Date.AddDays((numeroSemanas * 7) - 1)));

            reportProgress?.Invoke(new GenerationProgressUpdate(0, numeroSemanas, "Generando turnos"));

            var opcionesSolver = new OpcionesSolverRotacion
            {
                TiempoMaximoResolucion = TimeSpan.FromSeconds(60),
                CantidadWorkers = Math.Max(1, Environment.ProcessorCount),
                NivelUsoDescanso7Horas = ParseNivelUsoDescanso7Horas(nivelUsoDescanso7Horas),
                NivelEvitarFinesSemanaConsecutivos = ParseNivelEvitarFinesSemanaConsecutivos(nivelEvitarFinesSemanaConsecutivos),
                AutorizarSobrecupoSemanalEnFeriado = autorizarSobrecupoSemanalFeriado,
                ReportarDiagnostico = mensaje => _logger.LogInformation("RotacionV2 diag: {Mensaje}", mensaje)
            };

              _logger.LogInformation(
                "RotacionV2 reglas aplicadas desde planificacion vigente. objetivoMin={ObjetivoMin} descansoMin={DescansoMin} minDescansoConsecutivoDias={MinDiasDescanso} maxFdsConsecutivos={MaxFdsConsecutivos} maxNocturnosMes={MaxNocturnosMes} maxNocturnosSemana={MaxNocturnosSemana} maxSlotsFdsMes={MaxSlotsFdsMes}",
                  reglasGeneracion.Obligatorias.MinutosObjetivoSemanales,
                  reglasGeneracion.Obligatorias.MinutosMinimosDescansoEntreTurnos,
                  reglasGeneracion.Obligatorias.MinimoDiasDescansoConsecutivosPorSemana,
                  reglasGeneracion.Configurables.MaximoFinesSemanaConsecutivos,
                reglasGeneracion.Configurables.MaximoTurnosNocturnosPorMes,
                reglasGeneracion.Configurables.MaximoTurnosNocturnosPorSemana,
                reglasGeneracion.Configurables.MaximoSlotsFinSemanaPorMes);

            var configuracionFeriado = await ConstruirConfiguracionVisibilidadFeriadoAsync(equipoId);
            var excepcionesRotacion = await _db.ExcepcionesTurnoPersonas
                .AsNoTracking()
                .Include(e => e.Persona)
                .Where(e => e.Persona != null && e.Persona.EquipoId == equipoId && !e.Persona.Borrado)
                .Select(e => new ExcepcionTurno
                {
                    EmpleadoId = e.PersonaId,
                    TipoTurnoId = e.TipoTurnoId,
                    Motivo = e.MotivoExcepcion,
                    FechaInicio = e.FechaInicio,
                    FechaFin = e.FechaFin
                })
                .ToListAsync();

            var problema = _rotacionV2.CrearProblema(
                plantillaV2,
                numeroSemanas,
                fechaInicio,
                vacacionesPorPersona,
                feriadosRango,
                reglasGeneracion,
                excepciones: excepcionesRotacion);
            problema = AplicarCoberturaReducidaFeriados(problema, configuracionFeriado);
            var estadoSemanalInicial = await ConstruirEstadoSemanalInicialRotacionAsync(
                personaIdsGeneracion,
                DateOnly.FromDateTime(fechaInicio.Date),
                tiposTurno);

            if (!autorizarSobrecupoSemanalFeriado &&
                TryConstruirSolicitudSobrecupoFeriado(problema, out var solicitudSobrecupoFeriado))
            {
                return Result<List<TurnoGeneradoPreview>>.Fail(
                    HolidayOvertimeApprovalRequiredPrefix + solicitudSobrecupoFeriado);
            }

            if (DiagnosticarFactibilidadEstructural(problema, gruposPorId, tiposTurno, out var detalleFactibilidad) is false)
            {
                _logger.LogWarning("RotacionV2 factibilidad estructural fallida. {Detalle}", detalleFactibilidad);
                return Result<List<TurnoGeneradoPreview>>.Fail($"Configuración no factible para RotacionV2: {detalleFactibilidad}");
            }

            var solucion = _rotacionV2.ResolverProblema(problema, opcionesSolver, estadoSemanalInicial);

            if (solucion.Estado is not EstadoSolucionRotacion.Optima and not EstadoSolucionRotacion.Factible)
            {
                _logger.LogWarning(
                    "RotacionV2 infactible/error. estado={Estado} detalle={Detalle}",
                    solucion.Estado,
                    solucion.DetalleEstado);

                return Result<List<TurnoGeneradoPreview>>.Fail(
                    $"RotacionV2 no pudo resolver la generación ({solucion.Estado}). Detalle: {solucion.DetalleEstado}");
            }

            var equidadRecargos = AnalizadorEquidadRecargosProxy.Calcular(problema, solucion);
            _logger.LogInformation(
                "RotacionV2 equidad recargos proxy ponderado. empleados={Empleados} promedio={Promedio:P2} desviacion={Desviacion:P2} cv={Cv:0.##}% clasificacion={Clasificacion}",
                equidadRecargos.EmpleadosConsiderados,
                equidadRecargos.PromedioIndiceRecargoPorHoraProxy,
                equidadRecargos.DesviacionEstandarIndiceRecargoPorHoraProxy,
                equidadRecargos.CoeficienteVariacionPorcentaje,
                equidadRecargos.ClasificacionCv);

            var slotPorId = problema.Slots.ToDictionary(slot => slot.Id);
            var visibilidadFeriado = HelperVisibilidadFeriados.Calcular(problema, solucion, configuracionFeriado);
            var preview = solucion.Asignaciones
                .Where(asignacion => slotPorId.ContainsKey(asignacion.IdSlot))
                .Select(asignacion =>
                {
                    var slot = slotPorId[asignacion.IdSlot];
                    var esFeriado = feriadosRango.Contains(slot.Fecha);
                    return new TurnoGeneradoPreview(
                        asignacion.EmpleadoId,
                        slot.TipoTurnoId,
                        slot.GrupoId,
                        slot.Fecha,
                        EsFeriado: esFeriado,
                        NoLaboradoPorFeriado: esFeriado && !visibilidadFeriado.DebeMostrar(slot.Id, asignacion.EmpleadoId));
                })
                .ToList();

            reportProgress?.Invoke(new GenerationProgressUpdate(numeroSemanas, numeroSemanas, "Generación RotacionV2 completada."));
            return Result<List<TurnoGeneradoPreview>>.Ok(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar previsualizacion de turnos");
            return Result<List<TurnoGeneradoPreview>>.Fail($"Error al generar la previsualizacion: {ex.Message}");
        }
    }

    private async Task<EstadoResolucionSemanal?> ConstruirEstadoSemanalInicialRotacionAsync(
        IReadOnlyCollection<string> personaIds,
        DateOnly fechaCorte,
        IReadOnlyDictionary<string, TipoTurno> tiposTurno)
    {
        if (personaIds.Count == 0 || tiposTurno.Count == 0)
        {
            return null;
        }

        var historialPrevio = await _db.RegistroTurnos
            .AsNoTracking()
            .Where(rt => personaIds.Contains(rt.PersonaId)
                && rt.FechaTurno < fechaCorte
                && !rt.NoLaboradoPorFeriado)
            .Select(rt => new
            {
                rt.PersonaId,
                rt.FechaTurno,
                rt.TipoTurnoId
            })
            .ToListAsync();

        if (historialPrevio.Count == 0)
        {
            return null;
        }

        var estado = new EstadoResolucionSemanal();

        foreach (var grupoPersona in historialPrevio.GroupBy(item => item.PersonaId, StringComparer.OrdinalIgnoreCase))
        {
            DateTime? ultimoFin = null;

            foreach (var registro in grupoPersona)
            {
                if (!tiposTurno.TryGetValue(registro.TipoTurnoId, out var tipoTurno))
                {
                    continue;
                }

                var fechaFin = tipoTurno.HoraFin <= tipoTurno.HoraInicio
                    ? registro.FechaTurno.AddDays(1)
                    : registro.FechaTurno;
                var fin = fechaFin.ToDateTime(tipoTurno.HoraFin);

                if (ultimoFin is null || fin > ultimoFin.Value)
                {
                    ultimoFin = fin;
                }
            }

            if (ultimoFin.HasValue)
            {
                estado.UltimoFinTurnoPorEmpleado[grupoPersona.Key] = ultimoFin.Value;
            }
        }

        return estado.UltimoFinTurnoPorEmpleado.Count > 0 ? estado : null;
    }


    public Task<Result<List<TurnoGeneradoPreview>>> GenerarTurnosBlueprintPreviewAsync(List<string> gruposIds, int numeroSemanas, DateTime fechaInicio, Action<GenerationProgressUpdate>? reportProgress = null)
    {
        _logger.LogWarning("Blueprint legacy solicitado pero esta deshabilitado. Usa RotacionV2 basada en cobertura.");
        return Task.FromResult(Result<List<TurnoGeneradoPreview>>.Fail("Blueprint legacy deshabilitado. Usa RotacionV2 basada en cobertura."));
    }
#if false
        try
        {
            if (gruposIds == null || gruposIds.Count == 0)
            {
                return Result<List<TurnoGeneradoPreview>>.Fail("No se proporcionaron grupos.");
            }

            // 1. Validar que todos los grupos existan y obtener sus datos
            var grupos = new List<Grupo>();
            foreach (var grupoId in gruposIds)
            {
                var grupo = await _db.Grupos
                    .Include(g => g.Equipo)
                    .FirstOrDefaultAsync(g => g.GrupoId == grupoId);

                if (grupo == null)
                {
                    return Result<List<TurnoGeneradoPreview>>.Fail($"El grupo con ID {grupoId} no existe");
                }
                grupos.Add(grupo);
            }

            string? nombreEquipo = grupos.FirstOrDefault()?.Equipo?.NombreEquipo;

            // 2. Obtener planificaciones blueprint de todos los grupos
            var blueprintsPorGrupo = new Dictionary<string, List<PlanificacionBlueprint>>();
            foreach (var grupoId in gruposIds)
            {
                var blueprints = await _db.PlanificacionBlueprints
                    .Where(p => p.GrupoId == grupoId)
                    .AsNoTracking()
                    .OrderBy(p => p.Dia)
                    .ThenBy(p => p.TipoTurnoId)
                    .ToListAsync();

                if (!blueprints.Any())
                {
                    return Result<List<TurnoGeneradoPreview>>.Fail($"No hay blueprint configurado para el grupo {grupoId}");
                }
                blueprintsPorGrupo[grupoId] = blueprints;
            }

            // Identificar grupos que usan grupos secundarios y ordenar el procesamiento
            var gruposConUsaSecundarios = new HashSet<string>(
                gruposIds.Where(gid => blueprintsPorGrupo[gid].Any(b => b.UsaGruposSecundarios)),
                StringComparer.OrdinalIgnoreCase);

            // Ordenar: grupos con usa_grupos_secundarios primero (cobertura especial),
            // luego por cantidad de blueprints ascendente (menor cobertura se asigna antes)
            var gruposOrdenados = gruposIds
                .OrderByDescending(gid => gruposConUsaSecundarios.Contains(gid) ? 1 : 0)
                .ThenBy(gid => blueprintsPorGrupo[gid].Count)
                .ToList();

            // 3. Obtener personas principales de todos los grupos
            var personasPorGrupo = new Dictionary<string, List<Persona>>();
            foreach (var grupoId in gruposIds)
            {
                var personas = await _db.PersonaGrupos
                    .Where(pg => pg.GrupoId == grupoId && pg.EsPrincipal)
                    .Include(pg => pg.Persona)
                    .Where(pg => pg.Persona != null && !pg.Persona.Borrado)
                    .Select(pg => pg.Persona!)
                    .ToListAsync();

                // Los grupos con usa_grupos_secundarios obtienen sus personas desde otros grupos;
                // no se requiere que tengan miembros primarios propios
                if (!personas.Any() && !gruposConUsaSecundarios.Contains(grupoId))
                {
                    string nombreGrupo = grupos.First(g => g.GrupoId == grupoId).NombreGrupo;
                    return Result<List<TurnoGeneradoPreview>>.Fail($"El grupo {nombreGrupo} no tiene personas asignadas");
                }
                personasPorGrupo[grupoId] = personas;
            }

            // Cargar membresías de grupos secundarios de todas las personas en los grupos seleccionados
            var secundariosRawBp = await _db.PersonaGrupos
                .Where(pg => gruposIds.Contains(pg.GrupoId) && !pg.EsPrincipal)
                .Where(pg => pg.Persona != null && !pg.Persona.Borrado)
                .Select(pg => new { pg.PersonaId, pg.GrupoId })
                .ToListAsync();

            var secundariosPorPersonaIdBp = secundariosRawBp
                .GroupBy(x => x.PersonaId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.GrupoId).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            // Mapa plano personaId → Persona (para búsqueda rápida de candidatos secundarios)
            var todasPersonasEquipoBp = personasPorGrupo.Values
                .SelectMany(p => p)
                .GroupBy(p => p.PersonaId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // 4. Obtener tipos de turno con horarios
            var tiposTurno = await _db.TipoTurnos
                .Where(t => t.Activo)
                .ToDictionaryAsync(t => t.TipoTurnoId, t => t);

            // 5. Construir estructura para el algoritmo Blueprint
            var gruposEquipo = new List<GrupoEquipo>();
            var patronesPorGrupo = new Dictionary<string, List<PatronTurnos>>(StringComparer.OrdinalIgnoreCase);
            int numeroPersonaGlobal = 1;

            // Procesar en orden: grupos con usa_grupos_secundarios primero,
            // luego grupos regulares. Ambos ordenados por cantidad de blueprints ascendente.
            foreach (var grupoId in gruposOrdenados)
            {
                var grupo = grupos.First(g => g.GrupoId == grupoId);
                var blueprints = blueprintsPorGrupo[grupoId];

                // Convertir blueprints a patrones
                var patronesTurnos = ConstruirPatronesDesdeBlueprints(blueprints);
                patronesPorGrupo[grupoId] = patronesTurnos;

                List<PersonaTurno> personasGrupo;

                if (gruposConUsaSecundarios.Contains(grupoId))
                {
                    // Preferir miembros primarios del grupo y usar secundarios solo como fallback.
                    var primarios = personasPorGrupo.ContainsKey(grupoId)
                        ? personasPorGrupo[grupoId]
                        : new List<Persona>();

                    var candidatos = todasPersonasEquipoBp.Values
                        .Where(p => secundariosPorPersonaIdBp.TryGetValue(p.PersonaId, out var secs)
                                    && secs.Contains(grupoId, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    // Construir lista única: primarios primero, luego secundarios que no son primarios.
                    var personasUnicas = new List<Persona>();
                    var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var p in primarios)
                    {
                        personasUnicas.Add(p);
                        vistos.Add(p.PersonaId);
                    }

                    foreach (var p in candidatos)
                    {
                        if (vistos.Add(p.PersonaId))
                        {
                            personasUnicas.Add(p);
                        }
                    }

                    if (!personasUnicas.Any())
                    {
                        _logger.LogWarning(
                            "Grupo usa_grupos_secundarios=true ({GrupoId}) sin candidatos disponibles. " +
                            "Ninguna persona del equipo tiene este grupo como secundario ni primarios activos.", grupoId);
                    }

                    personasGrupo = personasUnicas.Select(p => new PersonaTurno
                    {
                        PersonaId = p.PersonaId,
                        Nombre = p.Nombre,
                        Numero = numeroPersonaGlobal++,
                        Grupo = grupo.NombreGrupo,
                        GrupoId = grupoId,
                        GruposSecundarios = secundariosPorPersonaIdBp.TryGetValue(p.PersonaId, out var secsG)
                            ? new HashSet<string>(secsG, StringComparer.OrdinalIgnoreCase)
                            : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    }).ToList();
                }
                    if (gruposConUsaSecundarios.Contains(grupoId))
                    {
                        // Preferir miembros primarios del grupo y usar secundarios solo como fallback.
                        var primarios = personasPorGrupo.ContainsKey(grupoId)
                            ? personasPorGrupo[grupoId]
                            : new List<Persona>();

                        var candidatos = todasPersonasEquipoBp.Values
                            .Where(p => secundariosPorPersonaIdBp.TryGetValue(p.PersonaId, out var secs)
                                        && secs.Contains(grupoId, StringComparer.OrdinalIgnoreCase))
                            .ToList();

                        // Construir lista única: primarios primero, luego secundarios que no son primarios.
                        var personasUnicas = new List<Persona>();
                        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (var p in primarios)
                        {
                            personasUnicas.Add(p);
                            vistos.Add(p.PersonaId);
                        }

                        foreach (var p in candidatos)
                        {
                            if (vistos.Add(p.PersonaId))
                            {
                                personasUnicas.Add(p);
                            }
                        }

                        if (!personasUnicas.Any())
                        {
                            _logger.LogWarning(
                                "Grupo usa_grupos_secundarios=true ({GrupoId}) sin candidatos disponibles. " +
                                "Ninguna persona del equipo tiene este grupo como secundario ni primarios activos.", grupoId);
                        }

                        personasGrupo = personasUnicas.Select(p => new PersonaTurno
                        {
                            PersonaId = p.PersonaId,
                            Nombre = p.Nombre,
                            Numero = numeroPersonaGlobal++,
                            Grupo = grupo.NombreGrupo,
                            GrupoId = grupoId,
                            GruposSecundarios = secundariosPorPersonaIdBp.TryGetValue(p.PersonaId, out var secsG)
                                ? new HashSet<string>(secsG, StringComparer.OrdinalIgnoreCase)
                                : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        }).ToList();
                    }
                else
                {
                    // Grupo regular: todos los miembros primarios.
                    // La exclusión dinámica por semana se gestiona dentro del algoritmo.
                    personasGrupo = personasPorGrupo[grupoId]
                        .Select(p => new PersonaTurno
                        {
                            PersonaId = p.PersonaId,
                            Nombre = p.Nombre,
                            Numero = numeroPersonaGlobal++,
                            Grupo = grupo.NombreGrupo,
                            GrupoId = grupoId,
                            GruposSecundarios = secundariosPorPersonaIdBp.TryGetValue(p.PersonaId, out var secsR)
                                ? new HashSet<string>(secsR, StringComparer.OrdinalIgnoreCase)
                                : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        }).ToList();
                }

                gruposEquipo.Add(new GrupoEquipo
                {
                    GrupoId = grupoId,
                    Personas = personasGrupo,
                    PlanificacionSemanal = new List<PlanificacionTurno>(), // No usado en Blueprint
                    PatronDeTurnos = patronesTurnos,
                    UsaGruposSecundarios = gruposConUsaSecundarios.Contains(grupoId)
                });
            }

            var equipoPlantilla = new EquipoPlantilla
            {
                Nombre = nombreEquipo ?? "Equipo",
                Grupos = gruposEquipo
            };

            // 5.1 Obtener los registros de turnos del equipo de las últimas N semanas, donde N es el número de personas por grupo (para obtener un historial suficiente para continuar la rotación)
            //  separado por semanas, cada semana en grupos y cada grupo con sus personas y turnos asignados. Esto se usará para alimentar el algoritmo Blueprint y que pueda continuar la rotación de manera coherente. 
            var historialBlueprintSemanal = await ObtenerHistorialBlueprintPorSemanasAsync(
                gruposIds,
                personasPorGrupo,
                fechaInicio);

            _logger.LogInformation(
                "Historial Blueprint cargado: semanas={Semanas}, grupos={Grupos}, personas={Personas}, turnos={Turnos}",
                historialBlueprintSemanal.Count,
                historialBlueprintSemanal
                    .SelectMany(s => s.Grupos)
                    .Select(g => g.GrupoId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                historialBlueprintSemanal
                    .SelectMany(s => s.Grupos)
                    .SelectMany(g => g.Personas)
                    .Count(),
                historialBlueprintSemanal
                    .SelectMany(s => s.Grupos)
                    .SelectMany(g => g.Personas)
                    .Sum(p => p.Turnos.Count));

            var asignacionesPatronPorNSemanas = AnalizarPatronesPorNSemanas(
                historialBlueprintSemanal,
                patronesPorGrupo,
                0.80);

            foreach (var grupoAsignacion in asignacionesPatronPorNSemanas.OrderBy(x => x.Key))
            {
                foreach (var personaAsignacion in grupoAsignacion.Value.OrderBy(x => x.PersonaId))
                {
                    foreach (var patronSemana in personaAsignacion.PatronesPorSemana
                        .OrderBy(p => p.SemanaInicio))
                    {
                        _logger.LogInformation(
                            "Patron historial grupo={GrupoId} persona={PersonaId} nombre={NombrePersona} semana={SemanaInicio} patron={Patron} similitud={Similitud:P0}",
                            grupoAsignacion.Key,
                            personaAsignacion.PersonaId,
                            personaAsignacion.NombrePersona,
                            patronSemana.SemanaInicio,
                            patronSemana.PatronNombre ?? "null",
                            patronSemana.Similitud);
                    }
                }
            }

            var historialPatronParaAlgoritmo = ConvertirHistorialPatronesParaAlgoritmo(asignacionesPatronPorNSemanas);
            foreach (var grupoEquipoActual in gruposEquipo)
            {
                if (historialPatronParaAlgoritmo.TryGetValue(grupoEquipoActual.GrupoId, out var historialGrupo))
                {
                    grupoEquipoActual.HistorialPatronesPorPersona = historialGrupo;
                }
            }

            // Enriquecer HistorialPatronSemana con las horas REALES del último turno de la semana anterior.
            // Esto es necesario porque puede haber cambios manuales en RegistroTurnos que difieren del patrón
            // (ej: domingo tarde → noche), y la validación de descanso debe usar esas horas reales.
            var inicioSemanaAnteriorRef = ObtenerInicioSemana(DateOnly.FromDateTime(fechaInicio.Date)).AddDays(-7);
            var semanaAnteriorHistorial = historialBlueprintSemanal
                .FirstOrDefault(s => s.SemanaInicio == inicioSemanaAnteriorRef);

            if (semanaAnteriorHistorial != null)
            {
                foreach (var grupoEquipoActual in gruposEquipo)
                {
                    if (grupoEquipoActual.HistorialPatronesPorPersona == null) continue;

                    var grupoSemana = semanaAnteriorHistorial.Grupos
                        .FirstOrDefault(g => string.Equals(g.GrupoId, grupoEquipoActual.GrupoId, StringComparison.OrdinalIgnoreCase));
                    if (grupoSemana == null) continue;

                    foreach (var personaSemana in grupoSemana.Personas)
                    {
                        if (!grupoEquipoActual.HistorialPatronesPorPersona.TryGetValue(
                            personaSemana.PersonaId, out var historialPersona)) continue;

                        var registroSemana = historialPersona
                            .FirstOrDefault(h => h.SemanaInicio == inicioSemanaAnteriorRef);
                        if (registroSemana == null) continue;

                        // Último turno real: último día y, dentro del mismo día, el de inicio más tardío
                        var ultimoTurnoReal = personaSemana.Turnos
                            .Where(t => t.HoraInicio != TimeOnly.MinValue)
                            .OrderByDescending(t => t.FechaTurno)
                            .ThenByDescending(t => t.HoraInicio)
                            .FirstOrDefault();

                        if (ultimoTurnoReal != null)
                        {
                            var turnoInicioReal = ultimoTurnoReal.FechaTurno.ToDateTime(ultimoTurnoReal.HoraInicio);
                            // Si HoraFin < HoraInicio el turno cruza medianoche
                            var turnoFinReal = (ultimoTurnoReal.HoraFin != TimeOnly.MinValue &&
                                               ultimoTurnoReal.HoraFin < ultimoTurnoReal.HoraInicio)
                                ? ultimoTurnoReal.FechaTurno.AddDays(1).ToDateTime(ultimoTurnoReal.HoraFin)
                                : ultimoTurnoReal.FechaTurno.ToDateTime(ultimoTurnoReal.HoraFin);

                            registroSemana.UltimoTurnoInicioReal = turnoInicioReal;
                            registroSemana.UltimoTurnoFinReal = turnoFinReal;

                            _logger.LogInformation(
                                "[EnriquecerHistorial] Grupo={GrupoId} Persona={PersonaId}: último turno real={FechaTurno} {HoraInicio:HH:mm}-{HoraFin:HH:mm}",
                                grupoEquipoActual.GrupoId, personaSemana.PersonaId,
                                ultimoTurnoReal.FechaTurno, turnoInicioReal, turnoFinReal);
                        }
                    }
                }
            }

            // Extraer turnos reales de la semana anterior (para personas sin patrón analizado)
            var turnosRealesSemanaAnterior = new Dictionary<string, Dictionary<string, List<PlanificacionTurno>>>(StringComparer.OrdinalIgnoreCase);
            if (semanaAnteriorHistorial != null)
            {
                foreach (var grupoHistorico in semanaAnteriorHistorial.Grupos)
                {
                    var turnosPorPersona = new Dictionary<string, List<PlanificacionTurno>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var personaHistorica in grupoHistorico.Personas)
                    {
                        var turnosPersona = new List<PlanificacionTurno>();
                        foreach (var turnoReal in personaHistorica.Turnos
                            .OrderBy(t => t.FechaTurno)
                            .ThenBy(t => t.HoraInicio))
                        {
                            var inicio = turnoReal.FechaTurno.ToDateTime(turnoReal.HoraInicio);
                            var fin = (turnoReal.HoraFin != TimeOnly.MinValue && turnoReal.HoraFin < turnoReal.HoraInicio)
                                ? turnoReal.FechaTurno.AddDays(1).ToDateTime(turnoReal.HoraFin)
                                : turnoReal.FechaTurno.ToDateTime(turnoReal.HoraFin);

                            turnosPersona.Add(new PlanificacionTurno
                            {
                                Dia = NormalizarNombreDia(ObtenerNombreDia(turnoReal.FechaTurno.DayOfWeek)),
                                TipoHorario = turnoReal.TipoTurnoId,
                                Cantidad = 1,
                                Inicio = inicio,
                                Fin = fin
                            });
                        }
                        turnosPorPersona[personaHistorica.PersonaId] = turnosPersona;
                    }
                    turnosRealesSemanaAnterior[grupoHistorico.GrupoId] = turnosPorPersona;
                }
            }

            // Para grupos con usa_grupos_secundarios: inyectar los turnos reales de los candidatos
            // desde sus grupos primarios originales, para que el algoritmo valide correctamente
            // el descanso >= 8h desde el último domingo de la semana anterior.
            if (gruposConUsaSecundarios.Count > 0)
            {
                // Mapa plano personaId → turnos reales (independiente de su grupo de origen)
                var turnosPlanosPorPersona = turnosRealesSemanaAnterior.Values
                    .SelectMany(d => d)
                    .GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g.SelectMany(kv => kv.Value).ToList(),
                        StringComparer.OrdinalIgnoreCase);

                foreach (var grupoId in gruposConUsaSecundarios)
                {
                    if (!turnosRealesSemanaAnterior.ContainsKey(grupoId))
                        turnosRealesSemanaAnterior[grupoId] = new Dictionary<string, List<PlanificacionTurno>>(StringComparer.OrdinalIgnoreCase);

                    var grupoEquipoSecundario = gruposEquipo.FirstOrDefault(g =>
                        string.Equals(g.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase));
                    if (grupoEquipoSecundario == null) continue;

                    foreach (var persona in grupoEquipoSecundario.Personas)
                    {
                        if (turnosPlanosPorPersona.TryGetValue(persona.PersonaId, out var turnosPersona)
                            && !turnosRealesSemanaAnterior[grupoId].ContainsKey(persona.PersonaId))
                        {
                            turnosRealesSemanaAnterior[grupoId][persona.PersonaId] = turnosPersona;
                            _logger.LogInformation(
                                "[GrupoSecundario] Grupo={GrupoId} Persona={PersonaId}: " +
                                "{Count} turnos reales inyectados para validación descanso >= 8h.",
                                grupoId, persona.PersonaId, turnosPersona.Count);
                        }
                    }
                }
            }

            // 6. Cargar vacaciones antes de ejecutar el algoritmo
            // (el algoritmo las necesita para validar personas en grupos secundarios semana a semana)
            var fechaFinGeneracion = fechaInicio.AddDays(numeroSemanas * 7);
            var equipoIdsBlueprint = grupos
                .Where(g => !string.IsNullOrWhiteSpace(g.EquipoId))
                .Select(g => g.EquipoId!)
                .Distinct()
                .ToList();

            var vacacionesBp = await _db.Vacaciones
                .Include(v => v.Solicitud)
                    .ThenInclude(s => s!.PersonaSolicitante)
                .Where(v => v.Solicitud != null &&
                            v.Solicitud.PersonaSolicitante != null &&
                            v.Solicitud.PersonaSolicitante.EquipoId != null &&
                            equipoIdsBlueprint.Contains(v.Solicitud.PersonaSolicitante.EquipoId))
                .Where(v => v.FechaInicio.ToDateTime(TimeOnly.MinValue) <= fechaFinGeneracion &&
                            v.FechaFin.ToDateTime(TimeOnly.MaxValue) >= fechaInicio)
                .ToListAsync();

            // Mapa personaId → rangos de vacaciones para búsqueda rápida
            var vacMapBp = new Dictionary<string, List<(DateOnly inicio, DateOnly fin)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var vac in vacacionesBp)
            {
                var pid = vac.Solicitud!.PersonaSolicitanteId;
                if (string.IsNullOrWhiteSpace(pid)) continue;
                if (!vacMapBp.TryGetValue(pid, out var rangos))
                    vacMapBp[pid] = rangos = new List<(DateOnly, DateOnly)>();
                rangos.Add((vac.FechaInicio, vac.FechaFin));
            }

            _logger.LogInformation("Blueprint: {Cantidad} vacaciones encontradas para el período", vacacionesBp.Count);

            // 7. Ejecutar el algoritmo de rotación Blueprint (con mapa de vacaciones)
            var solucion = _plantillaAlgoritmoRotacion.GenerarRotacionPorEquipo(
                equipoPlantilla,
                numeroSemanas,
                fechaInicio,
                vacMapBp,
                turnosRealesSemanaAnterior,
                (completedSteps, totalSteps) =>
                {
                    var porcentaje = Math.Clamp(
                        (int)Math.Round(completedSteps * 100d / Math.Max(1, totalSteps), MidpointRounding.AwayFromZero),
                        0,
                        100);
                    reportProgress?.Invoke(new GenerationProgressUpdate(
                        completedSteps,
                        totalSteps,
                        $"Generando Turnos... {porcentaje}%"));
                });

            if (!solucion.ListaAsignaciones.Any())
            {
                return Result<List<TurnoGeneradoPreview>>.Fail("No se pudo generar la rotación");
            }

            // 8. (vacMapBp ya cargado arriba — se reutiliza para Post-Processing)

            // 8. Convertir a Preview excluyendo personas en vacaciones
            var preview = new List<TurnoGeneradoPreview>();
            var turnosGenerados = solucion.ListaAsignaciones.Values.SelectMany(t => t);

            foreach (var turno in turnosGenerados)
            {
                var fechaTurno = DateOnly.FromDateTime(turno.Inicio.Date);
                foreach (var persona in turno.PersonaTurnoTurno)
                {
                    if (EstaEnVacacionesBp(persona.PersonaId, fechaTurno, vacMapBp))
                    {
                        _logger.LogInformation(
                            "Blueprint: persona {PersonaId} excluida el {Fecha} ({TipoTurnoId}) por vacaciones",
                            persona.PersonaId, fechaTurno, turno.TipoTurnoId);
                        continue;
                    }
                    preview.Add(new TurnoGeneradoPreview(
                        persona.PersonaId,
                        turno.TipoTurnoId,
                        turno.GrupoId,
                        fechaTurno));
                }
            }

            // 9–10. Bucle iterativo: balancear FDS + noche hasta que no queden déficits
            //        en fin de semana o se alcance el máximo de iteraciones.
            const int maxIteracionesBalanceo = 100;
            for (int iterBal = 0; iterBal < maxIteracionesBalanceo; iterBal++)
            {
                var previoAlBalanceo = preview.ToList();

                // 9. Balanceador fin de semana: cubrir déficit Sáb/Dom moviendo personas de Jue+Vie.
                preview = BalancearFinDeSemanaBlueprint(
                    preview, blueprintsPorGrupo, tiposTurno, numeroSemanas, fechaInicio, vacMapBp);

                // 10. Balanceador noche: reasignar diurnos a nocturnos con déficit (máx 2 noches/semana por persona).
                preview = BalancearTurnosNocheBlueprint(
                    preview, blueprintsPorGrupo, tiposTurno, numeroSemanas, fechaInicio, vacMapBp);

                // Comprobar si hubo cambios respecto a la iteración anterior.
                // Si no cambió nada, ya no hay déficits que resolver (o no se puede mejorar).
                bool sinCambios = preview.Count == previoAlBalanceo.Count &&
                    !preview.Where((t, idx) => idx < previoAlBalanceo.Count &&
                        (t.PersonaId != previoAlBalanceo[idx].PersonaId ||
                         t.FechaTurno != previoAlBalanceo[idx].FechaTurno ||
                         t.TipoTurnoId != previoAlBalanceo[idx].TipoTurnoId ||
                         t.GrupoId != previoAlBalanceo[idx].GrupoId)).Any();

                if (sinCambios)
                {
                    _logger.LogInformation(
                        "Bucle balanceo FDS+Noche: sin cambios en iteración {Iter}, saliendo.",
                        iterBal + 1);
                    break;
                }

                _logger.LogInformation(
                    "Bucle balanceo FDS+Noche: iteración {Iter} produjo cambios, reintentando.",
                    iterBal + 1);

                if (iterBal == maxIteracionesBalanceo - 1)
                {
                    _logger.LogWarning(
                        "Bucle balanceo FDS+Noche: alcanzado máximo de {Max} iteraciones, continuando con estado actual.",
                        maxIteracionesBalanceo);
                }
            }

            // 11. Balanceador diurno Lun–Vie: cubre déficits diurnos moviéndolos entre pares de días.
            preview = BalancearTurnosDiurnosSemanaBlueprint(
                preview, blueprintsPorGrupo, tiposTurno, numeroSemanas, fechaInicio, vacMapBp);

            // Log final: estado del preview antes de devolver (para diagnóstico)
            _logger.LogInformation(
                "Blueprint FINAL antes de FeriadoBalancer: {Total} turnos. Muestra por fecha/grupo/tipo:",
                preview.Count);
            foreach (var g in preview
                .GroupBy(p => new { p.FechaTurno, p.GrupoId, p.TipoTurnoId })
                .OrderBy(g => g.Key.FechaTurno).ThenBy(g => g.Key.GrupoId).ThenBy(g => g.Key.TipoTurnoId))
            {
                // _logger.LogInformation(
                //     "  {Fecha} {GrupoId} {TipoTurnoId}: [{Personas}]",
                //     g.Key.FechaTurno, g.Key.GrupoId, g.Key.TipoTurnoId,
                //     string.Join(",", g.Select(p => p.PersonaId)));
            }

            return Result<List<TurnoGeneradoPreview>>.Ok(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar previsualizacion de turnos Blueprint");
            return Result<List<TurnoGeneradoPreview>>.Fail($"Error al generar la previsualizacion: {ex.Message}");
        }
    }

#endif
    private static bool EstaEnVacacionesBp(
         string personaId,
         DateOnly fecha,
         IReadOnlyDictionary<string, List<(DateOnly inicio, DateOnly fin)>> mapa)
    {
        if (!mapa.TryGetValue(personaId, out var rangos)) return false;

        foreach (var r in rangos)
        {
            // Dentro del rango de vacaciones
            if (fecha >= r.inicio && fecha <= r.fin)
                return true;

            // Buffer post-vacaciones: 2 días extra, pero solo dentro de la misma semana
            // (el buffer no puede pasar del domingo de la semana en que terminan las vacaciones).
            // DayOfWeek: Sunday=0, Monday=1, ..., Saturday=6
            int diasHastaDomingo = r.fin.DayOfWeek == DayOfWeek.Sunday
                ? 0
                : (7 - (int)r.fin.DayOfWeek);
            int bufferDias = Math.Min(2, diasHastaDomingo);

            if (bufferDias > 0)
            {
                var bufferFin = r.fin.AddDays(bufferDias);
                if (fecha > r.fin && fecha <= bufferFin)
                    return true;
            }
        }

        return false;
    }
    /// <summary>
    /// Balanceador post-generación para fin de semana Blueprint.
    /// Regla: si Sábado o Domingo tienen déficit (RequiereReemplazo=true, asignados &lt; mínimo),
    /// busca personas con turno en Jueves Y Viernes de la misma semana/grupo y las reasigna
    /// al fin de semana, con las siguientes comprobaciones:
    /// - No tiene ya turnos en Sáb/Dom de esa misma semana.
    /// - No tiene Sáb/Dom en la semana anterior ni en la siguiente (sin fines de semana consecutivos).
    /// - Los slots fuente (Jueves/Viernes) no tienen RequiereReemplazo=true (no se puede quitar gente de ellos).
    /// - El descanso entre el fin del turno asignado y el inicio del primer turno del Lunes/Martes
    ///   de la semana siguiente es de al menos 8 horas.
    /// </summary>
    private List<TurnoGeneradoPreview> BalancearFinDeSemanaBlueprint(
        List<TurnoGeneradoPreview> preview,
        Dictionary<string, List<PlanificacionBlueprint>> blueprintsPorGrupo,
        Dictionary<string, TipoTurno> tiposTurno,
        int numeroSemanas,
        DateTime fechaInicio,
        IReadOnlyDictionary<string, List<(DateOnly inicio, DateOnly fin)>> vacMap)
    {
        // Mapa (grupoId, diaNorm, tipoTurnoId) → minPersonas
        var bpSlots = new Dictionary<(string grupoId, string dia, string tipoTurnoId), int>();
        foreach (var (grupoId, bps) in blueprintsPorGrupo)
        {
            foreach (var bp in bps)
            {
                var diaN = NormalizarNombreDia(bp.Dia);
                var key = (grupoId: bp.GrupoId ?? string.Empty, dia: diaN, tipoTurnoId: bp.TipoTurnoId);
                bpSlots[key] = bp.MinPersonasTurno;
            }
        }

        var result = preview.ToList();

        for (int semana = 0; semana < numeroSemanas; semana++)
        {
            var lunes = DateOnly.FromDateTime(fechaInicio.AddDays(semana * 7));
            var jueves = lunes.AddDays(3);
            var viernes = lunes.AddDays(4);
            var sabado = lunes.AddDays(5);
            var domingo = lunes.AddDays(6);

            // Fechas de fin de semana adyacentes (para check de fin de semana consecutivo)
            var sabAnterior = sabado.AddDays(-7);
            var domAnterior = domingo.AddDays(-7);
            var sabSiguiente = sabado.AddDays(7);
            var domSiguiente = domingo.AddDays(7);

            // Lunes y Martes de la semana siguiente (para check de descanso)
            var lunesSig = lunes.AddDays(7);
            var martesSig = lunes.AddDays(8);
            var miercolesSig = lunes.AddDays(9);
            var juevesSig = lunes.AddDays(10);
            var viernesSig = lunes.AddDays(11);

            foreach (var grupoId in blueprintsPorGrupo.Keys)
            {
                // 1. Detectar déficits en Sábado y Domingo (slots con RequiereReemplazo=true)
                var deficits = new List<(DateOnly fecha, string tipoTurnoId, int deficit)>();
                foreach (var (fechaFds, diaNFds) in new[] { (sabado, "Sábado"), (domingo, "Domingo") })
                {
                    var slotsConReemplazo = bpSlots
                        .Where(kvp =>
                            string.Equals(kvp.Key.grupoId, grupoId, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(kvp.Key.dia, diaNFds, StringComparison.OrdinalIgnoreCase) &&
                            kvp.Value > 0)
                        .ToList();

                    foreach (var slot in slotsConReemplazo)
                    {
                        // Suma de mínimos de TODOS los grupos para este (día, tipoTurno)
                        int sumaMinimos = bpSlots
                            .Where(kvp =>
                                string.Equals(kvp.Key.dia, diaNFds, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(kvp.Key.tipoTurnoId, slot.Key.tipoTurnoId, StringComparison.OrdinalIgnoreCase) &&
                                kvp.Value > 0)
                            .Sum(kvp => kvp.Value);
                        // Total asignados de TODOS los grupos para este (fecha, tipoTurno)
                        int totalAsignados = result.Count(p =>
                            p.FechaTurno == fechaFds &&
                            string.Equals(p.TipoTurnoId, slot.Key.tipoTurnoId, StringComparison.OrdinalIgnoreCase));
                        int def = sumaMinimos - totalAsignados;
                        if (def > 0)
                            deficits.Add((fechaFds, slot.Key.tipoTurnoId, def));
                    }
                }

                if (!deficits.Any()) continue;

                // _logger.LogInformation(
                //     "BalFDS: grupo={GrupoId} semana={Sem}: {N} slot(s) con déficit en fin de semana",
                //     grupoId, semana + 1, deficits.Count);

                // 2. Candidatos: personas con turno en Jueves Y Viernes en este grupo esta semana
                var jueEntradas = result
                    .Where(p =>
                        string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase) &&
                        p.FechaTurno == jueves)
                    .ToList();

                var vieEntradas = result
                    .Where(p =>
                        string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase) &&
                        p.FechaTurno == viernes)
                    .ToList();

                // Solo personas que aparecen en AMBOS días
                var candidatos = jueEntradas
                    .Where(pj => vieEntradas.Any(pv =>
                        string.Equals(pv.PersonaId, pj.PersonaId, StringComparison.OrdinalIgnoreCase)))
                    .Select(pj => (
                        personaId: pj.PersonaId,
                        tipoTurnoIdJueves: pj.TipoTurnoId,
                        tipoTurnoIdViernes: vieEntradas.First(pv =>
                            string.Equals(pv.PersonaId, pj.PersonaId, StringComparison.OrdinalIgnoreCase)).TipoTurnoId))
                    .ToList();

                if (!candidatos.Any())
                {
                    // _logger.LogInformation("BalFDS: sin candidatos Jue+Vie para grupo={GrupoId} semana={Sem}", grupoId, semana + 1);
                    continue;
                }

                // 3. Procesar déficits agrupados por tipo de turno.
                // Si un tipo no puede completarse se revierte solo ese tipo; los ya cubiertos se conservan.
                // Para turnos NOCTURNOS se aplica lógica especial según si el déficit es solo Dom,
                // solo Sáb, o ambos (ver detalles abajo).
                // Se excluye HoraFin == 00:00: turno 15:00-24:00 queda como TimeOnly.MinValue
                // y sería clasificado erróneamente como nocturno.
                var turnosNocturnosFds = tiposTurno.Values
                    .Where(t => t.HoraFin < t.HoraInicio && t.HoraFin != TimeOnly.MinValue)
                    .Select(t => t.TipoTurnoId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Agrupar déficits por tipoTurnoId para procesar Sáb+Dom juntos.
                // Los turnos nocturnos se procesan primero.
                var deficitsPorTipo = deficits
                    .GroupBy(d => d.tipoTurnoId, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => turnosNocturnosFds.Contains(g.Key))
                    .ToList();

                var tipoTurnoFallido = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Helper local: verifica descanso ≥8h antes de Lunes Y Martes siguiente
                bool DescansoOkLunesYMartes(string personaId, DateTime finTurno)
                {
                    foreach (var diaCheck in new[] { lunesSig, martesSig })
                    {
                        var turnos = result
                            .Where(p =>
                                string.Equals(p.PersonaId, personaId, StringComparison.OrdinalIgnoreCase) &&
                                p.FechaTurno == diaCheck &&
                                tiposTurno.ContainsKey(p.TipoTurnoId))
                            .ToList();
                        if (!turnos.Any()) continue;
                        var inicioMin = turnos
                            .Select(p => diaCheck.ToDateTime(tiposTurno[p.TipoTurnoId].HoraInicio))
                            .Min();
                        if ((inicioMin - finTurno).TotalHours < 8) return false;
                    }
                    return true;
                }

                foreach (var tipoGrupo in deficitsPorTipo)
                {
                    var tipoTurnoId = tipoGrupo.Key;
                    if (!tiposTurno.TryGetValue(tipoTurnoId, out var tipoDestino)) continue;

                    bool esNocturno = turnosNocturnosFds.Contains(tipoTurnoId);

                    // Snapshot antes de intentar este tipo
                    var snapshotTipo = result.ToList();

                    bool tipoOk = true; // se pone false si alguna unidad no puede cubrirse

                    if (esNocturno)
                    {
                        // ── LÓGICA ESPECIAL PARA NOCTURNOS ──────────────────────────────────────
                        // Determinar qué días tienen déficit nocturno
                        bool defSab = tipoGrupo.Any(d => d.fecha == sabado);
                        bool defDom = tipoGrupo.Any(d => d.fecha == domingo);

                        // Fin del turno nocturno en Sábado y en Domingo (para check descanso)
                        DateTime finNocheSab = sabado.ToDateTime(tipoDestino.HoraFin).AddDays(1); // cruza medianoche
                        DateTime finNocheDom = domingo.ToDateTime(tipoDestino.HoraFin).AddDays(1);

                        // tipoTurnoId del slot Viernes del candidato (puede variar; se resuelve al seleccionar)
                        // No necesitamos el Jueves para el caso "solo Dom"

                        if (!defSab && defDom)
                        {
                            // ── CASO A: solo déficit en Domingo ──
                            // Candidato: persona con turno en Viernes (no RequiereReemplazo).
                            // Se quita solo Viernes y se pone en Domingo.
                            // No es necesario cubrir Sábado (ya está completo).

                            var vieCandidatos = vieEntradas.ToList();

                            int defDomCount = tipoGrupo.First(d => d.fecha == domingo).deficit;
                            for (int i = 0; i < defDomCount && tipoOk; i++)
                            {
                                bool cubierto = false;

                                foreach (var pv in vieCandidatos)
                                {
                                    if (result.Any(p =>
                                            string.Equals(p.PersonaId, pv.PersonaId, StringComparison.OrdinalIgnoreCase) &&
                                            p.FechaTurno == domingo)) continue;
                                    // Solo bloquea si tiene turno NOCTURNO en FDS anterior/siguiente
                                    if (result.Any(p =>
                                            string.Equals(p.PersonaId, pv.PersonaId, StringComparison.OrdinalIgnoreCase) &&
                                            (p.FechaTurno == sabAnterior || p.FechaTurno == domAnterior) &&
                                            turnosNocturnosFds.Contains(p.TipoTurnoId))) continue;
                                    if (result.Any(p =>
                                            string.Equals(p.PersonaId, pv.PersonaId, StringComparison.OrdinalIgnoreCase) &&
                                            (p.FechaTurno == sabSiguiente || p.FechaTurno == domSiguiente) &&
                                            turnosNocturnosFds.Contains(p.TipoTurnoId))) continue;
                                    if (!DescansoOkLunesYMartes(pv.PersonaId, finNocheDom)) continue;

                                    if (EstaEnVacacionesBp(pv.PersonaId, domingo, vacMap))
                                    {
                                        // _logger.LogInformation("BalFDS noche CasoA: {PId} SKIP — vacaciones en Dom", pv.PersonaId);
                                        continue;
                                    }
                                    var entVie2 = result.FirstOrDefault(p =>
                                        string.Equals(p.PersonaId, pv.PersonaId, StringComparison.OrdinalIgnoreCase) &&
                                        p.FechaTurno == viernes &&
                                        string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase));
                                    if (entVie2 != null) result.Remove(entVie2);
                                    result.Add(new TurnoGeneradoPreview(pv.PersonaId, tipoTurnoId, grupoId, domingo));
                                    _logger.LogInformation(
                                        "BalFDS noche CasoA: {PId} Vie → Dom", pv.PersonaId);
                                    cubierto = true;
                                    break;
                                }

                                if (!cubierto) tipoOk = false;
                            }
                        }
                        else
                        {
                            // ── CASO B: déficit en Sábado (solo Sáb, o Sáb+Dom) ──
                            // Candidato: persona con turno en Jueves Y Viernes.
                            // Se quita Jueves+Viernes y se coloca en AMBOS Sábado Y Domingo
                            // independientemente de cuál de los dos tenga déficit.
                            // unidades a cubrir = max(defSabCount, defDomCount) porque la
                            // persona cubre ambos días a la vez.
                            int defSabCount = defSab ? tipoGrupo.First(d => d.fecha == sabado).deficit : 0;
                            int defDomCount = defDom ? tipoGrupo.First(d => d.fecha == domingo).deficit : 0;
                            int unidades = Math.Max(defSabCount, defDomCount);

                            var personasYaUsadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                            for (int i = 0; i < unidades && tipoOk; i++)
                            {
                                bool cubierto = false;

                                // Fin del turno más tardío entre Sáb y Dom (para check descanso)
                                // El domingo noche termina más tarde (un día después), pero como
                                // son iguales de duración usamos finNocheDom como el más restrictivo.
                                DateTime finMasRestrictivo = finNocheDom;

                                foreach (var cand in candidatos)
                                {
                                    if (personasYaUsadas.Contains(cand.personaId)) continue;
                                    // No debe tener turno en Sáb ni Dom ya (en este tipo)
                                    if (result.Any(p =>
                                            string.Equals(p.PersonaId, cand.personaId, StringComparison.OrdinalIgnoreCase) &&
                                            (p.FechaTurno == sabado || p.FechaTurno == domingo) &&
                                            string.Equals(p.TipoTurnoId, tipoTurnoId, StringComparison.OrdinalIgnoreCase))) continue;
                                    // Solo bloquea si tiene turno NOCTURNO en FDS anterior/siguiente
                                    if (result.Any(p =>
                                            string.Equals(p.PersonaId, cand.personaId, StringComparison.OrdinalIgnoreCase) &&
                                            (p.FechaTurno == sabAnterior || p.FechaTurno == domAnterior) &&
                                            turnosNocturnosFds.Contains(p.TipoTurnoId))) continue;
                                    if (result.Any(p =>
                                            string.Equals(p.PersonaId, cand.personaId, StringComparison.OrdinalIgnoreCase) &&
                                            (p.FechaTurno == sabSiguiente || p.FechaTurno == domSiguiente) &&
                                            turnosNocturnosFds.Contains(p.TipoTurnoId))) continue;
                                    if (!DescansoOkLunesYMartes(cand.personaId, finMasRestrictivo)) continue;

                                    if (EstaEnVacacionesBp(cand.personaId, sabado, vacMap) ||
                                        EstaEnVacacionesBp(cand.personaId, domingo, vacMap))
                                    {
                                        _logger.LogInformation("BalFDS noche CasoB: {PId} SKIP — vacaciones en Sáb o Dom", cand.personaId);
                                        continue;
                                    }
                                    var entJue2 = result.FirstOrDefault(p =>
                                        string.Equals(p.PersonaId, cand.personaId, StringComparison.OrdinalIgnoreCase) &&
                                        p.FechaTurno == jueves &&
                                        string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase));
                                    var entVie2 = result.FirstOrDefault(p =>
                                        string.Equals(p.PersonaId, cand.personaId, StringComparison.OrdinalIgnoreCase) &&
                                        p.FechaTurno == viernes &&
                                        string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase));
                                    if (entJue2 != null) result.Remove(entJue2);
                                    if (entVie2 != null) result.Remove(entVie2);
                                    result.Add(new TurnoGeneradoPreview(cand.personaId, tipoTurnoId, grupoId, sabado));
                                    result.Add(new TurnoGeneradoPreview(cand.personaId, tipoTurnoId, grupoId, domingo));
                                    personasYaUsadas.Add(cand.personaId);
                                    // _logger.LogInformation(
                                    //     "BalFDS noche CasoB: {PId} Jue+Vie → Sáb+Dom", cand.personaId);

                                    // ── Reubicar semana siguiente: Lun/Mar/Mié → Mié/Jue/Vie (mañana) ──
                                    if (semana + 1 < numeroSemanas)
                                    {
                                        // ORDEN INVERTIDO: Mié→Vie primero, después Mar→Jue, último Lun→Mié.
                                        // Así el slot de miercolesSig queda libre antes de que lunesSig lo use como destino.
                                        var diasFuente = new[] { miercolesSig, martesSig, lunesSig };
                                        var diasDestino = new[] { viernesSig, juevesSig, miercolesSig };

                                        // Recopilar antes de modificar result
                                        var movSig = new List<(TurnoGeneradoPreview turno, DateOnly fechaDest)>();
                                        for (int d = 0; d < diasFuente.Length; d++)
                                        {
                                            result
                                                .Where(p =>
                                                    string.Equals(p.PersonaId, cand.personaId, StringComparison.OrdinalIgnoreCase) &&
                                                    p.FechaTurno == diasFuente[d] &&
                                                    string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase))
                                                .ToList()
                                                .ForEach(t => movSig.Add((t, diasDestino[d])));
                                        }

                                        var bpsGrupo = blueprintsPorGrupo.TryGetValue(grupoId, out var bpListSig) ? bpListSig : null;

                                        foreach (var (t, fechaDest) in movSig)
                                        {
                                            if (EstaEnVacacionesBp(cand.personaId, fechaDest, vacMap))
                                            {
                                                _logger.LogInformation(
                                                    "BalFDS CasoB sig: {PId} SKIP {Src}→{Dst}: vacaciones",
                                                    cand.personaId, t.FechaTurno, fechaDest);
                                                continue;
                                            }
                                            if (result.Any(p =>
                                                    string.Equals(p.PersonaId, cand.personaId, StringComparison.OrdinalIgnoreCase) &&
                                                    p.FechaTurno == fechaDest &&
                                                    string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase)))
                                            {
                                                _logger.LogInformation(
                                                    "BalFDS CasoB sig: {PId} SKIP {Src}→{Dst}: ya tiene turno en destino",
                                                    cand.personaId, t.FechaTurno, fechaDest);
                                                continue;
                                            }
                                            // Usar el TipoTurno diurno (mañana) del blueprint del día destino
                                            var diaNombreDest = NormalizarNombreDia(ObtenerNombreDia(fechaDest.DayOfWeek));
                                            var tipoManana = bpsGrupo?
                                                .Where(bp => NormalizarNombreDia(bp.Dia) == diaNombreDest
                                                          && !turnosNocturnosFds.Contains(bp.TipoTurnoId))
                                                .Select(bp => bp.TipoTurnoId)
                                                .FirstOrDefault();
                                            var tipoDestId = tipoManana ?? t.TipoTurnoId;
                                            result.Remove(t);
                                            result.Add(new TurnoGeneradoPreview(cand.personaId, tipoDestId, grupoId, fechaDest));
                                            // _logger.LogInformation(
                                            //     "BalFDS CasoB sig: {PId} {Src} → {Dst} tipo={TipoId}",
                                            //     cand.personaId, t.FechaTurno, fechaDest, tipoDestId);
                                        }
                                    }

                                    cubierto = true;
                                    break;
                                }

                                if (!cubierto) tipoOk = false;
                            }
                        }
                    }
                    else
                    {
                        // ── LÓGICA para turnos DIURNOS (mañana / tarde / etc.) ─────────────────
                        // Cuando AMBOS Sáb y Dom tienen déficit se procesan JUNTOS: se asigna
                        // la misma persona a los dos días, usando el descanso del Domingo
                        // (el más restrictivo) contra Lun/Mar siguiente. Si no pasa el check
                        // de Dom se descarta al candidato sin asignarle Sáb.
                        bool defDiurSab = tipoGrupo.Any(d => d.fecha == sabado);
                        bool defDiurDom = tipoGrupo.Any(d => d.fecha == domingo);

                        // Fin del turno en Domingo (el más restrictivo para descanso vs Lun/Mar)
                        DateTime finTurnoDom = tipoDestino.HoraFin < tipoDestino.HoraInicio
                            ? domingo.ToDateTime(tipoDestino.HoraFin).AddDays(1)
                            : domingo.ToDateTime(tipoDestino.HoraFin);
                        DateTime finTurnoSab = tipoDestino.HoraFin < tipoDestino.HoraInicio
                            ? sabado.ToDateTime(tipoDestino.HoraFin).AddDays(1)
                            : sabado.ToDateTime(tipoDestino.HoraFin);

                        // Helper local: check descanso >=8h con Lun/Mar usando un finTurno dado
                        bool DescansoOkDiurno(string personaId, DateTime finTurno)
                        {
                            foreach (var diaCheck in new[] { lunesSig, martesSig })
                            {
                                var turnosSigDia = result
                                    .Where(p =>
                                        string.Equals(p.PersonaId, personaId, StringComparison.OrdinalIgnoreCase) &&
                                        p.FechaTurno == diaCheck &&
                                        tiposTurno.ContainsKey(p.TipoTurnoId))
                                    .ToList();
                                if (!turnosSigDia.Any()) continue;
                                var inicioMasTemp = turnosSigDia
                                    .Min(p => diaCheck.ToDateTime(tiposTurno[p.TipoTurnoId].HoraInicio));
                                if ((inicioMasTemp - finTurno).TotalHours < 8)
                                    return false;
                            }
                            return true;
                        }

                        if (defDiurSab && defDiurDom)
                        {
                            // ── Sáb + Dom juntos ──
                            int defSabCount = tipoGrupo.First(d => d.fecha == sabado).deficit;
                            int defDomCount = tipoGrupo.First(d => d.fecha == domingo).deficit;
                            int unidades = Math.Max(defSabCount, defDomCount);

                            var personasYaUsadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                            for (int i = 0; i < unidades && tipoOk; i++)
                            {
                                bool cubierto = false;
                                foreach (var candidato in candidatos)
                                {
                                    if (personasYaUsadas.Contains(candidato.personaId)) continue;
                                    // No debe tener turno ya en Sáb ni Dom
                                    if (result.Any(p =>
                                            string.Equals(p.PersonaId, candidato.personaId, StringComparison.OrdinalIgnoreCase) &&
                                            (p.FechaTurno == sabado || p.FechaTurno == domingo)))
                                        continue;
                                    // El balanceador tiene prioridad sobre la alternancia de FDS;
                                    // no se bloquea por FDS anterior/siguiente en turnos diurnos.
                                    // if (result.Any(p =>
                                    //         string.Equals(p.PersonaId, candidato.personaId, StringComparison.OrdinalIgnoreCase) &&
                                    //         (p.FechaTurno == sabAnterior || p.FechaTurno == domAnterior)))
                                    //     continue;
                                    // if (result.Any(p =>
                                    //         string.Equals(p.PersonaId, candidato.personaId, StringComparison.OrdinalIgnoreCase) &&
                                    //         (p.FechaTurno == sabSiguiente || p.FechaTurno == domSiguiente)))
                                    //     continue;
                                    // Descanso: usar Domingo (el más restrictivo) contra Lun/Mar
                                    if (!DescansoOkDiurno(candidato.personaId, finTurnoDom))
                                    {
                                        // _logger.LogInformation(
                                        //     "BalFDS diurno Sáb+Dom: {PId} SKIP — descanso insuficiente Dom→Lun/Mar",
                                        //     candidato.personaId);
                                        continue;
                                    }
                                    // Vacaciones
                                    if (EstaEnVacacionesBp(candidato.personaId, sabado, vacMap) ||
                                        EstaEnVacacionesBp(candidato.personaId, domingo, vacMap))
                                    {
                                        _logger.LogInformation(
                                            "BalFDS diurno Sáb+Dom: {PId} SKIP — vacaciones en Sáb o Dom",
                                            candidato.personaId);
                                        continue;
                                    }

                                    // Asignar: quitar Jue+Vie, poner Sáb+Dom
                                    var entJue = result.FirstOrDefault(p =>
                                        string.Equals(p.PersonaId, candidato.personaId, StringComparison.OrdinalIgnoreCase) &&
                                        p.FechaTurno == jueves &&
                                        string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase));
                                    var entVie = result.FirstOrDefault(p =>
                                        string.Equals(p.PersonaId, candidato.personaId, StringComparison.OrdinalIgnoreCase) &&
                                        p.FechaTurno == viernes &&
                                        string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase));
                                    if (entJue != null) result.Remove(entJue);
                                    if (entVie != null) result.Remove(entVie);
                                    result.Add(new TurnoGeneradoPreview(candidato.personaId, tipoTurnoId, grupoId, sabado));
                                    result.Add(new TurnoGeneradoPreview(candidato.personaId, tipoTurnoId, grupoId, domingo));
                                    personasYaUsadas.Add(candidato.personaId);
                                    // _logger.LogInformation(
                                    //     "BalFDS diurno Sáb+Dom: {PId} Jue+Vie → Sáb+Dom grupo={GId}",
                                    //     candidato.personaId, grupoId);
                                    cubierto = true;
                                    break;
                                }
                                if (!cubierto) tipoOk = false;
                            }
                        }
                        else
                        {
                            // ── Solo Sáb o solo Dom ──
                            foreach (var deficitSlot in tipoGrupo)
                            {
                                if (!tipoOk) break;

                                // Descanso siempre contra el día asignado real
                                DateTime finTurnoFds = deficitSlot.fecha == domingo ? finTurnoDom : finTurnoSab;

                                for (int i = 0; i < deficitSlot.deficit && tipoOk; i++)
                                {
                                    bool cubierto = false;
                                    foreach (var candidato in candidatos)
                                    {
                                        if (result.Any(p =>
                                                string.Equals(p.PersonaId, candidato.personaId, StringComparison.OrdinalIgnoreCase) &&
                                                p.FechaTurno == deficitSlot.fecha))
                                            continue;
                                        // El balanceador tiene prioridad sobre la alternancia de FDS;
                                        // no se bloquea por FDS anterior/siguiente en turnos diurnos.
                                        // if (result.Any(p =>
                                        //         string.Equals(p.PersonaId, candidato.personaId, StringComparison.OrdinalIgnoreCase) &&
                                        //         (p.FechaTurno == sabAnterior || p.FechaTurno == domAnterior)))
                                        //     continue;
                                        // if (result.Any(p =>
                                        //         string.Equals(p.PersonaId, candidato.personaId, StringComparison.OrdinalIgnoreCase) &&
                                        //         (p.FechaTurno == sabSiguiente || p.FechaTurno == domSiguiente)))
                                        //     continue;
                                        if (!DescansoOkDiurno(candidato.personaId, finTurnoFds))
                                        {
                                            _logger.LogInformation(
                                                "BalFDS diurno: {PId} SKIP — descanso insuficiente {Fecha}→Lun/Mar",
                                                candidato.personaId, deficitSlot.fecha);
                                            continue;
                                        }
                                        if (EstaEnVacacionesBp(candidato.personaId, deficitSlot.fecha, vacMap))
                                        {
                                            _logger.LogInformation(
                                                "BalFDS diurno: {PId} SKIP — vacaciones en {Fecha}",
                                                candidato.personaId, deficitSlot.fecha);
                                            continue;
                                        }
                                        var entJue = result.FirstOrDefault(p =>
                                            string.Equals(p.PersonaId, candidato.personaId, StringComparison.OrdinalIgnoreCase) &&
                                            p.FechaTurno == jueves &&
                                            string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase));
                                        var entVie = result.FirstOrDefault(p =>
                                            string.Equals(p.PersonaId, candidato.personaId, StringComparison.OrdinalIgnoreCase) &&
                                            p.FechaTurno == viernes &&
                                            string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase));
                                        if (entJue != null) result.Remove(entJue);
                                        if (entVie != null) result.Remove(entVie);
                                        result.Add(new TurnoGeneradoPreview(
                                            candidato.personaId, deficitSlot.tipoTurnoId, grupoId, deficitSlot.fecha));
                                        _logger.LogInformation(
                                            "BalFDS diurno: {PId} Jue+Vie → {Fecha} grupo={GId}",
                                            candidato.personaId, deficitSlot.fecha, grupoId);
                                        cubierto = true;
                                        break;
                                    }
                                    if (!cubierto) tipoOk = false;
                                }
                            }
                        }
                    }

                    if (!tipoOk)
                    {
                        tipoTurnoFallido.Add(tipoTurnoId);
                        result.Clear();
                        result.AddRange(snapshotTipo);
                        _logger.LogWarning(
                            "BalFDS: tipo={TipoTurnoId} no completado sem={Sem} grupo={GId} — revertido; tipos ya cubiertos se conservan",
                            tipoTurnoId, semana + 1, grupoId);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Balanceador post-generación para turnos diurnos de Lunes a Viernes.
    /// Cubre déficits en turnos no nocturnos convirtiendo personas del mismo día
    /// desde otro turno diurno disponible hacia el turno deficitario.
    /// Ejemplos:
    ///   - Déficit Lunes tarde  → candidatos de Lunes mañana.
    ///   - Déficit Martes tarde → candidatos de Martes mañana.
    ///   - Déficit Jueves tarde → candidatos de Jueves mañana.
    ///   - Déficit Viernes tarde → candidatos de Viernes mañana.
    /// Condición: el candidato no debe tener otro turno adicional en esa misma fecha.
    /// </summary>

    /// <summary>
    /// Balanceador post-generación para Blueprint.
    /// Regla: si un turno nocturno (cruza medianoche) tiene RequiereReemplazo=true y quedó
    /// con déficit (personas asignadas &lt; mínimo del blueprint), intenta cubrir ese déficit
    /// moviendo personas del mismo día/grupo que están en turnos diurnos con RequiereReemplazo=false.
    /// </summary>
    private List<TurnoGeneradoPreview> BalancearTurnosNocheBlueprint(
        List<TurnoGeneradoPreview> preview,
        Dictionary<string, List<PlanificacionBlueprint>> blueprintsPorGrupo,
        Dictionary<string, TipoTurno> tiposTurno,
        int numeroSemanas,
        DateTime fechaInicio,
        IReadOnlyDictionary<string, List<(DateOnly inicio, DateOnly fin)>> vacMap)
    {
        // Mapa (grupoId, diaNormalizado, tipoTurnoId) → minPersonas
        var bpSlots = new Dictionary<(string grupoId, string dia, string tipoTurnoId), int>();
        foreach (var (grupoId, bps) in blueprintsPorGrupo)
        {
            foreach (var bp in bps)
            {
                var diaN = NormalizarNombreDia(bp.Dia);
                var key = (grupoId: bp.GrupoId ?? string.Empty, dia: diaN, tipoTurnoId: bp.TipoTurnoId);
                bpSlots[key] = bp.MinPersonasTurno;
            }
        }

        // Turnos que cruzan medianoche → nocturnos
        // Se excluye HoraFin == 00:00 porque un turno 15:00-24:00 se guarda como HoraFin=TimeOnly.MinValue
        // y 00:00 < 15:00 lo clasificaría erróneamente como nocturno. Los turnos que realmente
        // cruzan la medianoche tienen HoraFin distinto de 00:00 (ej: 06:00).
        var turnosNocturnos = tiposTurno.Values
            .Where(t => t.HoraFin < t.HoraInicio && t.HoraFin != TimeOnly.MinValue)
            .Select(t => t.TipoTurnoId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (turnosNocturnos.Count == 0)
        {
            _logger.LogInformation("Balanceador Blueprint: sin turnos nocturnos configurados, se omite.");
            return preview;
        }

        var result = preview.ToList();

        for (int semana = 0; semana < numeroSemanas; semana++)
        {
            var lunes = DateOnly.FromDateTime(fechaInicio.AddDays(semana * 7));

            // Snapshot de noches por persona AL INICIO de la semana (antes de cualquier movimiento).
            // El cap se evalúa contra este snapshot para que los turnos añadidos durante el
            // propio balanceo no bloqueen cubrir déficits en otros días de la misma semana.
            var nocturnosInicioSemana = result
                .Where(p =>
                    p.FechaTurno >= lunes && p.FechaTurno <= lunes.AddDays(6) &&
                    turnosNocturnos.Contains(p.TipoTurnoId))
                .GroupBy(p => p.PersonaId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            // Iterar de domingo hacia lunes para que los turnos del final de la semana
            // se resuelvan primero: si Viernes→noche se mueve antes que Jueves→noche,
            // el check de "día siguiente" del Jueves ya verá la noche del Viernes y pasará.
            for (int dayOffset = 6; dayOffset >= 0; dayOffset--)
            {
                var fecha = lunes.AddDays(dayOffset);
                var diaNombre = NormalizarNombreDia(ObtenerNombreDia(fecha.DayOfWeek));

                // Grupos que tienen blueprint configurado para este día
                var gruposConDia = bpSlots.Keys
                    .Where(k => string.Equals(k.dia, diaNombre, StringComparison.OrdinalIgnoreCase))
                    .Select(k => k.grupoId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var grupoId in gruposConDia)
                {
                    // Slots nocturnos con déficit (minPersonas > 0)
                    var slotsNocheDeficit = bpSlots
                        .Where(kvp =>
                            string.Equals(kvp.Key.grupoId, grupoId, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(kvp.Key.dia, diaNombre, StringComparison.OrdinalIgnoreCase) &&
                            turnosNocturnos.Contains(kvp.Key.tipoTurnoId) &&
                            kvp.Value > 0)
                        .ToList();

                    foreach (var deficitKvp in slotsNocheDeficit)
                    {
                        var tipoNoche = deficitKvp.Key.tipoTurnoId;
                        // Suma de mínimos de TODOS los grupos para este (día, tipoTurno nocturno)
                        var sumaMinNoche = bpSlots
                            .Where(kvp =>
                                string.Equals(kvp.Key.dia, diaNombre, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(kvp.Key.tipoTurnoId, tipoNoche, StringComparison.OrdinalIgnoreCase) &&
                                kvp.Value > 0)
                            .Sum(kvp => kvp.Value);

                        // Total asignados de TODOS los grupos para este (fecha, tipoTurno nocturno)
                        var asignadosNoche = result.Count(p =>
                            p.FechaTurno == fecha &&
                            string.Equals(p.TipoTurnoId, tipoNoche, StringComparison.OrdinalIgnoreCase));

                        int deficit = sumaMinNoche - asignadosNoche;
                        if (deficit <= 0) continue;

                        _logger.LogInformation(
                            "Balanceador Blueprint: déficit detectado — grupo={GrupoId} {Dia} {TipoNoche} {Fecha}: sumaMin={Min} totalAsig={Asig}",
                            grupoId, diaNombre, tipoNoche, fecha, sumaMinNoche, asignadosNoche);

                        // Turnos diurnos del mismo día/grupo (fuente para mover al nocturno)
                        var slotsFuente = bpSlots
                            .Where(kvp =>
                                string.Equals(kvp.Key.grupoId, grupoId, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(kvp.Key.dia, diaNombre, StringComparison.OrdinalIgnoreCase) &&
                                !turnosNocturnos.Contains(kvp.Key.tipoTurnoId))
                            .Select(kvp => kvp.Key.tipoTurnoId)
                            .ToList();

                        // Horario del turno nocturno destino para el check de descanso
                        var tipoNocheInfo = tiposTurno.GetValueOrDefault(tipoNoche);
                        // Inicio del turno nocturno en la fecha actual
                        DateTime inicioNoche = tipoNocheInfo != null
                            ? fecha.ToDateTime(tipoNocheInfo.HoraInicio)
                            : DateTime.MinValue;
                        // Fin del turno nocturno (cruza medianoche)
                        DateTime finNoche = tipoNocheInfo != null
                            ? (tipoNocheInfo.HoraFin < tipoNocheInfo.HoraInicio
                                ? fecha.ToDateTime(tipoNocheInfo.HoraFin).AddDays(1)
                                : fecha.ToDateTime(tipoNocheInfo.HoraFin))
                            : DateTime.MinValue;

                        // Día anterior y siguiente para check de descanso
                        var fechaAnterior = fecha.AddDays(-1);
                        var fechaSiguiente = fecha.AddDays(1);

                        foreach (var tipoFuente in slotsFuente)
                        {
                            if (deficit <= 0) break;

                            var personasEnFuente = result
                                .Where(p =>
                                    string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase) &&
                                    p.FechaTurno == fecha &&
                                    string.Equals(p.TipoTurnoId, tipoFuente, StringComparison.OrdinalIgnoreCase))
                                .ToList();

                            var yaEnNoche = result
                                .Where(p =>
                                    string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase) &&
                                    p.FechaTurno == fecha &&
                                    string.Equals(p.TipoTurnoId, tipoNoche, StringComparison.OrdinalIgnoreCase))
                                .Select(p => p.PersonaId)
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);

                            foreach (var entrada in personasEnFuente)
                            {
                                if (deficit <= 0) break;
                                if (yaEnNoche.Contains(entrada.PersonaId)) continue;

                                // No puede ser candidato si ya tenía 2+ turnos nocturnos al inicio del balanceo de esta semana
                                nocturnosInicioSemana.TryGetValue(entrada.PersonaId, out int nocturnosIniciales);
                                if (nocturnosIniciales >= 2) continue;

                                // Check descanso con turno del día anterior (su fin debe ser >= 8h antes de inicioNoche)
                                if (tipoNocheInfo != null)
                                {
                                    // Usar el turno que TERMINA más tarde en el día anterior
                                    var finPrevMax = result
                                        .Where(p =>
                                            string.Equals(p.PersonaId, entrada.PersonaId, StringComparison.OrdinalIgnoreCase) &&
                                            p.FechaTurno == fechaAnterior &&
                                            tiposTurno.ContainsKey(p.TipoTurnoId))
                                        .Select(p =>
                                        {
                                            var tp = tiposTurno[p.TipoTurnoId];
                                            return tp.HoraFin < tp.HoraInicio
                                                ? fechaAnterior.ToDateTime(tp.HoraFin).AddDays(1)
                                                : fechaAnterior.ToDateTime(tp.HoraFin);
                                        })
                                        .Cast<DateTime?>()
                                        .Max();
                                    if (finPrevMax.HasValue && (inicioNoche - finPrevMax.Value).TotalHours < 8)
                                    {
                                        _logger.LogInformation(
                                            "BalNoche: {PId} rechazado — descanso insuficiente {H:F1}h desde turno anterior",
                                            entrada.PersonaId, (inicioNoche - finPrevMax.Value).TotalHours);
                                        continue;
                                    }

                                    // Check descanso con turno del día siguiente (fin de la noche vs inicio del siguiente)
                                    var turnoSiguiente = result
                                        .Where(p =>
                                            string.Equals(p.PersonaId, entrada.PersonaId, StringComparison.OrdinalIgnoreCase) &&
                                            p.FechaTurno == fechaSiguiente &&
                                            tiposTurno.ContainsKey(p.TipoTurnoId))
                                        .Select(p => fechaSiguiente.ToDateTime(tiposTurno[p.TipoTurnoId].HoraInicio))
                                        .OrderBy(t => t)
                                        .Cast<DateTime?>()
                                        .FirstOrDefault();
                                    if (turnoSiguiente.HasValue && (turnoSiguiente.Value - finNoche).TotalHours < 8)
                                    {
                                        _logger.LogInformation(
                                            "BalNoche: {PId} rechazado — descanso insuficiente {H:F1}h antes de turno siguiente",
                                            entrada.PersonaId, (turnoSiguiente.Value - finNoche).TotalHours);
                                        continue;
                                    }
                                }

                                // Mover: quitar del turno diurno y agregar al nocturno
                                // Verificar que el candidato no esté de vacaciones en el día destino
                                if (EstaEnVacacionesBp(entrada.PersonaId, fecha, vacMap))
                                {
                                    _logger.LogInformation(
                                        "BalNoche: {PId} SKIP — vacaciones en {Fecha} destino",
                                        entrada.PersonaId, fecha);
                                    continue;
                                }
                                result.Remove(entrada);
                                result.Add(new TurnoGeneradoPreview(
                                    entrada.PersonaId, tipoNoche, grupoId, fecha));
                                yaEnNoche.Add(entrada.PersonaId);
                                deficit--;

                                _logger.LogInformation(
                                    "Balanceador Blueprint: persona {PersonaId} reasignada {TipoFuente} → {TipoNoche} el {Fecha} (grupo {GrupoId})",
                                    entrada.PersonaId, tipoFuente, tipoNoche, fecha, grupoId);
                            }
                        }

                        int cubiertos = sumaMinNoche - deficit;
                        if (deficit > 0)
                            _logger.LogWarning(
                                "Balanceador Blueprint: déficit residual={Def} en grupo={GrupoId} {TipoNoche} {Fecha} — no había suficientes candidatos diurnos",
                                deficit, grupoId, tipoNoche, fecha);
                        else
                            _logger.LogInformation(
                                "Balanceador Blueprint: déficit cubierto en grupo={GrupoId} {TipoNoche} {Fecha} ({Cubiertos} persona(s) reasignada(s))",
                                grupoId, tipoNoche, fecha, cubiertos);
                    }
                }
            }
        }

        return result;
    }
    private List<TurnoGeneradoPreview> BalancearTurnosDiurnosSemanaBlueprint(
        List<TurnoGeneradoPreview> preview,
        Dictionary<string, List<PlanificacionBlueprint>> blueprintsPorGrupo,
        Dictionary<string, TipoTurno> tiposTurno,
        int numeroSemanas,
        DateTime fechaInicio,
        IReadOnlyDictionary<string, List<(DateOnly inicio, DateOnly fin)>> vacMap)
    {
        // Mapa (grupoId, diaNorm, tipoTurnoId) → minPersonas
        // Si MinPersonasTurno no fue configurado explícitamente (= 0), se usa el conteo de etiquetas
        // como mínimo implícito. Esto permite detectar déficits inducidos por el balanceador FDS
        // que mueve personas de Jueves/Viernes al fin de semana: esas celdas podían tener
        // MinPersonasTurno=0 porque originalmente no tenían déficit.
        var bpSlots = new Dictionary<(string grupoId, string dia, string tipoTurnoId), int>();
        foreach (var (grupoId, bps) in blueprintsPorGrupo)
        {
            foreach (var bp in bps)
            {
                var diaN = NormalizarNombreDia(bp.Dia);
                var key = (grupoId: bp.GrupoId ?? string.Empty, dia: diaN, tipoTurnoId: bp.TipoTurnoId);
                int etiquetasCount = string.IsNullOrWhiteSpace(bp.Etiquetas) ? 0 :
                    bp.Etiquetas.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim()).Count(e => !string.IsNullOrEmpty(e));
                bpSlots[key] = bp.MinPersonasTurno > 0 ? bp.MinPersonasTurno : etiquetasCount;
            }
        }

        var turnosNocturnos = tiposTurno.Values
            .Where(t => t.HoraFin < t.HoraInicio && t.HoraFin != TimeOnly.MinValue)
            .Select(t => t.TipoTurnoId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // _logger.LogInformation(
        //     "BalDiurnoSem INICIO: semanas={Sem} grupos={Grupos} bpSlots={Slots} turnosNocturnos=[{Noct}]",
            // numeroSemanas,
            // string.Join(",", blueprintsPorGrupo.Keys),
            // bpSlots.Count,
            // string.Join(",", turnosNocturnos));

        var result = preview.ToList();

        for (int semana = 0; semana < numeroSemanas; semana++)
        {
            var lunes = DateOnly.FromDateTime(fechaInicio.AddDays(semana * 7));
            var martes = lunes.AddDays(1);
            var miercoles = lunes.AddDays(2);
            var jueves = lunes.AddDays(3);
            var viernes = lunes.AddDays(4);

            // _logger.LogInformation(
            //     "BalDiurnoSem sem={Sem} lunes={Lunes}",
            //     semana + 1, lunes);

            foreach (var grupoId in blueprintsPorGrupo.Keys)
            {
                // 1. Detectar qué días/tipos tienen déficit diurno para saber si un día
                //    es "único" (solo Martes o solo Viernes tienen déficit en ese tipo).
                var fechasConDeficitPorTipo = new Dictionary<string, HashSet<DateOnly>>(StringComparer.OrdinalIgnoreCase);

                foreach (var (fecha, diaNorm) in new[]
                {
                    (lunes,     "Lunes"),
                    (martes,    "Martes"),
                    // Miércoles NO se incluye: es solo día fuente, nunca día destino de déficit.
                    // Si estuviera aquí, un déficit en Miércoles inflaría el Count y rompería
                    // la condición "Solo Martes" / "Solo Viernes" (Count == 1).
                    (jueves,    "Jueves"),
                    (viernes,   "Viernes"),
                })
                {
                    var slots = bpSlots
                        .Where(kvp =>
                            string.Equals(kvp.Key.grupoId, grupoId, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(kvp.Key.dia, diaNorm, StringComparison.OrdinalIgnoreCase) &&
                            !turnosNocturnos.Contains(kvp.Key.tipoTurnoId) &&
                            kvp.Value > 0)
                        .ToList();

                    // _logger.LogInformation(
                    //     "BalDiurnoSem  chk {Dia}({Fecha}) grupo={GId}: slots_diurnos_con_min={SlotCount}",
                    //     diaNorm, fecha, grupoId, slots.Count);

                    foreach (var slot in slots)
                    {
                        // Suma de mínimos de TODOS los grupos para este (día, tipoTurno)
                        int sumaMinSlot = bpSlots
                            .Where(kvp =>
                                string.Equals(kvp.Key.dia, diaNorm, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(kvp.Key.tipoTurnoId, slot.Key.tipoTurnoId, StringComparison.OrdinalIgnoreCase) &&
                                !turnosNocturnos.Contains(kvp.Key.tipoTurnoId) &&
                                kvp.Value > 0)
                            .Sum(kvp => kvp.Value);
                        // Total asignados de TODOS los grupos para este (fecha, tipoTurno)
                        var personasEnSlot = result
                            .Where(p =>
                                p.FechaTurno == fecha &&
                                string.Equals(p.TipoTurnoId, slot.Key.tipoTurnoId, StringComparison.OrdinalIgnoreCase))
                            .Select(p => p.PersonaId)
                            .ToList();
                        int totalAsignados = personasEnSlot.Count;

                        // _logger.LogInformation(
                        //     "BalDiurnoSem    slot tipo={Tipo} sumaMin={Min} totalAsig={Asig} deficit={Def} personas=[{Personas}]",
                        //     slot.Key.tipoTurnoId, sumaMinSlot, totalAsignados, sumaMinSlot - totalAsignados,
                        //     string.Join(",", personasEnSlot));

                        if (sumaMinSlot - totalAsignados <= 0) continue;

                        if (!fechasConDeficitPorTipo.TryGetValue(slot.Key.tipoTurnoId, out var dias))
                        {
                            dias = new HashSet<DateOnly>();
                            fechasConDeficitPorTipo[slot.Key.tipoTurnoId] = dias;
                        }
                        dias.Add(fecha);
                    }
                }

                if (!fechasConDeficitPorTipo.Any())
                {
                    _logger.LogInformation(
                        "BalDiurnoSem  grupo={GId} sem={Sem}: sin déficit diurno, se omite",
                        grupoId, semana + 1);
                    continue;
                }

                // _logger.LogInformation(
                //     "BalDiurnoSem  grupo={GId} sem={Sem}: déficits detectados tipos=[{Tipos}]",
                //     grupoId, semana + 1,
                //     string.Join(",", fechasConDeficitPorTipo.Select(kv =>
                //         $"{kv.Key}:[{string.Join(",", kv.Value.Select(f => f.DayOfWeek.ToString()[..3]))}]")));

                // 2. Procesar cada tipo con déficit
                foreach (var (tipoTurnoId, fechasConDeficit) in fechasConDeficitPorTipo)
                {
                    foreach (var fecha in fechasConDeficit.OrderBy(f => f))
                    {
                        // Recalcular déficit actual cross-grupo (previos pasos pueden haber reducido el deficit)
                        var diaNormRecalc = NormalizarNombreDia(ObtenerNombreDia(fecha.DayOfWeek));
                        int sumaMinRecalc = bpSlots
                            .Where(kvp =>
                                string.Equals(kvp.Key.dia, diaNormRecalc, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(kvp.Key.tipoTurnoId, tipoTurnoId, StringComparison.OrdinalIgnoreCase) &&
                                kvp.Value > 0)
                            .Sum(kvp => kvp.Value);
                        if (sumaMinRecalc <= 0) continue;
                        int asignadosActuales = result.Count(p =>
                            p.FechaTurno == fecha &&
                            string.Equals(p.TipoTurnoId, tipoTurnoId, StringComparison.OrdinalIgnoreCase));
                        int deficit = sumaMinRecalc - asignadosActuales;

                        _logger.LogInformation(
                            "BalDiurnoSem  procesando tipo={Tipo} dia={Dia}({Fecha}) sumaMin={Min} totalAsig={Asig} deficit={Def} diasEnTipo={DiasCount}",
                            tipoTurnoId, fecha.DayOfWeek, fecha, sumaMinRecalc, asignadosActuales, deficit, fechasConDeficit.Count);

                        if (deficit <= 0) continue;

                        // Regla actual: el déficit diurno se cubre con personas del mismo día,
                        // convirtiendo otro turno diurno (ej: mañana -> tarde) dentro de esa fecha.
                        var diasFuente = new List<DateOnly> { fecha };
                        const string reglaSrc = "MismoDia→ConversionDiurna";

                        _logger.LogInformation(
                            "BalDiurnoSem    regla={Regla} diasFuente=[{DiasFuente}]",
                            reglaSrc, string.Join(",", diasFuente));

                        if (!diasFuente.Any()) continue;

                        foreach (var diaFuente in diasFuente)
                        {
                            if (deficit <= 0) break;

                            // Buscar candidatos con cualquier turno diurno en el mismo día,
                            // excluyendo el propio tipo con déficit para que la conversión sea real
                            // (ej: mañana -> tarde, tarde -> mañana).
                            var candidatos = result
                                .Where(p =>
                                    string.Equals(p.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase) &&
                                    p.FechaTurno == diaFuente &&
                                    !turnosNocturnos.Contains(p.TipoTurnoId) &&
                                    !string.Equals(p.TipoTurnoId, tipoTurnoId, StringComparison.OrdinalIgnoreCase))
                                .ToList();

                            _logger.LogInformation(
                                "BalDiurnoSem    diaFuente={DiaFuente} candidatos={Count} [{Personas}]",
                                diaFuente, candidatos.Count,
                                string.Join(",", candidatos.Select(c => c.PersonaId)));

                            foreach (var candidato in candidatos)
                            {
                                if (deficit <= 0) break;

                                // En balanceo de mismo día se permite reemplazar el turno actual del candidato,
                                // pero no si ya tiene además otro turno distinto en esa misma fecha.
                                bool tieneOtroTurnoEnElDia = result.Any(p =>
                                    string.Equals(p.PersonaId, candidato.PersonaId, StringComparison.OrdinalIgnoreCase) &&
                                    p.FechaTurno == fecha &&
                                    !string.Equals(p.TipoTurnoId, candidato.TipoTurnoId, StringComparison.OrdinalIgnoreCase));

                                if (tieneOtroTurnoEnElDia)
                                {
                                    _logger.LogInformation(
                                        "BalDiurnoSem      SKIP {PId}: ya tiene otro turno en {Dia}",
                                        candidato.PersonaId, fecha);
                                    continue;
                                }

                                if (EstaEnVacacionesBp(candidato.PersonaId, fecha, vacMap))
                                {
                                    _logger.LogInformation(
                                        "BalDiurnoSem      SKIP {PId}: vacaciones en {Dia}",
                                        candidato.PersonaId, fecha);
                                    continue;
                                }

                                result.Remove(candidato);
                                result.Add(new TurnoGeneradoPreview(candidato.PersonaId, tipoTurnoId, grupoId, fecha));
                                deficit--;

                                _logger.LogInformation(
                                    "BalDiurnoSem: MOVIDO {PId} {Tipo} {DiaFuente} → {DiaDeficit} grupo={GId}",
                                    candidato.PersonaId, tipoTurnoId, diaFuente, fecha, grupoId);
                            }
                        }

                        if (deficit > 0)
                            _logger.LogWarning(
                                "BalDiurnoSem: déficit residual={Def} tipo={Tipo} {Dia} grupo={GId}",
                                deficit, tipoTurnoId, fecha, grupoId);
                    }
                }
            }
        }

        _logger.LogInformation("BalDiurnoSem FIN");
        return result;
    }

    private async Task<List<HistorialSemanaBlueprint>> ObtenerHistorialBlueprintPorSemanasAsync(
        IReadOnlyCollection<string> gruposIds,
        IReadOnlyDictionary<string, List<Persona>> personasPorGrupo,
        DateTime fechaInicio)
    {
        if (gruposIds.Count == 0)
        {
            return new List<HistorialSemanaBlueprint>();
        }

        // Calcular el inicio de la semana de fechaInicio (lunes anterior o el mismo día si es lunes)
        var inicioSemanaReferencia = ObtenerInicioSemana(DateOnly.FromDateTime(fechaInicio.Date));

        _logger.LogInformation(
            "Inicio análisis historial Blueprint: fechaInicio={FechaInicio}, semanaReferencia={SemanaRef}, grupos={Grupos}",
            fechaInicio.Date,
            inicioSemanaReferencia,
            string.Join(", ", gruposIds));

        var semanasHistorialPorGrupo = gruposIds.ToDictionary(
            grupoId => grupoId,
            grupoId =>
            {
                if (!personasPorGrupo.TryGetValue(grupoId, out var personas) || personas.Count == 0)
                {
                    return 1;
                }

                return personas.Count;
            },
            StringComparer.OrdinalIgnoreCase);

        // Log ventanas por grupo
        foreach (var kvp in semanasHistorialPorGrupo)
        {
            var fechaCorte = inicioSemanaReferencia.AddDays(-7 * kvp.Value);
            _logger.LogInformation(
                "Ventana grupo {GrupoId}: personas={Personas}, semanas={Semanas}, desde={Desde}",
                kvp.Key,
                personasPorGrupo.TryGetValue(kvp.Key, out var p) ? p.Count : 0,
                kvp.Value,
                fechaCorte);
        }

        var maxSemanasHistorial = semanasHistorialPorGrupo.Values.Max();

        // Calcular fechas de inicio/fin asegurando semanas completas
        var fechaDesdeGlobal = inicioSemanaReferencia.AddDays(-7 * maxSemanasHistorial);
        var fechaHastaGlobal = inicioSemanaReferencia; // Excluye la semana de inicio

        _logger.LogInformation(
            "Consulta SQL: desde={Desde} hasta(excl)={Hasta}, ventanaMax={Semanas} semanas",
            fechaDesdeGlobal,
            fechaHastaGlobal,
            maxSemanasHistorial);

        // Consulta sin filtro Contains para debug
        var totalRegistrosGrupos = await _db.RegistroTurnos
            .AsNoTracking()
            .Where(rt => rt.GrupoId != null && gruposIds.Contains(rt.GrupoId))
            .CountAsync();

        _logger.LogInformation("Total RegistroTurnos en estos grupos (sin filtro fecha): {Count}", totalRegistrosGrupos);

        var historialBase = await _db.RegistroTurnos
            .AsNoTracking()
            .Include(rt => rt.Persona)
            .Include(rt => rt.TipoTurno)
            .Include(rt => rt.Grupo)
            .Where(rt =>
                rt.GrupoId != null &&
                gruposIds.Contains(rt.GrupoId) &&
                rt.FechaTurno >= fechaDesdeGlobal &&
                rt.FechaTurno < fechaHastaGlobal)
            .OrderBy(rt => rt.FechaTurno)
            .ThenBy(rt => rt.GrupoId)
            .ThenBy(rt => rt.PersonaId)
            .ThenBy(rt => rt.TipoTurnoId)
            .ToListAsync();

        _logger.LogInformation("Registros recuperados de BD: {Count}", historialBase.Count);

        // Log por grupo de los registros base
        var registrosPorGrupo = historialBase.GroupBy(r => r.GrupoId ?? "NULL").ToList();
        foreach (var grupoReg in registrosPorGrupo)
        {
            _logger.LogInformation(
                "  Grupo {GrupoId}: {Count} registros (fechas: {Min} a {Max})",
                grupoReg.Key,
                grupoReg.Count(),
                grupoReg.Min(r => r.FechaTurno),
                grupoReg.Max(r => r.FechaTurno));
        }

        // Filtrar por cada grupo según su ventana específica (N semanas completas)
        var registrosFiltrados = historialBase
            .Where(rt =>
            {
                if (string.IsNullOrWhiteSpace(rt.GrupoId))
                {
                    return false;
                }

                if (!semanasHistorialPorGrupo.TryGetValue(rt.GrupoId, out var semanasGrupo))
                {
                    return false;
                }

                var fechaCorteGrupo = inicioSemanaReferencia.AddDays(-7 * semanasGrupo);
                return rt.FechaTurno >= fechaCorteGrupo;
            })
            .ToList();

        _logger.LogInformation("Registros después de filtrado por ventana individual: {Count}", registrosFiltrados.Count);

        var historialSemanal = registrosFiltrados
            .GroupBy(rt => ObtenerInicioSemana(rt.FechaTurno))
            .OrderBy(g => g.Key)
            .Select(semanaGroup =>
            {
                var gruposSemana = semanaGroup
                    .GroupBy(rt => rt.GrupoId!, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g.Key)
                    .Select(grupoGroup =>
                    {
                        var nombreGrupo = grupoGroup
                            .Select(r => r.Grupo?.NombreGrupo)
                            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                            ?? grupoGroup.Key;

                        var personasGrupo = grupoGroup
                            .GroupBy(rt => rt.PersonaId)
                            .OrderBy(g => g.Key)
                            .Select(personaGroup =>
                            {
                                var nombrePersona = personaGroup
                                    .Select(r => r.Persona?.Nombre)
                                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                                    ?? personaGroup.Key;

                                var turnosPersona = personaGroup
                                    .OrderBy(r => r.FechaTurno)
                                    .ThenBy(r => r.TipoTurnoId)
                                    .Select(r => new HistorialTurnoAsignadoBlueprint(
                                        r.TurnoId,
                                        r.FechaTurno,
                                        r.TipoTurnoId,
                                        r.TipoTurno?.NombreTurno,
                                        r.EsFeriado,
                                        r.NoLaboradoPorFeriado,
                                        r.TipoTurno?.HoraInicio ?? TimeOnly.MinValue,
                                        r.TipoTurno?.HoraFin ?? TimeOnly.MinValue))
                                    .ToList();

                                return new HistorialPersonaBlueprint(
                                    personaGroup.Key,
                                    nombrePersona,
                                    turnosPersona);
                            })
                            .ToList();

                        return new HistorialGrupoSemanaBlueprint(
                            grupoGroup.Key,
                            nombreGrupo,
                            semanasHistorialPorGrupo[grupoGroup.Key],
                            personasGrupo);
                    })
                    .ToList();

                var inicioSemana = semanaGroup.Key;
                var inicioSemanaDateTime = inicioSemana.ToDateTime(TimeOnly.MinValue);

                return new HistorialSemanaBlueprint(
                    inicioSemana,
                    System.Globalization.ISOWeek.GetWeekOfYear(inicioSemanaDateTime),
                    System.Globalization.ISOWeek.GetYear(inicioSemanaDateTime),
                    gruposSemana);
            })
            .ToList();

        // Log detallado por grupo
        foreach (var grupoId in gruposIds)
        {
            var turnosGrupo = historialSemanal
                .SelectMany(s => s.Grupos)
                .Where(g => string.Equals(g.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase))
                .SelectMany(g => g.Personas)
                .Sum(p => p.Turnos.Count);

            var semanasGrupo = historialSemanal
                .Count(s => s.Grupos.Any(g => string.Equals(g.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase)));

            _logger.LogDebug(
                "Historial grupo {GrupoId}: semanas={Semanas}, ventana={Ventana}, turnos={Turnos}",
                grupoId,
                semanasGrupo,
                semanasHistorialPorGrupo[grupoId],
                turnosGrupo);
        }

        return historialSemanal;
    }

    private static Dictionary<string, List<AsignacionPatronPersonaHistorial>> AnalizarPatronesPorNSemanas(
        IReadOnlyList<HistorialSemanaBlueprint> historialSemanal,
        IReadOnlyDictionary<string, List<PatronTurnos>> patronesPorGrupo,
        double umbralCoincidencia)
    {
        var acumulado = new Dictionary<string, Dictionary<string, AsignacionPatronPersonaHistorial>>(StringComparer.OrdinalIgnoreCase);
        var patronesUsadosPorPersona = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);

        if (historialSemanal.Count == 0)
        {
            return new Dictionary<string, List<AsignacionPatronPersonaHistorial>>(StringComparer.OrdinalIgnoreCase);
        }

        var semanasOrdenadas = historialSemanal
            .OrderBy(s => s.SemanaInicio)
            .ToList();

        foreach (var semana in semanasOrdenadas)
        {
            foreach (var grupoSemana in semana.Grupos)
            {
                if (!acumulado.TryGetValue(grupoSemana.GrupoId, out var personasAcumuladas))
                {
                    personasAcumuladas = new Dictionary<string, AsignacionPatronPersonaHistorial>(StringComparer.OrdinalIgnoreCase);
                    acumulado[grupoSemana.GrupoId] = personasAcumuladas;
                }

                if (!patronesUsadosPorPersona.TryGetValue(grupoSemana.GrupoId, out var usadosPorPersona))
                {
                    usadosPorPersona = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                    patronesUsadosPorPersona[grupoSemana.GrupoId] = usadosPorPersona;
                }

                if (!patronesPorGrupo.TryGetValue(grupoSemana.GrupoId, out var patronesGrupo) || patronesGrupo.Count == 0)
                {
                    foreach (var persona in grupoSemana.Personas)
                    {
                        if (!personasAcumuladas.TryGetValue(persona.PersonaId, out var historialPersona))
                        {
                            historialPersona = new AsignacionPatronPersonaHistorial(
                                persona.PersonaId,
                                persona.NombrePersona,
                                new List<AsignacionPatronSemana>());
                            personasAcumuladas[persona.PersonaId] = historialPersona;
                        }

                        historialPersona.PatronesPorSemana.Add(new AsignacionPatronSemana(
                            semana.SemanaInicio,
                            semana.NumeroSemanaIso,
                            semana.AnioSemanaIso,
                            null,
                            0));
                    }

                    continue;
                }

                var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var candidatos = new List<CandidatoAsignacionPatron>();

                foreach (var persona in grupoSemana.Personas)
                {
                    var slotsPersona = persona.Turnos
                        .Select(t => (
                            Dia: NormalizarNombreDia(ObtenerNombreDia(t.FechaTurno.DayOfWeek)),
                             t.TipoTurnoId))
                        .ToHashSet();

                    foreach (var patron in patronesGrupo)
                    {
                        var slotsPatron = patron.DiasTrabajo
                            .Select(d => (
                                Dia: NormalizarNombreDia(d.Dia),
                                TipoTurnoId: d.TipoHorario))
                            .ToHashSet();

                        if (slotsPatron.Count == 0)
                        {
                            continue;
                        }

                        var coincidencias = slotsPatron.Count(slotsPersona.Contains);
                        var baseComparacion = Math.Max(slotsPatron.Count, slotsPersona.Count);
                        //comparar el los registros de turnos asignados a la persona en la semana con el patrón, calculando un porcentaje de similitud basado 
                        // en la cantidad de coincidencias dividido por la cantidad de días en el patrón ó en la cantidad de turnos asignados a la persona "el q sea mayor"
                        var similitud = baseComparacion == 0 ? 0 : (double)coincidencias / baseComparacion;

                        if (similitud >= umbralCoincidencia)
                        {
                            candidatos.Add(new CandidatoAsignacionPatron(
                                persona.PersonaId,
                                persona.NombrePersona,
                                patron.Nombre,
                                similitud));
                        }
                    }
                }

                var asignacionesSemana = grupoSemana.Personas
                    .Select(p => new AsignacionPatronSemanaTemp(
                        p.PersonaId,
                        p.NombrePersona,
                        null,
                        0))
                    .ToDictionary(x => x.PersonaId, StringComparer.OrdinalIgnoreCase);

                foreach (var candidato in candidatos
                    .OrderByDescending(c => c.Similitud)
                    .ThenBy(c => c.PersonaId)
                    .ThenBy(c => c.PatronNombre))
                {
                    if (usados.Contains(candidato.PatronNombre))
                    {
                        continue;
                    }

                    if (!usadosPorPersona.TryGetValue(candidato.PersonaId, out var patronesPersona))
                    {
                        patronesPersona = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        usadosPorPersona[candidato.PersonaId] = patronesPersona;
                    }

                    // Evita repetir en el historial el mismo patrón para la misma persona.
                    if (patronesPersona.Contains(candidato.PatronNombre))
                    {
                        continue;
                    }

                    if (asignacionesSemana.TryGetValue(candidato.PersonaId, out var actual) && actual.PatronNombre == null)
                    {
                        asignacionesSemana[candidato.PersonaId] = new AsignacionPatronSemanaTemp(
                            candidato.PersonaId,
                            candidato.NombrePersona,
                            candidato.PatronNombre,
                            candidato.Similitud);

                        patronesPersona.Add(candidato.PatronNombre);
                        usados.Add(candidato.PatronNombre);
                    }
                }

                foreach (var personaSemana in asignacionesSemana.Values)
                {
                    if (!personasAcumuladas.TryGetValue(personaSemana.PersonaId, out var historialPersona))
                    {
                        historialPersona = new AsignacionPatronPersonaHistorial(
                            personaSemana.PersonaId,
                            personaSemana.NombrePersona,
                            new List<AsignacionPatronSemana>());
                        personasAcumuladas[personaSemana.PersonaId] = historialPersona;
                    }

                    historialPersona.PatronesPorSemana.Add(new AsignacionPatronSemana(
                        semana.SemanaInicio,
                        semana.NumeroSemanaIso,
                        semana.AnioSemanaIso,
                        personaSemana.PatronNombre,
                        personaSemana.Similitud));
                }
            }
        }

        var resultado = acumulado.ToDictionary(
            g => g.Key,
            g => g.Value.Values
                .OrderBy(v => v.PersonaId)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);

        return resultado;
    }

    private static Dictionary<string, Dictionary<string, List<HistorialPatronSemana>>> ConvertirHistorialPatronesParaAlgoritmo(
        IReadOnlyDictionary<string, List<AsignacionPatronPersonaHistorial>> asignacionesPorGrupo)
    {
        var resultado = new Dictionary<string, Dictionary<string, List<HistorialPatronSemana>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var grupo in asignacionesPorGrupo)
        {
            var porPersona = new Dictionary<string, List<HistorialPatronSemana>>(StringComparer.OrdinalIgnoreCase);

            foreach (var persona in grupo.Value)
            {
                var semanas = persona.PatronesPorSemana
                    .OrderBy(s => s.SemanaInicio)
                    .Select(s => new HistorialPatronSemana
                    {
                        SemanaInicio = s.SemanaInicio,
                        NumeroSemanaIso = s.NumeroSemanaIso,
                        AnioSemanaIso = s.AnioSemanaIso,
                        PatronNombre = s.PatronNombre,
                        Similitud = s.Similitud
                    })
                    .ToList();

                porPersona[persona.PersonaId] = semanas;
            }

            resultado[grupo.Key] = porPersona;
        }

        return resultado;
    }

    private static string ObtenerNombreDia(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "Lunes",
            DayOfWeek.Tuesday => "Martes",
            DayOfWeek.Wednesday => "Miércoles",
            DayOfWeek.Thursday => "Jueves",
            DayOfWeek.Friday => "Viernes",
            DayOfWeek.Saturday => "Sábado",
            DayOfWeek.Sunday => "Domingo",
            _ => string.Empty
        };
    }

    private static DateOnly ObtenerInicioSemana(DateOnly fecha)
    {
        var offset = ((int)fecha.DayOfWeek + 6) % 7;
        return fecha.AddDays(-offset);
    }

    private sealed record HistorialSemanaBlueprint(
        DateOnly SemanaInicio,
        int NumeroSemanaIso,
        int AnioSemanaIso,
        List<HistorialGrupoSemanaBlueprint> Grupos);

    private sealed record HistorialGrupoSemanaBlueprint(
        string GrupoId,
        string NombreGrupo,
        int SemanasHistorial,
        List<HistorialPersonaBlueprint> Personas);

    private sealed record HistorialPersonaBlueprint(
        string PersonaId,
        string NombrePersona,
        List<HistorialTurnoAsignadoBlueprint> Turnos);

    private sealed record HistorialTurnoAsignadoBlueprint(
        string TurnoId,
        DateOnly FechaTurno,
        string TipoTurnoId,
        string? NombreTipoTurno,
        bool EsFeriado,
        bool NoLaboradoPorFeriado,
        TimeOnly HoraInicio,
        TimeOnly HoraFin);

    private sealed record CandidatoAsignacionPatron(
        string PersonaId,
        string NombrePersona,
        string PatronNombre,
        double Similitud);

    private sealed record AsignacionPatronSemanaTemp(
        string PersonaId,
        string NombrePersona,
        string? PatronNombre,
        double Similitud);

    private sealed record AsignacionPatronSemana(
        DateOnly SemanaInicio,
        int NumeroSemanaIso,
        int AnioSemanaIso,
        string? PatronNombre,
        double Similitud);

    private sealed record AsignacionPatronPersonaHistorial(
        string PersonaId,
        string NombrePersona,
        List<AsignacionPatronSemana> PatronesPorSemana);



    private static bool EsTurnoNocturno(Turno turno)
    {
        var inicio = turno.Inicio;
        var fin = turno.Fin;
        if (fin <= inicio)
            return false;

        DateTime ventanaInicio;
        DateTime ventanaFin;
        if (inicio.TimeOfDay < TimeSpan.FromHours(8))
        {
            ventanaInicio = inicio.Date.AddDays(-1).AddHours(20);
            ventanaFin = inicio.Date.AddHours(8);
        }
        else
        {
            ventanaInicio = inicio.Date.AddHours(20);
            ventanaFin = inicio.Date.AddDays(1).AddHours(8);
        }

        return inicio >= ventanaInicio && fin <= ventanaFin;
    }

    private static bool PuedeCubrirGrupo(PersonaTurno persona, string grupoId)
    {
        if (string.IsNullOrWhiteSpace(grupoId))
            return false;

        if (string.Equals(persona.GrupoId, grupoId, StringComparison.OrdinalIgnoreCase))
            return true;

        return persona.GruposSecundarios.Contains(grupoId);
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
                break;
            }
        }

        return result;
    }

    private async Task<ConfiguracionVisibilidadFeriado?> ConstruirConfiguracionVisibilidadFeriadoAsync(string equipoId)
    {
        if (string.IsNullOrWhiteSpace(equipoId))
        {
            return null;
        }

        var filas = await _db.FeriadoCoberturaConfigs
            .AsNoTracking()
            .Where(configuracion => configuracion.EquipoId == equipoId)
            .ToListAsync();

        if (filas.Count == 0)
        {
            return null;
        }

        return new ConfiguracionVisibilidadFeriado
        {
            EquipoId = equipoId,
            Coberturas = filas
                .Where(fila => !string.IsNullOrWhiteSpace(fila.GrupoId) && !string.IsNullOrWhiteSpace(fila.TipoTurnoId))
                .Select(fila => new CoberturaVisibilidadFeriado
                {
                    Id = $"{fila.GrupoId}|{fila.TipoTurnoId}",
                    GruposIncluidos = new HashSet<string>([fila.GrupoId], StringComparer.OrdinalIgnoreCase),
                    TiposTurnoIds = new HashSet<string>([fila.TipoTurnoId], StringComparer.OrdinalIgnoreCase),
                    PersonasVisibles = Math.Max(0, fila.CantidadVisible)
                })
                .ToList()
        };
    }

    private ProblemaRotacion AplicarCoberturaReducidaFeriados(
        ProblemaRotacion problema,
        ConfiguracionVisibilidadFeriado? configuracion)
    {
        if (configuracion is null || configuracion.Coberturas.Count == 0 || problema.Feriados.Count == 0)
        {
            return problema;
        }

        var slots = problema.Slots
            .Select(slot =>
            {
                if (!problema.Feriados.Contains(slot.Fecha) || !EsFeriadoLaborable(slot.Fecha) || slot.EsAuxiliar)
                {
                    return slot;
                }

                var requeridos = CalcularCoberturaRequeridaFeriado(slot, configuracion);
                requeridos = Math.Max(0, requeridos);
                _logger.LogInformation(
                    "RotacionV2 feriado cobertura reducida: fecha={Fecha} grupo={GrupoId} tipo={TipoTurnoId} slot={SlotId} originalReq={OriginalReq} originalCap={OriginalCap} configReq={ConfigReq} finalReq={FinalReq}",
                    slot.Fecha,
                    slot.GrupoId,
                    slot.TipoTurnoId,
                    slot.Id,
                    slot.EmpleadosRequeridos,
                    slot.CapacidadPlanificada,
                    requeridos,
                    requeridos);

                return slot with
                {
                    EmpleadosRequeridos = requeridos,
                    CapacidadPlanificada = requeridos,
                    MaximoApoyoCedible = Math.Min(slot.MaximoApoyoCedible, requeridos)
                };
            })
            .ToList();

        foreach (var fecha in problema.Feriados.Where(EsFeriadoLaborable).OrderBy(fecha => fecha))
        {
            foreach (var cobertura in configuracion.Coberturas)
            {
                var slotsCoincidentes = slots
                    .Where(slot =>
                        !slot.EsAuxiliar &&
                        slot.Fecha == fecha &&
                        CoincideCoberturaFeriado(slot, cobertura))
                    .ToArray();

                _logger.LogInformation(
                    "RotacionV2 feriado cobertura config: fecha={Fecha} cobertura={CoberturaId} grupos=[{Grupos}] tipos=[{Tipos}] personasConfig={PersonasConfig} slotsCoincidentes={SlotsCoincidentes} requeridoFinalTotal={RequeridoFinalTotal}",
                    fecha,
                    cobertura.Id,
                    string.Join(",", cobertura.GruposIncluidos),
                    string.Join(",", cobertura.TiposTurnoIds),
                    cobertura.PersonasVisibles,
                    slotsCoincidentes.Length,
                    slotsCoincidentes.Sum(slot => slot.EmpleadosRequeridos));
            }
        }

        return problema with { Slots = slots };
    }

    private static bool TryConstruirSolicitudSobrecupoFeriado(ProblemaRotacion problema, out string mensaje)
    {
        var detalles = new List<string>();

        for (var indiceSemana = 0; indiceSemana < problema.CantidadSemanas; indiceSemana++)
        {
            var fechaInicioSemana = problema.FechaInicio.AddDays(indiceSemana * 7);
            var fechaFinSemana = fechaInicioSemana.AddDays(6);
            var feriadosLaborables = problema.Feriados
                .Where(fecha =>
                    fecha >= fechaInicioSemana &&
                    fecha <= fechaFinSemana &&
                    CalculadoraCreditoFeriado.EsFeriadoLaborable(problema, fecha))
                .OrderBy(fecha => fecha)
                .ToArray();

            if (feriadosLaborables.Length == 0)
            {
                continue;
            }

            var slotsNoFeriado = problema.Slots
                .Where(slot =>
                    slot.IndiceSemana == indiceSemana &&
                    !slot.EsAuxiliar &&
                    !CalculadoraCreditoFeriado.EsFeriadoLaborable(problema, slot.Fecha))
                .ToArray();

            if (slotsNoFeriado.Length == 0)
            {
                continue;
            }

            var minutosDistintos = slotsNoFeriado
                .Select(slot => slot.MinutosTrabajoComputables)
                .Distinct()
                .ToArray();

            if (minutosDistintos.Length != 1)
            {
                continue;
            }

            var minutosPorTurno = minutosDistintos[0];
            if (minutosPorTurno <= 0 || problema.Reglas.Obligatorias.MinutosObjetivoSemanales % minutosPorTurno != 0)
            {
                continue;
            }

            var turnosObjetivoBase = problema.Reglas.Obligatorias.MinutosObjetivoSemanales / minutosPorTurno;
            var objetivoAjustadoTotal = problema.Empleados.Sum(empleado =>
                CalculadoraObjetivoSemanal.CalcularTurnosObjetivo(
                    problema,
                    empleado,
                    indiceSemana,
                    turnosObjetivoBase));
            var objetivoBaseTotal = problema.Empleados.Count * turnosObjetivoBase;
            var requeridoNoFeriado = slotsNoFeriado.Sum(slot => slot.EmpleadosRequeridos);

            if (requeridoNoFeriado <= objetivoAjustadoTotal || requeridoNoFeriado > objetivoBaseTotal)
            {
                continue;
            }

            var excedente = requeridoNoFeriado - objetivoAjustadoTotal;
            detalles.Add(
                string.Join(Environment.NewLine,
                    $"Semana {indiceSemana + 1}",
                    $"Rango: {fechaInicioSemana:yyyy-MM-dd} - {fechaFinSemana:yyyy-MM-dd}",
                    $"Feriado: {string.Join(", ", feriadosLaborables.Select(fecha => fecha.ToString("yyyy-MM-dd")))}",
                    $"Necesidad: {excedente} turno(s) adicional(es) sobre el objetivo reducido ({requeridoNoFeriado} requeridos vs {objetivoAjustadoTotal} permitidos).",
                    $"Limite autorizado: hasta {turnosObjetivoBase} turno(s) por persona esa semana."));
        }

        if (detalles.Count == 0)
        {
            mensaje = string.Empty;
            return false;
        }

        mensaje =
            "La generacion detecto semana(s) con feriado donde la cobertura no cabe dentro del objetivo semanal reducido." +
            Environment.NewLine +
            Environment.NewLine +
            string.Join(Environment.NewLine + Environment.NewLine, detalles) +
            Environment.NewLine +
            Environment.NewLine +
            "Autoriza que algunas personas trabajen hasta el maximo semanal base para completar la cobertura?" +
            Environment.NewLine +
            "El solver mantendra el balance con historial de feriados, recargos y horas acumuladas.";
        return true;
    }

    private static NivelUsoDescanso7Horas ParseNivelUsoDescanso7Horas(string? nivel)
    {
        return (nivel ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "none" or "no_usar" or "no usar" => NivelUsoDescanso7Horas.NoUsar,
            "medium" or "medio" => NivelUsoDescanso7Horas.Medio,
            "high" or "alto" => NivelUsoDescanso7Horas.Alto,
            _ => NivelUsoDescanso7Horas.Bajo
        };
    }

    private static NivelEvitarFinesSemanaConsecutivos ParseNivelEvitarFinesSemanaConsecutivos(string? nivel)
    {
        return (nivel ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "none" or "no_usar" or "no usar" => NivelEvitarFinesSemanaConsecutivos.NoUsar,
            "medium" or "medio" => NivelEvitarFinesSemanaConsecutivos.Medio,
            "high" or "alto" => NivelEvitarFinesSemanaConsecutivos.Alto,
            _ => NivelEvitarFinesSemanaConsecutivos.Bajo
        };
    }

    private static int CalcularCoberturaRequeridaFeriado(
        SlotTurno slot,
        ConfiguracionVisibilidadFeriado configuracion)
    {
        return configuracion.Coberturas
            .Where(cobertura => CoincideCoberturaFeriado(slot, cobertura))
            .Sum(cobertura => Math.Max(0, cobertura.PersonasVisibles));
    }

    private static bool CoincideCoberturaFeriado(SlotTurno slot, CoberturaVisibilidadFeriado cobertura)
    {
        if (cobertura.TiposTurnoIds.Count > 0 && !cobertura.TiposTurnoIds.Contains(slot.TipoTurnoId))
        {
            return false;
        }

        if (cobertura.GruposIncluidos.Count > 0 && !cobertura.GruposIncluidos.Contains(slot.GrupoId))
        {
            return false;
        }

        return true;
    }

    private static bool EsFeriadoLaborable(DateOnly fecha)
    {
        return fecha.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }

    private static int CalcularMinutosTrabajoComputables(TimeOnly horaInicio, TimeOnly horaFin)
    {
        var inicio = FechaBaseCalculoTurno.ToDateTime(horaInicio);
        var fin = FechaBaseCalculoTurno.ToDateTime(horaFin);
        if (fin <= inicio)
        {
            fin = fin.AddDays(1);
        }

        var minutos = (int)Math.Round((fin - inicio).TotalMinutes, MidpointRounding.AwayFromZero);
        return minutos >= 9 * 60 ? minutos - 60 : minutos;
    }

    private static int ResolverMinutosPorTurnoBase(
        IReadOnlyDictionary<string, TipoTurno> tiposTurno,
        IEnumerable<string> tipoTurnoIds)
    {
        var minutosDistintos = tipoTurnoIds
            .Where(tipoTurnoId => !string.IsNullOrWhiteSpace(tipoTurnoId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(tipoTurnoId => tiposTurno.TryGetValue(tipoTurnoId, out var tipoTurno)
                ? CalcularMinutosTrabajoComputables(tipoTurno.HoraInicio, tipoTurno.HoraFin)
                : 0)
            .Where(minutos => minutos > 0)
            .Distinct()
            .ToList();

        return minutosDistintos.Count == 1
            ? minutosDistintos[0]
            : 8 * 60;
    }

    private static int ResolverTurnosObjetivoSemanales(int minutosObjetivoSemanales, int minutosPorTurnoBase)
    {
        var divisor = Math.Max(1, minutosPorTurnoBase);
        return Math.Max(1, minutosObjetivoSemanales / divisor);
    }

    private static bool PuedeUsarPersonaUnicaPorSemana(IEnumerable<(string TipoTurnoId, int NumeroPersonas)> planificaciones)
    {
        var items = planificaciones
            .Where(item => item.NumeroPersonas > 0)
            .ToList();

        return items.Count > 0 &&
               items.Select(item => item.TipoTurnoId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1 &&
               items.Sum(item => item.NumeroPersonas) == 5 &&
               items.All(item => item.NumeroPersonas <= 1);
    }

    private Dictionary<string, ConfigGrupoEspecialSecundarios> ResolverConfiguracionesGruposEspeciales(
        IReadOnlyDictionary<string, List<Planificacion>> planificacionesPorGrupo,
        IReadOnlyDictionary<string, Grupo> gruposPorId)
    {
        var resultado = new Dictionary<string, ConfigGrupoEspecialSecundarios>(StringComparer.OrdinalIgnoreCase);
        foreach (var (grupoId, planificaciones) in planificacionesPorGrupo)
        {
            var config = planificaciones
                .Where(planificacion => !planificacion.IsAuxiliar)
                .OrderBy(planificacion => ObtenerIndiceDiaSemana(planificacion.Dia))
                .ThenBy(planificacion => planificacion.TipoTurnoId)
                .FirstOrDefault();

            if (config?.UsaSoloSecundarios != true)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(config.GrupoFuenteSecundariosId))
            {
                resultado[grupoId] = new ConfigGrupoEspecialSecundarios(grupoId, string.Empty, false);
                continue;
            }

            var usarPersonaUnica = config.UsarPersonaUnicaPorSemana &&
                PuedeUsarPersonaUnicaPorSemana(planificaciones
                    .Where(planificacion => !planificacion.IsAuxiliar)
                    .Select(planificacion => (planificacion.TipoTurnoId, planificacion.NumeroPersonas)));

            resultado[grupoId] = new ConfigGrupoEspecialSecundarios(
                grupoId,
                config.GrupoFuenteSecundariosId,
                usarPersonaUnica);
        }

        foreach (var config in resultado.Values)
        {
            _logger.LogInformation(
                "Planificacion grupo especial detectado: especial={GrupoEspecial}, fuente={GrupoFuente}, personaUnicaSemana={PersonaUnica}",
                DescribirGrupo(config.GrupoEspecialId, gruposPorId),
                string.IsNullOrWhiteSpace(config.GrupoFuenteId) ? "sin fuente" : DescribirGrupo(config.GrupoFuenteId, gruposPorId),
                config.UsarPersonaUnicaPorSemana);
        }

        return resultado;
    }

    private static Result AplicarReduccionesGruposEspeciales(
        Dictionary<string, List<Planificacion>> planificacionesPorGrupo,
        IReadOnlyDictionary<string, ConfigGrupoEspecialSecundarios> configuracionesEspeciales,
        IReadOnlyDictionary<string, Grupo> gruposPorId)
    {
        foreach (var config in configuracionesEspeciales.Values)
        {
            if (string.IsNullOrWhiteSpace(config.GrupoFuenteId))
            {
                return Result.Fail($"El {DescribirGrupo(config.GrupoEspecialId, gruposPorId)} esta configurado para usar secundarios, pero no tiene grupo fuente.");
            }

            if (!planificacionesPorGrupo.ContainsKey(config.GrupoFuenteId))
            {
                return Result.Fail($"El grupo fuente {DescribirGrupo(config.GrupoFuenteId, gruposPorId)} del {DescribirGrupo(config.GrupoEspecialId, gruposPorId)} debe estar incluido en la generacion.");
            }

            if (configuracionesEspeciales.ContainsKey(config.GrupoFuenteId))
            {
                return Result.Fail($"El grupo fuente {DescribirGrupo(config.GrupoFuenteId, gruposPorId)} debe ser un grupo normal, no otro grupo especial.");
            }

            foreach (var slotEspecial in planificacionesPorGrupo[config.GrupoEspecialId].Where(planificacion => !planificacion.IsAuxiliar && planificacion.NumeroPersonas > 0))
            {
                var slotFuente = planificacionesPorGrupo[config.GrupoFuenteId]
                    .FirstOrDefault(planificacion =>
                        !planificacion.IsAuxiliar &&
                        string.Equals(NormalizarNombreDia(planificacion.Dia), NormalizarNombreDia(slotEspecial.Dia), StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(planificacion.TipoTurnoId, slotEspecial.TipoTurnoId, StringComparison.OrdinalIgnoreCase));

                if (slotFuente == null || slotFuente.NumeroPersonas < slotEspecial.NumeroPersonas)
                {
                    return Result.Fail(
                        $"El {DescribirGrupo(config.GrupoEspecialId, gruposPorId)} requiere {slotEspecial.NumeroPersonas} persona(s) en {slotEspecial.Dia}/{slotEspecial.TipoTurnoId}, pero el grupo fuente {DescribirGrupo(config.GrupoFuenteId, gruposPorId)} no tiene cobertura suficiente para reducir en ese mismo turno.");
                }

                slotFuente.NumeroPersonas -= slotEspecial.NumeroPersonas;
            }
        }

        foreach (var grupoId in planificacionesPorGrupo.Keys.ToList())
        {
            planificacionesPorGrupo[grupoId] = planificacionesPorGrupo[grupoId]
                .Where(planificacion => planificacion.IsAuxiliar || planificacion.NumeroPersonas > 0)
                .ToList();
        }

        return Result.Ok();
    }

    private async Task<List<string>> ObtenerPersonaIdsElegiblesGrupoEspecialAsync(
        ConfigGrupoEspecialSecundarios config,
        IReadOnlyCollection<string> personaIdsActivas,
        IReadOnlyDictionary<string, Grupo> gruposPorId)
    {
        if (string.IsNullOrWhiteSpace(config.GrupoFuenteId))
        {
            return [];
        }

        var principalesFuente = _db.PersonaGrupos
            .AsNoTracking()
            .Where(pg => pg.GrupoId == config.GrupoFuenteId && pg.EsPrincipal);
        var secundariosEspecial = _db.PersonaGrupos
            .AsNoTracking()
            .Where(pg => pg.GrupoId == config.GrupoEspecialId && !pg.EsPrincipal);

        return await principalesFuente
            .Join(
                secundariosEspecial,
                principal => principal.PersonaId,
                secundario => secundario.PersonaId,
                (principal, _) => principal.PersonaId)
            .Where(personaId => personaIdsActivas.Contains(personaId))
            .Distinct()
            .ToListAsync();
    }

    private async Task<List<Persona>> ObtenerPersonasElegiblesGrupoEspecialAsync(
        ConfigGrupoEspecialSecundarios config,
        IReadOnlyCollection<string> personaIdsActivas,
        IReadOnlyDictionary<string, Grupo> gruposPorId)
    {
        var personaIds = await ObtenerPersonaIdsElegiblesGrupoEspecialAsync(config, personaIdsActivas, gruposPorId);
        if (personaIds.Count == 0)
        {
            return [];
        }

        return await _db.Personas
            .AsNoTracking()
            .Where(persona => personaIds.Contains(persona.PersonaId))
            .OrderBy(persona => persona.Nombre)
            .ThenBy(persona => persona.Apellido)
            .ToListAsync();
    }

    private static ReglasRotacion AplicarReglasGruposEspeciales(
        ReglasRotacion reglas,
        IReadOnlyDictionary<string, ConfigGrupoEspecialSecundarios> configuracionesEspeciales)
    {
        if (configuracionesEspeciales.Count == 0)
        {
            return reglas;
        }

        return reglas with
        {
            Configurables = reglas.Configurables with
            {
                GrupoFuentePorGrupoEspecial = configuracionesEspeciales.Values
                    .Where(config => !string.IsNullOrWhiteSpace(config.GrupoFuenteId))
                    .ToDictionary(
                        config => config.GrupoEspecialId,
                        config => config.GrupoFuenteId,
                        StringComparer.OrdinalIgnoreCase),
                GruposEspecialesPersonaUnicaPorSemana = configuracionesEspeciales.Values
                    .Where(config => config.UsarPersonaUnicaPorSemana)
                    .Select(config => config.GrupoEspecialId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
            }
        };
    }

    private sealed record ConfigGrupoEspecialSecundarios(
        string GrupoEspecialId,
        string GrupoFuenteId,
        bool UsarPersonaUnicaPorSemana);

    private async Task<Dictionary<string, HashSet<DateOnly>>> ObtenerVacacionesAprobadasPorPersonaAsync(
        IReadOnlyCollection<string> personaIds,
        DateOnly fechaInicio,
        DateOnly fechaFin)
    {
        var resultado = new Dictionary<string, HashSet<DateOnly>>(StringComparer.OrdinalIgnoreCase);
        if (personaIds.Count == 0 || fechaFin < fechaInicio)
        {
            return resultado;
        }

        var vacaciones = await (
            from vacacion in _db.Vacaciones.AsNoTracking()
            join solicitud in _db.Solicitudes.AsNoTracking() on vacacion.SolicitudId equals solicitud.SolicitudId
            where personaIds.Contains(solicitud.PersonaSolicitanteId)
                && solicitud.EstadoSolicitud == SolicitudEstado.AprobadoFinal
                && vacacion.FechaInicio <= fechaFin
                && vacacion.FechaFin >= fechaInicio
            select new
            {
                solicitud.PersonaSolicitanteId,
                vacacion.FechaInicio,
                vacacion.FechaFin
            })
            .ToListAsync();

        foreach (var vacacion in vacaciones)
        {
            if (!resultado.TryGetValue(vacacion.PersonaSolicitanteId, out var dias))
            {
                dias = new HashSet<DateOnly>();
                resultado[vacacion.PersonaSolicitanteId] = dias;
            }

            var inicio = vacacion.FechaInicio > fechaInicio ? vacacion.FechaInicio : fechaInicio;
            var fin = vacacion.FechaFin < fechaFin ? vacacion.FechaFin : fechaFin;

            for (var actual = inicio; actual <= fin; actual = actual.AddDays(1))
            {
                dias.Add(actual);
            }
        }

        if (resultado.Count > 0)
        {
            _logger.LogInformation(
                "Vacaciones detectadas para generacion rotativa: personas={Personas}, dias={Dias}, rango={Inicio}..{Fin}",
                resultado.Count,
                resultado.Sum(item => item.Value.Count),
                fechaInicio,
                fechaFin);
        }

        return resultado;
    }

    private async Task<List<string>> ObtenerPersonaIdsActivasAsync()
    {
        return await _db.Personas
            .AsNoTracking()
            .Where(persona => !persona.Borrado)
            .Select(persona => persona.PersonaId)
            .ToListAsync();
    }

    private async Task<HashSet<DateOnly>> ObtenerFeriadosEnRangoAsync(DateOnly fechaInicio, DateOnly fechaFin)
    {
        var resultado = new HashSet<DateOnly>();

        var feriados = await _db.Feriados
            .AsNoTracking()
            .Where(f => f.InicioFeriado <= fechaFin && f.FinFeriado >= fechaInicio)
            .Select(f => new { f.InicioFeriado, f.FinFeriado })
            .ToListAsync();

        foreach (var feriado in feriados)
        {
            var inicio = feriado.InicioFeriado > fechaInicio ? feriado.InicioFeriado : fechaInicio;
            var fin = feriado.FinFeriado < fechaFin ? feriado.FinFeriado : fechaFin;
            for (var actual = inicio; actual <= fin; actual = actual.AddDays(1))
            {
                resultado.Add(actual);
            }
        }

        return resultado;
    }

    private static int ObtenerIndiceDiaSemana(string dia)
    {
        return dia.Trim().ToLowerInvariant() switch
        {
            "lunes" => 0,
            "martes" => 1,
            "miercoles" or "miércoles" => 2,
            "jueves" => 3,
            "viernes" => 4,
            "sabado" or "sábado" => 5,
            "domingo" => 6,
            _ => 0
        };
    }

    private async Task<ReglasRotacion> ConstruirReglasRotacionDesdePlanificacionAsync(string equipoId, bool balancearHorasSemanales = true)
    {
        var permiteTurnosAuxiliares = !string.IsNullOrWhiteSpace(equipoId)
            && await _db.PlanificacionesAuxiliaresEquipo
                .AsNoTracking()
                .AnyAsync(auxiliar => auxiliar.EquipoId == equipoId
                    && !(auxiliar.DesdeDia == DiaConfiguracionNocturnosMes && auxiliar.HastaDia == DiaConfiguracionNocturnosMes));
        const bool balancearCargaFeriados = true;
        var configEquipo = await GetEquipoPlanificacionConfigAsync(equipoId);

        return ConstruirReglasRotacion(
            permiteTurnosAuxiliares,
            balancearCargaFeriados,
            configEquipo.MaximoTurnosNocturnosPorMes,
            configEquipo.MaximoTurnosNocturnosPorSemana,
            configEquipo.MaximoSlotsFinSemanaPorMes,
            balancearHorasSemanales);
    }

    private static ReglasRotacion ConstruirReglasRotacion(
        bool permiteTurnosAuxiliares,
        bool balancearCargaFeriados,
        int maximoTurnosNocturnosPorMes,
        int maximoTurnosNocturnosPorSemana,
        int maximoSlotsFinSemanaPorMes,
        bool balancearHorasSemanales = true)
    {
        const int objetivoNormalizado = 40 * 60;
        const int pesoRecargoNocturnoPorcentaje = 50;
        const int pesoRecargoFeriadoPorcentaje = 100;
        const int pesoRecargoFinSemanaPorcentaje = 100;

        return new ReglasRotacion
        {
            Obligatorias = new ReglasGlobalesObligatorias
            {
                MinutosObjetivoSemanales = objetivoNormalizado,
                MinutosMinimosDescansoEntreTurnos = 8 * 60,
                MinimoDiasDescansoConsecutivosPorSemana = 2
            },
            Configurables = new PoliticasConfigurablesEquipo
            {
                MaximoTurnosPorDia = 1,
                AplicarVacaciones = true,
                PermiteTurnosAuxiliares = permiteTurnosAuxiliares,
                EvitarFinesSemanaConsecutivos = true,
                MaximoFinesSemanaConsecutivos = 2,
                MaximoSlotsFinSemanaPorMes = maximoSlotsFinSemanaPorMes,
                MaximoTurnosNocturnosPorMes = maximoTurnosNocturnosPorMes,
                MaximoTurnosNocturnosPorSemana = maximoTurnosNocturnosPorSemana,
                BalancearHorasSemanales = balancearHorasSemanales,
                BalancearTurnosNocturnos = true,
                BalancearCargaFeriados = balancearCargaFeriados,
                BalancearRecargosCompuestos = true,
                PesoRecargoNocturnoPorcentaje = pesoRecargoNocturnoPorcentaje,
                PesoRecargoFeriadoPorcentaje = pesoRecargoFeriadoPorcentaje,
                PesoRecargoFinSemanaPorcentaje = pesoRecargoFinSemanaPorcentaje
            }
        };
    }

    private async Task<string?> ResolverTipoTurnoNocturnoConfiguracionAsync(string equipoId, IEnumerable<string> tipoTurnoIdsPreferidos)
    {
        var idsPreferidos = (tipoTurnoIdsPreferidos ?? Enumerable.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (idsPreferidos.Count > 0)
        {
            var tiposPreferidos = await _db.TipoTurnos
                .AsNoTracking()
                .Where(tipoTurno => idsPreferidos.Contains(tipoTurno.TipoTurnoId))
                .OrderBy(tipoTurno => tipoTurno.HoraInicio)
                .ToListAsync();

            var tipoPreferidoNocturno = tiposPreferidos
                .FirstOrDefault(tipoTurno => EsTipoTurnoNocturno(tipoTurno.HoraInicio, tipoTurno.HoraFin));

            if (tipoPreferidoNocturno != null)
            {
                return tipoPreferidoNocturno.TipoTurnoId;
            }
        }

        var tiposTurnoEquipo = await _db.EquipoTipoTurnos
            .AsNoTracking()
            .Where(mapeo => mapeo.EquipoId == equipoId)
            .Join(
                _db.TipoTurnos.AsNoTracking(),
                mapeo => mapeo.TipoTurnoId,
                tipoTurno => tipoTurno.TipoTurnoId,
                (_, tipoTurno) => tipoTurno)
            .OrderBy(tipoTurno => tipoTurno.HoraInicio)
            .ToListAsync();

        return tiposTurnoEquipo
            .FirstOrDefault(tipoTurno => EsTipoTurnoNocturno(tipoTurno.HoraInicio, tipoTurno.HoraFin))
            ?.TipoTurnoId;
    }

    private async Task GuardarEquipoPlanificacionConfigNormalizadaAsync(
        string equipoId,
        int? maximoSlotsFinSemanaPorMes,
        int? maximoTurnosNocturnosPorMes,
        int? maximoTurnosNocturnosPorSemana)
    {
        if (!maximoSlotsFinSemanaPorMes.HasValue
            && !maximoTurnosNocturnosPorMes.HasValue
            && !maximoTurnosNocturnosPorSemana.HasValue)
        {
            return;
        }

        var configuracion = await _db.EquipoPlanificacionConfigs
            .FirstOrDefaultAsync(config => config.EquipoId == equipoId);
        if (configuracion == null)
        {
            configuracion = new EquipoPlanificacionConfig
            {
                EquipoPlanificacionConfigId = Guid.NewGuid().ToString("N")[..12],
                EquipoId = equipoId
            };
            _db.EquipoPlanificacionConfigs.Add(configuracion);
        }

        if (maximoSlotsFinSemanaPorMes.HasValue)
        {
            configuracion.MaximoSlotsFinSemanaPorMes = NormalizarMaximoSlotsFinSemanaPorMes(maximoSlotsFinSemanaPorMes);
        }

        if (maximoTurnosNocturnosPorMes.HasValue)
        {
            configuracion.MaximoTurnosNocturnosPorMes = NormalizarMaximoTurnosNocturnosPorMes(maximoTurnosNocturnosPorMes);
        }

        if (maximoTurnosNocturnosPorSemana.HasValue)
        {
            configuracion.MaximoTurnosNocturnosPorSemana = NormalizarMaximoTurnosNocturnosPorSemana(maximoTurnosNocturnosPorSemana);
        }
    }

    private static int NormalizarMaximoTurnosNocturnosPorMes(int? valor)
    {
        return Math.Clamp(valor ?? MaximoTurnosNocturnosPorMesDefault, MaximoTurnosNocturnosPorMesMin, MaximoTurnosNocturnosPorMesMax);
    }

    private static int NormalizarMaximoTurnosNocturnosPorSemana(int? valor)
    {
        return Math.Clamp(valor ?? MaximoTurnosNocturnosPorSemanaDefault, MaximoTurnosNocturnosPorSemanaMin, MaximoTurnosNocturnosPorSemanaMax);
    }

    private static int NormalizarMaximoSlotsFinSemanaPorMes(int? valor)
    {
        return Math.Clamp(valor ?? MaximoSlotsFinSemanaPorMesDefault, MaximoSlotsFinSemanaPorMesMin, MaximoSlotsFinSemanaPorMesMax);
    }

    private static bool EsTipoTurnoNocturno(TimeOnly horaInicio, TimeOnly horaFin)
    {
        var inicio = FechaBaseCalculoTurno.ToDateTime(horaInicio);
        var fin = FechaBaseCalculoTurno.ToDateTime(horaFin);
        if (fin <= inicio)
        {
            fin = fin.AddDays(1);
        }

        var duracionMinutos = (fin - inicio).TotalMinutes;
        if (duracionMinutos <= 0)
        {
            return false;
        }

        var minutosVentanaNocturna = CalcularMinutosVentanaNocturna(inicio, fin);
        return minutosVentanaNocturna / duracionMinutos > 0.70d;
    }

    private static int CalcularMinutosVentanaNocturna(DateTime inicio, DateTime fin)
    {
        if (fin <= inicio)
        {
            return 0;
        }

        var total = 0d;
        var fecha = inicio.Date == DateTime.MinValue.Date
            ? inicio.Date
            : inicio.Date.AddDays(-1);
        while (fecha <= fin.Date)
        {
            var inicioVentana = fecha.AddHours(18);
            var finVentana = fecha.AddDays(1).AddHours(7);
            var inicioInterseccion = inicio > inicioVentana ? inicio : inicioVentana;
            var finInterseccion = fin < finVentana ? fin : finVentana;

            if (finInterseccion > inicioInterseccion)
            {
                total += (finInterseccion - inicioInterseccion).TotalMinutes;
            }

            fecha = fecha.AddDays(1);
        }

        return (int)Math.Round(total);
    }

    private static string DescribirGrupo(Grupo grupo)
    {
        var nombre = string.IsNullOrWhiteSpace(grupo.NombreGrupo)
            ? "sin nombre"
            : grupo.NombreGrupo.Trim();

        return $"grupo \"{nombre}\" ({grupo.GrupoId})";
    }

    private static string DescribirGrupo(string grupoId, IReadOnlyDictionary<string, Grupo> gruposPorId)
    {
        if (!string.IsNullOrWhiteSpace(grupoId) && gruposPorId.TryGetValue(grupoId, out var grupo))
        {
            return DescribirGrupo(grupo);
        }

        return string.IsNullOrWhiteSpace(grupoId)
            ? "grupo seleccionado"
            : $"grupo seleccionado ({grupoId})";
    }

    private static string DescribirEquipo(Equipo? equipo, string? equipoId)
    {
        if (equipo != null)
        {
            var nombre = string.IsNullOrWhiteSpace(equipo.NombreEquipo)
                ? "sin nombre"
                : equipo.NombreEquipo.Trim();

            return $"equipo \"{nombre}\" ({equipo.EquipoId})";
        }

        return string.IsNullOrWhiteSpace(equipoId)
            ? "equipo seleccionado"
            : $"equipo seleccionado ({equipoId})";
    }

    private static string DescribirTipoTurno(string tipoTurnoId, IReadOnlyDictionary<string, TipoTurno> tiposTurnoPorId)
    {
        if (!string.IsNullOrWhiteSpace(tipoTurnoId) && tiposTurnoPorId.TryGetValue(tipoTurnoId, out var tipoTurno))
        {
            var nombre = string.IsNullOrWhiteSpace(tipoTurno.NombreTurno)
                ? "sin nombre"
                : tipoTurno.NombreTurno.Trim();

            return $"turno \"{nombre}\" ({tipoTurno.TipoTurnoId})";
        }

        return string.IsNullOrWhiteSpace(tipoTurnoId)
            ? "turno seleccionado"
            : $"turno seleccionado ({tipoTurnoId})";
    }

    private static bool DiagnosticarFactibilidadEstructural(
        ProblemaRotacion problema,
        IReadOnlyDictionary<string, Grupo> gruposPorId,
        IReadOnlyDictionary<string, TipoTurno> tiposTurnoPorId,
        out string detalle)
    {
        var empleadosPorId = problema.Empleados.ToDictionary(empleado => empleado.Id, StringComparer.OrdinalIgnoreCase);
        var bloqueosVacacion = problema.Ausencias
            .Where(ausencia => string.Equals(ausencia.Motivo, "Vacaciones", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                ausencia => ausencia.EmpleadoId,
                ausencia => ausencia.Fechas,
                StringComparer.OrdinalIgnoreCase);

        var conflictos = new List<string>();

        foreach (var slot in problema.Slots.Where(s => !s.EsAuxiliar && s.MaximoApoyoCedible <= 0))
        {
            var elegibles = problema.Empleados
                .Where(empleado => PuedeCubrirGrupoDiagnostico(empleado, slot.GrupoId))
                .Where(empleado =>
                    !bloqueosVacacion.TryGetValue(empleado.Id, out var fechasBloqueadas)
                    || !fechasBloqueadas.Contains(slot.Fecha))
                .Select(empleado => empleado.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (elegibles < slot.EmpleadosRequeridos)
            {
                conflictos.Add(
                    $"S{slot.IndiceSemana + 1} {slot.Fecha:yyyy-MM-dd}: {DescribirGrupo(slot.GrupoId, gruposPorId)}, {DescribirTipoTurno(slot.TipoTurnoId, tiposTurnoPorId)}, requiere {slot.EmpleadosRequeridos} y solo hay {elegibles} elegibles");
            }
        }

        if (conflictos.Count == 0)
        {
            detalle = string.Empty;
            return true;
        }

        var muestra = string.Join(" | ", conflictos.Take(5));
        detalle = conflictos.Count > 5
            ? $"{muestra} | +{conflictos.Count - 5} conflictos más"
            : muestra;
        return false;
    }

    private static bool PuedeCubrirGrupoDiagnostico(Empleado empleado, string grupoId)
    {
        if (string.IsNullOrWhiteSpace(grupoId))
        {
            return true;
        }

        if (string.Equals(empleado.GrupoPrimarioId, grupoId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return empleado.GruposSecundariosIds.Contains(grupoId);
    }

    private static string NormalizarNombreDia(string dia)
    {
        // Normalizar los nombres de días para el algoritmo (sin tildes, capitalizado)
        return dia.ToLower() switch
        {
            "lunes" => "Lunes",
            "martes" => "Martes",
            "miercoles" or "miércoles" => "Miércoles",
            "jueves" => "Jueves",
            "viernes" => "Viernes",
            "sabado" or "sábado" => "Sábado",
            "domingo" => "Domingo",
            _ => dia // Si ya está normalizado, devolverlo tal cual
        };
    }

    private static List<PatronTurnos> ConstruirPatronesDesdeBlueprints(
        List<PlanificacionBlueprint> blueprints)
    {
        // Agrupar todas las etiquetas únicas
        var etiquetasUnicas = new HashSet<string>();
        foreach (var blueprint in blueprints)
        {
            if (!string.IsNullOrWhiteSpace(blueprint.Etiquetas))
            {
                var etiquetas = blueprint.Etiquetas.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .Where(e => !string.IsNullOrWhiteSpace(e));

                foreach (var etiqueta in etiquetas)
                {
                    etiquetasUnicas.Add(etiqueta);
                }
            }
        }

        var patrones = new List<PatronTurnos>();

        // Crear un patrón para cada etiqueta única
        foreach (var etiqueta in etiquetasUnicas.OrderBy(e => e))
        {
            var diasTrabajo = new List<PlanificacionTurno>();

            // Buscar todos los días/turnos donde aparece esta etiqueta
            foreach (var blueprint in blueprints)
            {
                if (string.IsNullOrWhiteSpace(blueprint.Etiquetas))
                    continue;

                var etiquetas = blueprint.Etiquetas.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .ToList();

                if (etiquetas.Contains(etiqueta, StringComparer.OrdinalIgnoreCase))
                {
                    diasTrabajo.Add(new PlanificacionTurno
                    {
                        Dia = NormalizarNombreDia(blueprint.Dia),
                        TipoHorario = blueprint.TipoTurnoId,
                        Cantidad = 0, // No usado en Blueprint
                        Inicio = DateTime.MinValue, // Se calculará en el algoritmo
                        Fin = DateTime.MinValue      // Se calculará en el algoritmo
                    });
                }
            }

            if (diasTrabajo.Any())
            {
                patrones.Add(new PatronTurnos
                {
                    Nombre = etiqueta,
                    DiasTrabajo = diasTrabajo
                });
            }
        }

        return patrones;
    }

}
