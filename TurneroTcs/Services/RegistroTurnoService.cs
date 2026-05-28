using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Services.Interfaces;

namespace TurneroTcs.Services;

public class RegistroTurnoService : IRegistroTurnoService
{
    private const int MinRestHours = 8;
    private const int MaxShiftsPerWeek = 5;
    private static readonly string[] NonWorkedShiftPrefixes = ["VAC", "PER", "CAL"];
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RegistroTurnoService> _logger;

    public RegistroTurnoService(ApplicationDbContext db, ILogger<RegistroTurnoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private static bool CanManageTurnosByRole(string currentUserRole) =>
        currentUserRole == "SuperAdmin" || currentUserRole == "Admin" || currentUserRole == "Lider";

    private async Task<Result<string?>> GetActorEquipoScopeAsync(string currentUserRole, string currentUserId)
    {
        if (string.IsNullOrWhiteSpace(currentUserRole))
        {
            return Result<string?>.Fail("No se ha podido identificar el rol del usuario.");
        }

        if (!CanManageTurnosByRole(currentUserRole))
        {
            return Result<string?>.Fail("No tiene permiso para realizar esta accion.");
        }

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Result<string?>.Fail("No se ha podido identificar el usuario autenticado.");
        }

        if (currentUserRole == "SuperAdmin" || currentUserRole == "Admin")
        {
            return Result<string?>.Ok(null);
        }

        var liderEquipoId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.UserId == currentUserId && !p.Borrado)
            .Select(p => p.EquipoId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(liderEquipoId))
        {
            return Result<string?>.Fail("Lider no tiene equipo asignado.");
        }

        return Result<string?>.Ok(liderEquipoId);
    }

    private static bool IsWithinActorEquipoScope(string? actorEquipoId, string? targetEquipoId) =>
        string.IsNullOrWhiteSpace(actorEquipoId)
        || (!string.IsNullOrWhiteSpace(targetEquipoId)
            && string.Equals(actorEquipoId, targetEquipoId, StringComparison.Ordinal));

    public async Task<TurnoGenerationResult> GenerateAsync(
        string personaId,
        IReadOnlyList<string> tipoTurnoIds,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        string? grupoId,
        string currentUserRole,
        string currentUserId)
    {
        if (string.IsNullOrWhiteSpace(personaId))
        {
            return TurnoGenerationResult.Fail("Persona es requerida.");
        }

        if (tipoTurnoIds == null || tipoTurnoIds.Count == 0)
        {
            return TurnoGenerationResult.Fail("Debe seleccionar al menos un tipo de turno.");
        }

        if (fechaFin < fechaInicio)
        {
            return TurnoGenerationResult.Fail("La fecha fin no puede ser menor que la fecha inicio.");
        }

        var actorScopeResult = await GetActorEquipoScopeAsync(currentUserRole, currentUserId);
        if (!actorScopeResult.Succeeded)
        {
            return TurnoGenerationResult.Fail(actorScopeResult.Error ?? "No tiene permiso para generar turnos.");
        }

        var persona = await _db.Personas
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.PersonaId == personaId && !p.Borrado);

        if (persona == null)
        {
            return TurnoGenerationResult.Fail("Persona no existe o fue borrada.");
        }

        if (!IsWithinActorEquipoScope(actorScopeResult.Value, persona.EquipoId))
        {
            _logger.LogWarning(
                "Usuario {UserId} con rol {Role} intento generar turnos para PersonaId {PersonaId} fuera de su equipo.",
                currentUserId,
                currentUserRole,
                personaId);
            return TurnoGenerationResult.Fail("No tiene permiso para administrar turnos de esta persona.");
        }

        if (!string.IsNullOrWhiteSpace(grupoId))
        {
            if (string.IsNullOrWhiteSpace(persona.EquipoId))
            {
                return TurnoGenerationResult.Fail("Persona no tiene equipo asignado.");
            }

            var grupo = await _db.Grupos
                .AsNoTracking()
                .SingleOrDefaultAsync(g => g.GrupoId == grupoId);
            if (grupo == null)
            {
                return TurnoGenerationResult.Fail("Grupo no existe.");
            }

            if (grupo.EquipoId != persona.EquipoId)
            {
                return TurnoGenerationResult.Fail("Grupo no pertenece al equipo de la persona.");
            }

            var personaEnGrupo = await _db.PersonaGrupos
                .AsNoTracking()
                .AnyAsync(pg => pg.PersonaId == personaId && pg.GrupoId == grupoId);

            if (!personaEnGrupo)
            {
                return TurnoGenerationResult.Fail("La persona no pertenece al grupo seleccionado.");
            }
        }

        var tipoTurnoIdSet = tipoTurnoIds.ToHashSet();
        var rangeStart = GetWeekStart(fechaInicio).AddDays(-7);
        var rangeEnd = fechaFin.AddDays(1);

        var existingTurnos = await _db.RegistroTurnos
            .AsNoTracking()
            .Where(rt => rt.PersonaId == personaId
                && rt.FechaTurno >= rangeStart
                && rt.FechaTurno <= rangeEnd)
            .ToListAsync();

        var neededTipoTurnoIds = existingTurnos
            .Select(rt => rt.TipoTurnoId)
            .Concat(tipoTurnoIds)
            .Distinct()
            .ToList();

        var tipoTurnos = await _db.TipoTurnos
            .AsNoTracking()
            .Where(t => neededTipoTurnoIds.Contains(t.TipoTurnoId))
            .ToListAsync();

        var tipoTurnoMap = tipoTurnos.ToDictionary(t => t.TipoTurnoId, t => t);
        if (tipoTurnoMap.Count != neededTipoTurnoIds.Count)
        {
            return TurnoGenerationResult.Fail("Uno o mas tipos de turno no existen.");
        }

        var scheduled = new List<TurnoSlot>();
        foreach (var existing in existingTurnos)
        {
            var tipo = tipoTurnoMap[existing.TipoTurnoId];
            var slot = BuildSlot(existing.FechaTurno, tipo, existing.TipoTurnoId, isNew: false);
            scheduled.Add(slot);
        }

        var existingDates = scheduled
            .Where(s => !s.IsNew)
            .Select(s => s.FechaTurno)
            .ToHashSet();

        var weekCounts = new Dictionary<DateOnly, int>();
        foreach (var slot in scheduled)
        {
            if (!ShouldCountAsWorkedShift(slot.TipoTurnoId, isNoLaboradoPorFeriado: false, isFeriado: false))
            {
                continue;
            }

            var weekStart = GetWeekStart(slot.FechaTurno);
            weekCounts[weekStart] = weekCounts.TryGetValue(weekStart, out var count) ? count + 1 : 1;
        }

        var created = new List<TurnoSlot>();
        var rotationIndex = 0;
        for (var fecha = fechaInicio; fecha <= fechaFin; fecha = fecha.AddDays(1))
        {
            var tipoTurnoId = tipoTurnoIds[rotationIndex % tipoTurnoIds.Count];
            rotationIndex++;

            if (existingDates.Contains(fecha) || created.Any(s => s.FechaTurno == fecha))
            {
                continue;
            }

            var tipoTurno = tipoTurnoMap[tipoTurnoId];
            var newSlot = BuildSlot(fecha, tipoTurno, tipoTurnoId, isNew: true);

            if (HasRestConflict(newSlot, scheduled))
            {
                continue;
            }

            var weekStart = GetWeekStart(fecha);
            var weekCount = weekCounts.TryGetValue(weekStart, out var count) ? count : 0;
            if (weekCount >= MaxShiftsPerWeek)
            {
                continue;
            }

            scheduled.Add(newSlot);
            created.Add(newSlot);
            weekCounts[weekStart] = weekCount + 1;
        }

        ApplyRestDaysRule(fechaInicio, fechaFin, scheduled, created, weekCounts);

        if (created.Count == 0)
        {
            return TurnoGenerationResult.Ok(0);
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var slot in created)
            {
                var registro = new RegistroTurno
                {
                    TurnoId = Guid.NewGuid().ToString("N"),
                    PersonaId = personaId,
                    TipoTurnoId = slot.TipoTurnoId,
                    FechaTurno = slot.FechaTurno,
                    GrupoId = grupoId,
                    EsFeriado = false,
                    NoLaboradoPorFeriado = false,
                    EsTurnoExtra = false
                };
                _db.RegistroTurnos.Add(registro);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogDebug("Generados {Count} turnos para persona {Persona}.", created.Count, personaId);
            return TurnoGenerationResult.Ok(created.Count);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al generar turnos para persona {Persona}.", personaId);
            return TurnoGenerationResult.Fail("Ha ocurrido un error al generar turnos.");
        }
    }

    private static TurnoSlot BuildSlot(DateOnly fecha, TipoTurno tipoTurno, string tipoTurnoId, bool isNew)
    {
        var start = fecha.ToDateTime(tipoTurno.HoraInicio);
        var endDate = tipoTurno.HoraFin <= tipoTurno.HoraInicio ? fecha.AddDays(1) : fecha;
        var end = endDate.ToDateTime(tipoTurno.HoraFin);

        return new TurnoSlot(fecha, tipoTurnoId, start, end, isNew);
    }

    public async Task<TurnoPreviewSaveResult> SavePreviewAsync(
        IReadOnlyList<RegistroTurnoPreviewItem> items,
        string currentUserRole,
        string currentUserId)
    {
        if (items == null || items.Count == 0)
        {
            return TurnoPreviewSaveResult.Fail("No hay turnos para guardar.");
        }

        var actorScopeResult = await GetActorEquipoScopeAsync(currentUserRole, currentUserId);
        if (!actorScopeResult.Succeeded)
        {
            return TurnoPreviewSaveResult.Fail(actorScopeResult.Error ?? "No tiene permiso para guardar turnos.");
        }

        var personaIds = items
            .Select(i => i.PersonaId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        var tipoTurnoIds = items
            .Select(i => i.TipoTurnoId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        var grupoIds = items
            .Select(i => i.GrupoId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        var fechas = items
            .Select(i => i.FechaTurno)
            .Distinct()
            .ToList();

        if (personaIds.Count == 0 || tipoTurnoIds.Count == 0 || fechas.Count == 0)
        {
            return TurnoPreviewSaveResult.Fail("Hay turnos incompletos en la previsualizacion.");
        }

        var personas = await _db.Personas
            .AsNoTracking()
            .Where(p => personaIds.Contains(p.PersonaId) && !p.Borrado)
            .ToDictionaryAsync(p => p.PersonaId);

        var tipos = await _db.TipoTurnos
            .AsNoTracking()
            .Where(t => tipoTurnoIds.Contains(t.TipoTurnoId))
            .ToDictionaryAsync(t => t.TipoTurnoId);

        var grupos = await _db.Grupos
            .AsNoTracking()
            .Where(g => grupoIds.Contains(g.GrupoId))
            .ToDictionaryAsync(g => g.GrupoId);

        if (actorScopeResult.Value != null &&
            personas.Values.Any(p => !IsWithinActorEquipoScope(actorScopeResult.Value, p.EquipoId)))
        {
            _logger.LogWarning(
                "Usuario {UserId} con rol {Role} intento guardar turnos previsualizados fuera de su equipo.",
                currentUserId,
                currentUserRole);
            return TurnoPreviewSaveResult.Fail("No tiene permiso para guardar turnos para otra area.");
        }

        if (actorScopeResult.Value != null &&
            grupos.Values.Any(g => !IsWithinActorEquipoScope(actorScopeResult.Value, g.EquipoId)))
        {
            _logger.LogWarning(
                "Usuario {UserId} con rol {Role} intento guardar turnos previsualizados con grupos fuera de su equipo.",
                currentUserId,
                currentUserRole);
            return TurnoPreviewSaveResult.Fail("No tiene permiso para guardar turnos para otra area.");
        }
        var feriadoDates = await BuildFeriadoDateSetAsync(fechas);

        var personaGrupos = await _db.PersonaGrupos
            .AsNoTracking()
            .Where(pg => personaIds.Contains(pg.PersonaId) && grupoIds.Contains(pg.GrupoId))
            .Select(pg => new { pg.PersonaId, pg.GrupoId })
            .ToListAsync();

        var personaGrupoSet = new HashSet<(string PersonaId, string GrupoId)>(
            personaGrupos.Select(pg => (pg.PersonaId, pg.GrupoId)));

        var existing = await _db.RegistroTurnos
            .Where(rt => personaIds.Contains(rt.PersonaId)
                && tipoTurnoIds.Contains(rt.TipoTurnoId)
                && fechas.Contains(rt.FechaTurno))
            .ToListAsync();

        var existingMap = new Dictionary<(string PersonaId, string TipoTurnoId, DateOnly FechaTurno), RegistroTurno>();
        foreach (var row in existing)
        {
            var key = (row.PersonaId, row.TipoTurnoId, row.FechaTurno);
            if (!existingMap.ContainsKey(key))
            {
                existingMap[key] = row;
            }
        }

        var itemScopes = items
            .Where(i => !string.IsNullOrWhiteSpace(i.PersonaId))
            .Select(i => (i.PersonaId!, GetWeekStart(i.FechaTurno)))
            .Distinct()
            .ToList();
        var weekWorkedCounts = await BuildWorkedShiftCountsByScopeAsync(itemScopes);

        var toCreate = new List<RegistroTurno>();
        var skipped = 0;
        var updated = 0;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.PersonaId) ||
                string.IsNullOrWhiteSpace(item.TipoTurnoId))
            {
                skipped++;
                continue;
            }

            if (!personas.TryGetValue(item.PersonaId, out var persona))
            {
                skipped++;
                continue;
            }

            if (!tipos.ContainsKey(item.TipoTurnoId))
            {
                skipped++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.GrupoId))
            {
                if (!grupos.TryGetValue(item.GrupoId!, out var grupo))
                {
                    skipped++;
                    continue;
                }

                if (grupo.EquipoId != persona.EquipoId)
                {
                    skipped++;
                    continue;
                }

                // Los turnos asignados por el balanceador de feriados ya validaron
                // la elegibilidad via PersonaGrupos o via el preview generado.
                // Solo aplicar el chequeo de PersonaGrupos para turnos no feriado.
                if (!item.EsFeriado && !personaGrupoSet.Contains((item.PersonaId, item.GrupoId!)))
                {
                    skipped++;
                    continue;
                }
            }

            var esFeriado = feriadoDates.Contains(item.FechaTurno);
            var noLaboradoPorFeriado = item.NoLaboradoPorFeriado || false;
            var key = (item.PersonaId, item.TipoTurnoId, item.FechaTurno);
            if (existingMap.TryGetValue(key, out var existingTurno))
            {
                var changed = false;

                if (!string.Equals(
                    string.IsNullOrWhiteSpace(existingTurno.GrupoId) ? string.Empty : existingTurno.GrupoId.Trim(),
                    string.IsNullOrWhiteSpace(item.GrupoId) ? string.Empty : item.GrupoId.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    existingTurno.GrupoId = item.GrupoId;
                    changed = true;
                }

                if (existingTurno.EsFeriado != esFeriado)
                {
                    existingTurno.EsFeriado = esFeriado;
                    changed = true;
                }

                if (existingTurno.NoLaboradoPorFeriado != noLaboradoPorFeriado)
                {
                    // Para turnos de feriado, el valor "trabaja" (false) tiene prioridad.
                    // Si el existente ya dice "trabaja" (false), no permitir que un duplicado
                    // del preview lo sobreescriba con "no trabaja" (true).
                    if (!item.EsFeriado || noLaboradoPorFeriado == false || existingTurno.NoLaboradoPorFeriado == true)
                    {
                        existingTurno.NoLaboradoPorFeriado = noLaboradoPorFeriado;
                        changed = true;
                    }
                }

                if (changed)
                {
                    updated++;
                }

                continue;
            }

            var weekScope = (item.PersonaId, GetWeekStart(item.FechaTurno));
            var workedCount = weekWorkedCounts.TryGetValue(weekScope, out var currentWeekCount)
                ? currentWeekCount
                : 0;
            var countsAsWorked = ShouldCountAsWorkedShift(item.TipoTurnoId, noLaboradoPorFeriado, esFeriado);
            var esTurnoExtra = countsAsWorked && workedCount >= MaxShiftsPerWeek;

            if (countsAsWorked)
            {
                weekWorkedCounts[weekScope] = workedCount + 1;
            }

            toCreate.Add(new RegistroTurno
            {
                TurnoId = Guid.NewGuid().ToString("N"),
                PersonaId = item.PersonaId,
                TipoTurnoId = item.TipoTurnoId,
                FechaTurno = item.FechaTurno,
                GrupoId = item.GrupoId,
                EsFeriado = esFeriado,
                NoLaboradoPorFeriado = noLaboradoPorFeriado,
                EsTurnoExtra = esTurnoExtra
            });
            existingMap[key] = toCreate[^1];
        }

        if (toCreate.Count == 0 && updated == 0)
        {
            return TurnoPreviewSaveResult.Fail("No hay turnos validos para guardar.");
        }

        var feriadosACrear = toCreate.Count(t => t.EsFeriado);
        _logger.LogWarning("[FERIADO-RESUMEN] toCreate={Total}, feriados en toCreate={Feriados}, updated={Updated}",
            toCreate.Count, feriadosACrear, updated);
        
        var turnosFiltrados = new List<RegistroTurno>();
        var turnosOmitidosPorVacaciones = 0;
        if (toCreate.Count > 0)
        {
            _logger.LogInformation("Aplicando filtro de vacaciones a {Count} turnos", toCreate.Count);
            var filtered = await FiltrarTurnosPorVacacionesAsync(toCreate);
            turnosFiltrados = filtered.turnosFiltrados;
            turnosOmitidosPorVacaciones = filtered.turnosOmitidos;

            var feriadosFiltrados = toCreate.Count(t => t.EsFeriado) - turnosFiltrados.Count(t => t.EsFeriado);
            if (feriadosFiltrados > 0)
                _logger.LogWarning("[FERIADO-VACACIONES] {N} turno(s) feriado fueron eliminados por vacaciones", feriadosFiltrados);

            if (turnosFiltrados.Count == 0 && updated == 0)
            {
                _logger.LogWarning("Todos los turnos fueron filtrados por vacaciones");
                return TurnoPreviewSaveResult.Fail("Todos los turnos caen en periodos de vacaciones.");
            }
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            if (turnosFiltrados.Count > 0)
            {
                _db.RegistroTurnos.AddRange(turnosFiltrados);
            }
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            var totalOmitidos = skipped + turnosOmitidosPorVacaciones;
            var totalAplicados = turnosFiltrados.Count + updated;
            
            if (turnosOmitidosPorVacaciones > 0)
            {
                _logger.LogWarning("⚠️ {TurnosOmitidos} turnos fueron omitidos por estar en período de vacaciones", 
                    turnosOmitidosPorVacaciones);
            }

            _logger.LogInformation("Guardados {Count} turnos de previsualizacion (nuevos={Nuevos}, actualizados={Actualizados}). Total omitidos: {Omitidos} ({Validacion} validacion, {Vacaciones} vacaciones)",
                totalAplicados, turnosFiltrados.Count, updated, totalOmitidos, skipped, turnosOmitidosPorVacaciones);
            
            return TurnoPreviewSaveResult.Ok(totalAplicados, totalOmitidos);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al guardar turnos de previsualizacion.");
            return TurnoPreviewSaveResult.Fail("Ha ocurrido un error al guardar los turnos.");
        }
    }

    private static bool HasRestConflict(TurnoSlot candidate, List<TurnoSlot> scheduled)
    {
        foreach (var slot in scheduled)
        {
            if (candidate.End <= slot.Start)
            {
                if ((slot.Start - candidate.End).TotalHours < MinRestHours)
                {
                    return true;
                }
                continue;
            }

            if (candidate.Start >= slot.End)
            {
                if ((candidate.Start - slot.End).TotalHours < MinRestHours)
                {
                    return true;
                }
                continue;
            }

            return true;
        }

        return false;
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var offset = (int)date.DayOfWeek - (int)DayOfWeek.Monday;
        if (offset < 0)
        {
            offset += 7;
        }
        return date.AddDays(-offset);
    }

    private static void ApplyRestDaysRule(
        DateOnly fechaInicio,
        DateOnly fechaFin,
        List<TurnoSlot> scheduled,
        List<TurnoSlot> created,
        Dictionary<DateOnly, int> weekCounts)
    {
        var weekStart = GetWeekStart(fechaInicio);
        var lastWeekStart = GetWeekStart(fechaFin);

        for (var week = weekStart; week <= lastWeekStart; week = week.AddDays(7))
        {
            var weekDates = Enumerable.Range(0, 7)
                .Select(offset => week.AddDays(offset))
                .ToList();

            var shiftsByDate = weekDates.ToDictionary(date => date, date => 0);
            foreach (var slot in scheduled.Where(s => s.FechaTurno >= week && s.FechaTurno <= week.AddDays(6)))
            {
                shiftsByDate[slot.FechaTurno]++;
            }

            if (HasTwoConsecutiveRestDays(shiftsByDate))
            {
                continue;
            }

            for (var dayIndex = 6; dayIndex >= 0; dayIndex--)
            {
                var date = weekDates[dayIndex];
                if (shiftsByDate[date] == 0)
                {
                    continue;
                }

                var removable = created.FirstOrDefault(s => s.FechaTurno == date);
                if (removable == null)
                {
                    continue;
                }

                created.Remove(removable);
                scheduled.Remove(removable);
                shiftsByDate[date]--;

                var weekStartKey = GetWeekStart(date);
                if (weekCounts.TryGetValue(weekStartKey, out var count) && count > 0)
                {
                    weekCounts[weekStartKey] = count - 1;
                }

                if (HasTwoConsecutiveRestDays(shiftsByDate))
                {
                    break;
                }
            }
        }
    }

    private static bool HasTwoConsecutiveRestDays(Dictionary<DateOnly, int> shiftsByDate)
    {
        var ordered = shiftsByDate.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value == 0).ToList();
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            if (ordered[i] && ordered[i + 1])
            {
                return true;
            }
        }

        return false;
    }

    private async Task<Dictionary<(string PersonaId, DateOnly WeekStart), int>> BuildWorkedShiftCountsByScopeAsync(
        IEnumerable<(string PersonaId, DateOnly WeekStart)> scopes)
    {
        var normalizedScopes = scopes
            .Where(s => !string.IsNullOrWhiteSpace(s.PersonaId))
            .Distinct()
            .ToList();

        if (normalizedScopes.Count == 0)
        {
            return new Dictionary<(string PersonaId, DateOnly WeekStart), int>();
        }

        var personaIds = normalizedScopes
            .Select(s => s.PersonaId)
            .Distinct()
            .ToList();

        var minWeekStart = normalizedScopes.Min(s => s.WeekStart);
        var maxWeekEnd = normalizedScopes.Max(s => s.WeekStart).AddDays(6);

        var turnos = await (
            from rt in _db.RegistroTurnos
            where personaIds.Contains(rt.PersonaId)
                && rt.FechaTurno >= minWeekStart
                && rt.FechaTurno <= maxWeekEnd
            select rt
        ).ToListAsync();

        var counts = new Dictionary<(string PersonaId, DateOnly WeekStart), int>();
        foreach (var scope in normalizedScopes)
        {
            var count = turnos
                .Where(t => t.PersonaId == scope.PersonaId)
                .Where(t => GetWeekStart(t.FechaTurno) == scope.WeekStart)
                .Count(t => ShouldCountAsWorkedShift(t.TipoTurnoId, t.NoLaboradoPorFeriado, t.EsFeriado));

            counts[(scope.PersonaId, scope.WeekStart)] = count;
        }

        return counts;
    }

    private static bool ShouldCountAsWorkedShift(string? tipoTurnoId, bool isNoLaboradoPorFeriado, bool isFeriado)
    {
        if (isFeriado)
        {
            return false;
        }

        if (isNoLaboradoPorFeriado)
        {
            return false;
        }

        var normalizedTipoTurnoId = tipoTurnoId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTipoTurnoId))
        {
            return false;
        }

        foreach (var prefix in NonWorkedShiftPrefixes ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                continue;
            }

            if (normalizedTipoTurnoId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record TurnoSlot(
        DateOnly FechaTurno,
        string TipoTurnoId,
        DateTime Start,
        DateTime End,
        bool IsNew);

    private async Task<HashSet<DateOnly>> BuildFeriadoDateSetAsync(IReadOnlyList<DateOnly> fechas)
    {
        var dates = new HashSet<DateOnly>();
        if (fechas == null || fechas.Count == 0)
        {
            return dates;
        }

        var minDate = fechas.Min();
        var maxDate = fechas.Max();

        var feriados = await _db.Feriados
            .AsNoTracking()
            .Where(f => f.InicioFeriado <= maxDate && f.FinFeriado >= minDate)
            .Select(f => new { f.InicioFeriado, f.FinFeriado })
            .ToListAsync();

        foreach (var fecha in fechas)
        {
            foreach (var feriado in feriados)
            {
                if (fecha >= feriado.InicioFeriado && fecha <= feriado.FinFeriado)
                {
                    dates.Add(fecha);
                    break;
                }
            }
        }

        return dates;
    }

    public async Task<TurnoChangeResult> ConfirmChangeAsync(
        RegistroTurnoChangeRequest request,
        string currentUserRole,
        string currentUserId)
    {
        if (request == null)
        {
            return TurnoChangeResult.Fail("Solicitud invalida.");
        }

        if (request.Type == "move")
        {
            return await ConfirmMoveAsync(request.Move, currentUserRole, currentUserId);
        }

        if (request.Type == "swap-persona")
        {
            return await ConfirmSwapPersonaAsync(request.Swap, currentUserRole, currentUserId);
        }

        return TurnoChangeResult.Fail("Tipo de cambio no soportado.");
    }

    public async Task<TurnoChangeResult> DeleteAsync(string turnoId, string currentUserRole, string currentUserId)
    {
        if (string.IsNullOrWhiteSpace(turnoId))
        {
            return TurnoChangeResult.Fail("Turno invalido.");
        }

        var actorScopeResult = await GetActorEquipoScopeAsync(currentUserRole, currentUserId);
        if (!actorScopeResult.Succeeded)
        {
            return TurnoChangeResult.Fail(actorScopeResult.Error ?? "No tiene permiso para eliminar turnos.");
        }

        var registro = await _db.RegistroTurnos
            .SingleOrDefaultAsync(rt => rt.TurnoId == turnoId);

        if (registro == null)
        {
            return TurnoChangeResult.Fail("Turno no existe.");
        }

        var turnoEquipoId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.PersonaId == registro.PersonaId)
            .Select(p => p.EquipoId)
            .FirstOrDefaultAsync();

        if (!IsWithinActorEquipoScope(actorScopeResult.Value, turnoEquipoId))
        {
            _logger.LogWarning(
                "Usuario {UserId} con rol {Role} intento eliminar TurnoId {TurnoId} fuera de su equipo.",
                currentUserId,
                currentUserRole,
                turnoId);
            return TurnoChangeResult.Fail("No tiene permiso para eliminar este turno.");
        }

        var hasPermiso = await (
            from permiso in _db.Permisos.AsNoTracking()
            join solicitud in _db.Solicitudes.AsNoTracking() on permiso.SolicitudId equals solicitud.SolicitudId
            where permiso.RegistroTurnoId == turnoId
                && solicitud.EstadoSolicitud != SolicitudEstado.Rechazado
                && solicitud.EstadoSolicitud != SolicitudEstado.Cancelado
            select permiso.PermisoId)
            .AnyAsync();

        var hasCambio = await (
            from cambio in _db.CambiosTurno.AsNoTracking()
            join solicitud in _db.Solicitudes.AsNoTracking() on cambio.SolicitudId equals solicitud.SolicitudId
            where (cambio.TurnoOrigenId == turnoId || cambio.TurnoDestinoId == turnoId)
                && solicitud.EstadoSolicitud != SolicitudEstado.Rechazado
                && solicitud.EstadoSolicitud != SolicitudEstado.Cancelado
            select cambio.CambioTurnoId)
            .AnyAsync();

        if (hasPermiso || hasCambio)
        {
            return TurnoChangeResult.Fail("El turno tiene solicitudes asociadas y no se puede eliminar.");
        }

        _db.RegistroTurnos.Remove(registro);

        try
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("Turno {TurnoId} eliminado de forma manual.", turnoId);
            return TurnoChangeResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar turno {TurnoId}.", turnoId);
            return TurnoChangeResult.Fail("No se pudo eliminar el turno.");
        }
    }

    public async Task<TurnoChangeResult> SetTurnoExtraAsync(
        string turnoId,
        bool esTurnoExtra,
        string currentUserRole,
        string currentUserId)
    {
        if (string.IsNullOrWhiteSpace(turnoId))
        {
            return TurnoChangeResult.Fail("Turno invalido.");
        }

        var actorScopeResult = await GetActorEquipoScopeAsync(currentUserRole, currentUserId);
        if (!actorScopeResult.Succeeded)
        {
            return TurnoChangeResult.Fail(actorScopeResult.Error ?? "No tiene permiso para actualizar turnos extra.");
        }
        var registro = await _db.RegistroTurnos
            .SingleOrDefaultAsync(rt => rt.TurnoId == turnoId);

        if (registro == null)
        {
            return TurnoChangeResult.Fail("Turno no existe.");
        }

        var turnoEquipoId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.PersonaId == registro.PersonaId)
            .Select(p => p.EquipoId)
            .FirstOrDefaultAsync();

        if (!IsWithinActorEquipoScope(actorScopeResult.Value, turnoEquipoId))
        {
            _logger.LogWarning(
                "Usuario {UserId} con rol {Role} intento actualizar EsTurnoExtra para TurnoId {TurnoId} fuera de su equipo.",
                currentUserId,
                currentUserRole,
                turnoId);
            return TurnoChangeResult.Fail("No tiene permiso para actualizar este turno.");
        }

        if (esTurnoExtra)
        {
            if (!ShouldCountAsWorkedShift(registro.TipoTurnoId, registro.NoLaboradoPorFeriado, registro.EsFeriado))
            {
                return TurnoChangeResult.Fail("No se puede marcar como extra un turno que no cuenta como laborado.");
            }
            if (registro.EsFeriado)
            {
                return TurnoChangeResult.Fail("No se puede marcar como extra un turno en dia feriado.");
            }

            if (registro.NoLaboradoPorFeriado)
            {
                return TurnoChangeResult.Fail("No se puede marcar como extra un turno no laborado por feriado.");
            }

            var weekStart = GetWeekStart(registro.FechaTurno);
            var weekEnd = weekStart.AddDays(6);

            var weekTurnos = await _db.RegistroTurnos
                .AsNoTracking()
                .Where(rt => rt.PersonaId == registro.PersonaId
                    && rt.FechaTurno >= weekStart
                    && rt.FechaTurno <= weekEnd)
                .ToListAsync();

            var weekWorkedCount = weekTurnos.Count(rt =>
                ShouldCountAsWorkedShift(rt.TipoTurnoId, rt.NoLaboradoPorFeriado, rt.EsFeriado));

            if (weekWorkedCount < MaxShiftsPerWeek + 1)
            {
                return TurnoChangeResult.Fail("Solo se puede marcar como extra cuando hay 6 o mas turnos en la semana.");
            }
        }

        registro.EsTurnoExtra = esTurnoExtra;

        try
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("Turno {TurnoId} actualizado manualmente: EsTurnoExtra={EsTurnoExtra}.",
                turnoId, esTurnoExtra);
            return TurnoChangeResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar EsTurnoExtra para turno {TurnoId}.", turnoId);
            return TurnoChangeResult.Fail("No se pudo actualizar el turno extra.");
        }
    }

    private async Task<TurnoChangeResult> ConfirmMoveAsync(
        RegistroTurnoMoveRequest? move,
        string currentUserRole,
        string currentUserId)
    {
        if (move == null || string.IsNullOrWhiteSpace(move.TurnoId))
        {
            return TurnoChangeResult.Fail("Movimiento invalido.");
        }

        var actorScopeResult = await GetActorEquipoScopeAsync(currentUserRole, currentUserId);
        if (!actorScopeResult.Succeeded)
        {
            return TurnoChangeResult.Fail(actorScopeResult.Error ?? "No tiene permiso para mover turnos.");
        }

        var registro = await _db.RegistroTurnos
            .SingleOrDefaultAsync(rt => rt.TurnoId == move.TurnoId);

        if (registro == null)
        {
            return TurnoChangeResult.Fail("Turno no existe.");
        }

        var turnoEquipoId = await _db.Personas
            .AsNoTracking()
            .Where(p => p.PersonaId == registro.PersonaId)
            .Select(p => p.EquipoId)
            .FirstOrDefaultAsync();

        if (!IsWithinActorEquipoScope(actorScopeResult.Value, turnoEquipoId))
        {
            _logger.LogWarning(
                "Usuario {UserId} con rol {Role} intento mover TurnoId {TurnoId} fuera de su equipo.",
                currentUserId,
                currentUserRole,
                move.TurnoId);
            return TurnoChangeResult.Fail("No tiene permiso para mover este turno.");
        }

        if (await AreAnyTurnosLinkedToActiveCalamidadReplacementAsync(new[] { registro.TurnoId }))
        {
            return TurnoChangeResult.Fail("No se puede mover un turno que ya actua como reemplazo de una calamidad.");
        }

        var tipo = await _db.TipoTurnos
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TipoTurnoId == move.TipoTurnoId);

        if (tipo == null)
        {
            return TurnoChangeResult.Fail("Tipo de turno no existe.");
        }

        var duplicate = await _db.RegistroTurnos
            .AsNoTracking()
            .AnyAsync(rt => rt.TurnoId != registro.TurnoId
                && rt.PersonaId == registro.PersonaId
                && rt.TipoTurnoId == move.TipoTurnoId
                && rt.FechaTurno == move.FechaTurno);

        if (duplicate)
        {
            return TurnoChangeResult.Fail("La persona ya tiene un turno en esa fecha y horario.");
        }

        registro.FechaTurno = move.FechaTurno;
        registro.TipoTurnoId = move.TipoTurnoId;

        var feriadoDates = await BuildFeriadoDateSetAsync(new List<DateOnly> { move.FechaTurno });
        var esFeriado = feriadoDates.Contains(move.FechaTurno);
        registro.EsFeriado = esFeriado;
        registro.NoLaboradoPorFeriado = false;

        try
        {
            await _db.SaveChangesAsync();
            _logger.LogDebug("Turno {TurnoId} movido a {Fecha} ({Tipo}).", registro.TurnoId, move.FechaTurno, move.TipoTurnoId);
            return TurnoChangeResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al mover turno {TurnoId}.", registro.TurnoId);
            return TurnoChangeResult.Fail("No se pudo guardar el movimiento.");
        }
    }

    private async Task<TurnoChangeResult> ConfirmSwapPersonaAsync(
        RegistroTurnoSwapRequest? swap,
        string currentUserRole,
        string currentUserId)
    {
        if (swap == null ||
            string.IsNullOrWhiteSpace(swap.First?.TurnoId) ||
            string.IsNullOrWhiteSpace(swap.Second?.TurnoId))
        {
            return TurnoChangeResult.Fail("Cambio invalido.");
        }

        var actorScopeResult = await GetActorEquipoScopeAsync(currentUserRole, currentUserId);
        if (!actorScopeResult.Succeeded)
        {
            return TurnoChangeResult.Fail(actorScopeResult.Error ?? "No tiene permiso para intercambiar turnos.");
        }

        var turnoIds = new[] { swap.First.TurnoId, swap.Second.TurnoId };
        var registros = await _db.RegistroTurnos
            .Where(rt => turnoIds.Contains(rt.TurnoId))
            .ToListAsync();

        if (registros.Count != 2)
        {
            return TurnoChangeResult.Fail("No se pudieron cargar los turnos.");
        }

        if (await AreAnyTurnosLinkedToActiveCalamidadReplacementAsync(turnoIds))
        {
            return TurnoChangeResult.Fail("No se pueden intercambiar turnos que esten vinculados a reemplazos de calamidad.");
        }

        var registroA = registros.Single(r => r.TurnoId == swap.First.TurnoId);
        var registroB = registros.Single(r => r.TurnoId == swap.Second.TurnoId);

        var personaIds = new[] { swap.First.PersonaId, swap.Second.PersonaId }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var scopePersonaIds = registros
            .Select(r => r.PersonaId)
            .Concat(personaIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var scopePersonas = await _db.Personas
            .AsNoTracking()
            .Where(p => scopePersonaIds.Contains(p.PersonaId))
            .Select(p => new { p.PersonaId, p.EquipoId, p.Borrado })
            .ToDictionaryAsync(p => p.PersonaId);

        if (scopePersonas.Count != scopePersonaIds.Count)
        {
            return TurnoChangeResult.Fail("Persona no existe.");
        }

        if (actorScopeResult.Value != null &&
            scopePersonas.Values.Any(p => !IsWithinActorEquipoScope(actorScopeResult.Value, p.EquipoId)))
        {
            _logger.LogWarning(
                "Usuario {UserId} con rol {Role} intento intercambiar turnos fuera de su equipo.",
                currentUserId,
                currentUserRole);
            return TurnoChangeResult.Fail("No tiene permiso para intercambiar turnos de otra area.");
        }

        if (personaIds.Any(id => !scopePersonas.TryGetValue(id, out var persona) || persona.Borrado))
        {
            return TurnoChangeResult.Fail("Persona no existe o fue borrada.");
        }

        var personaEquipos = scopePersonas.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.EquipoId);

        var grupos = await _db.Grupos
            .AsNoTracking()
            .Where(g => g.GrupoId == registroA.GrupoId || g.GrupoId == registroB.GrupoId)
            .ToDictionaryAsync(g => g.GrupoId);

        var personaGrupos = await _db.PersonaGrupos
            .AsNoTracking()
            .Where(pg => personaIds.Contains(pg.PersonaId)
                && (pg.GrupoId == registroA.GrupoId || pg.GrupoId == registroB.GrupoId))
            .Select(pg => new { pg.PersonaId, pg.GrupoId })
            .ToListAsync();

        var personaGrupoSet = new HashSet<(string PersonaId, string GrupoId)>(
            personaGrupos.Select(pg => (pg.PersonaId, pg.GrupoId)));

        if (!IsPersonaAllowedInGrupo(registroA, swap.First.PersonaId, personaEquipos, grupos, personaGrupoSet) ||
            !IsPersonaAllowedInGrupo(registroB, swap.Second.PersonaId, personaEquipos, grupos, personaGrupoSet))
        {
            return TurnoChangeResult.Fail("La persona no pertenece al grupo del turno.");
        }

        var duplicateA = await _db.RegistroTurnos
            .AsNoTracking()
            .AnyAsync(rt => rt.TurnoId != registroA.TurnoId
                && rt.PersonaId == swap.First.PersonaId
                && rt.TipoTurnoId == registroA.TipoTurnoId
                && rt.FechaTurno == registroA.FechaTurno);

        var duplicateB = await _db.RegistroTurnos
            .AsNoTracking()
            .AnyAsync(rt => rt.TurnoId != registroB.TurnoId
                && rt.PersonaId == swap.Second.PersonaId
                && rt.TipoTurnoId == registroB.TipoTurnoId
                && rt.FechaTurno == registroB.FechaTurno);

        if (duplicateA || duplicateB)
        {
            return TurnoChangeResult.Fail("La persona ya tiene un turno asignado en esa fecha.");
        }

        registroA.PersonaId = swap.First.PersonaId;
        registroB.PersonaId = swap.Second.PersonaId;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogDebug("Intercambio confirmado entre turnos {TurnoA} y {TurnoB}.", registroA.TurnoId, registroB.TurnoId);
            return TurnoChangeResult.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error al confirmar intercambio.");
            return TurnoChangeResult.Fail("No se pudo confirmar el intercambio.");
        }
    }

    private Task<bool> AreAnyTurnosLinkedToActiveCalamidadReplacementAsync(IEnumerable<string> turnoIds)
    {
        var ids = turnoIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
        {
            return Task.FromResult(false);
        }

        return (
            from reemplazo in _db.CalamidadReemplazos.AsNoTracking()
            join solicitud in _db.Solicitudes.AsNoTracking() on reemplazo.SolicitudId equals solicitud.SolicitudId
            where solicitud.EstadoSolicitud != SolicitudEstado.Rechazado
                && solicitud.EstadoSolicitud != SolicitudEstado.Cancelado
                && (ids.Contains(reemplazo.TurnoAusenteId) || ids.Contains(reemplazo.TurnoReemplazoId))
            select reemplazo.CalamidadReemplazoId)
            .AnyAsync();
    }

    private async Task<(List<RegistroTurno> turnosFiltrados, int turnosOmitidos)> FiltrarTurnosPorVacacionesAsync(
        List<RegistroTurno> turnos)
    {
        if (turnos == null || !turnos.Any())
        {
            return (new List<RegistroTurno>(), 0);
        }

        _logger.LogInformation("Iniciando filtrado de vacaciones para {TotalTurnos} turnos", turnos.Count);

        // 1. Obtener PersonaIds únicos y rango de fechas
        var personasIds = turnos
            .Select(t => t.PersonaId)
            .Distinct()
            .ToHashSet();

        var fechaMinima = turnos.Min(t => t.FechaTurno);
        var fechaMaxima = turnos.Max(t => t.FechaTurno);

        _logger.LogInformation("Buscando vacaciones para {NumPersonas} personas entre {FechaMin} y {FechaMax}",
            personasIds.Count, fechaMinima, fechaMaxima);

        // 2. Obtener vacaciones de todas las personas en el rango de fechas (una sola consulta optimizada)
        var vacaciones = await _db.Vacaciones
            .Include(v => v.Solicitud)
            .Where(v => v.Solicitud != null &&
                        v.Solicitud.PersonaSolicitanteId != null &&
                        v.Solicitud.EstadoSolicitud != SolicitudEstado.Rechazado &&
                        v.Solicitud.EstadoSolicitud != SolicitudEstado.Cancelado &&
                        personasIds.Contains(v.Solicitud.PersonaSolicitanteId) &&
                        v.FechaInicio <= fechaMaxima &&
                        v.FechaFin >= fechaMinima)
            .Select(v => new
            {
                PersonaId = v.Solicitud!.PersonaSolicitanteId!,
                v.FechaInicio,
                v.FechaFin
            })
            .ToListAsync();

        _logger.LogInformation("Se encontraron {NumVacaciones} períodos de vacaciones", vacaciones.Count);

        if (!vacaciones.Any())
        {
            _logger.LogInformation("No hay vacaciones registradas en el período. Todos los turnos serán guardados.");
            return (turnos, 0);
        }

        // 3. Crear diccionario de vacaciones por PersonaId para búsqueda eficiente O(1)
        var vacacionesPorPersona = vacaciones
            .GroupBy(v => v.PersonaId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(v => (v.FechaInicio, v.FechaFin)).ToList()
            );

        _logger.LogInformation("Vacaciones agrupadas por persona: {NumPersonasConVacaciones} personas tienen vacaciones",
            vacacionesPorPersona.Count);

        // 4. Filtrar turnos excluyendo los que caen en vacaciones
        var turnosFiltrados = new List<RegistroTurno>();
        int turnosOmitidos = 0;

        foreach (var turno in turnos)
        {
            // Verificar si la persona tiene vacaciones en esta fecha
            bool estaDeVacaciones = false;

            if (vacacionesPorPersona.TryGetValue(turno.PersonaId, out var vacacionesPersona))
            {
                estaDeVacaciones = vacacionesPersona.Any(v =>
                    turno.FechaTurno >= v.FechaInicio && turno.FechaTurno <= v.FechaFin);
            }

            if (estaDeVacaciones)
            {
                turnosOmitidos++;
                _logger.LogDebug("Turno omitido: PersonaId {PersonaId} está de vacaciones el {Fecha}",
                    turno.PersonaId, turno.FechaTurno);
            }
            else
            {
                turnosFiltrados.Add(turno);
            }
        }

        _logger.LogInformation("Filtrado completado: {TurnosFiltrados} turnos válidos, {TurnosOmitidos} turnos omitidos",
            turnosFiltrados.Count, turnosOmitidos);

        return (turnosFiltrados, turnosOmitidos);
    }

    private static bool IsPersonaAllowedInGrupo(
        RegistroTurno registro,
        string personaId,
        IReadOnlyDictionary<string, string?> personaEquipos,
        IReadOnlyDictionary<string, Grupo> grupos,
        HashSet<(string PersonaId, string GrupoId)> personaGrupoSet)
    {
        if (string.IsNullOrWhiteSpace(personaId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(registro.GrupoId))
        {
            return true;
        }

        if (!personaEquipos.TryGetValue(personaId, out var personaEquipoId))
        {
            return false;
        }

        if (!grupos.TryGetValue(registro.GrupoId, out var grupo))
        {
            return false;
        }

        if (!string.Equals(grupo.EquipoId, personaEquipoId, StringComparison.Ordinal))
        {
            return false;
        }

        return personaGrupoSet.Contains((personaId, registro.GrupoId));
    }
}
