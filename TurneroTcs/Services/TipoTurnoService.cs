using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Services.Interfaces;

namespace TurneroTcs.Services;

public class TipoTurnoService : ITipoTurnoService
{
    private readonly ApplicationDbContext _db;

    private readonly ILogger<TipoTurnoService> _logger;

    public TipoTurnoService (ApplicationDbContext db, ILogger<TipoTurnoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TipoTurno>> GetAllAsync()
    {
        try
        {
            var tipoTurnos = await _db.TipoTurnos
                .AsNoTracking()
                .OrderBy(p => p.NombreTurno)
                .ToListAsync();

                _logger.LogDebug("Lista de {Count} tipos de turno traidos exitosamente.", tipoTurnos.Count);
                return tipoTurnos;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la lista de tipo de turnos.");
            throw;
        }
    }
    public async Task<Result> CreateTurno(TipoTurno tipoTurno)
    {
        if (tipoTurno == null)
        {
            return Result.Fail("No se ha ingresa ning??n tipo de turno.");
        }

        if (string.IsNullOrWhiteSpace(tipoTurno.NombreTurno))
        {
            return Result.Fail("No se ha ingresado nombre del tipo de turno.");
        }

        var start = tipoTurno.HoraInicio.ToTimeSpan();
        var end = tipoTurno.HoraFin.ToTimeSpan();
        var duration = end >= start
            ? end - start
            : end + TimeSpan.FromHours(24) - start;
        if (duration <= TimeSpan.Zero)
        {
            return Result.Fail("La hora de inicio no puede ser igual o mayor a la hora de finalizacion.");
        }

        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var max = _db.TipoTurnos
                .AsNoTracking()
                .Where(t => t.TipoTurnoId.StartsWith("TT"))
                .Select(t => t.TipoTurnoId)
                .AsEnumerable()
                .Select(id => int.TryParse(id.Substring(2), out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();

            tipoTurno.TipoTurnoId = $"TT{(max + 1):D2}";

            _db.TipoTurnos.Add(tipoTurno);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogDebug("El tipo de turno {TipoTurno} ha sido creado satisfactoriamente", tipoTurno.NombreTurno);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            _logger.LogError(ex, "Error al crear el tipo de turno {TipoTurno}.", tipoTurno.NombreTurno);
            return Result.Fail("Ha ocurrido un error al crear el tipo de turno.");
        }
    }

    public async Task<Result> PatchAsync(string tipoTurnoId, TipoTurnoPatchRequest request, string currentUserRole)
    {
        if (string.IsNullOrWhiteSpace(currentUserRole))
        {
            return Result.Fail("No se ha podido identificar el rol del usuario.");
        }

        if (currentUserRole != "SuperAdmin" && currentUserRole != "Admin")
        {
            return Result.Fail("No tiene permiso para realizar esta accion.");
        }

        var turno = await _db.TipoTurnos.SingleOrDefaultAsync(t => t.TipoTurnoId == tipoTurnoId);
        if (turno == null)
        {
            return Result.Fail("Tipo de turno no existe.");
        }

        if (request.NombreTurno != null)
        {
            if (string.IsNullOrWhiteSpace(request.NombreTurno))
            {
                return Result.Fail("El nombre del tipo de turno es requerido.");
            }

            turno.NombreTurno = request.NombreTurno;
        }

        var inicio = request.HoraInicio ?? turno.HoraInicio;
        var fin = request.HoraFin ?? turno.HoraFin;
        if (request.HoraInicio.HasValue || request.HoraFin.HasValue)
        {
            var start = inicio.ToTimeSpan();
            var end = fin.ToTimeSpan();
            var duration = end >= start
                ? end - start
                : end + TimeSpan.FromHours(24) - start;
            if (duration <= TimeSpan.Zero)
            {
                return Result.Fail("La hora de inicio no puede ser igual o mayor a la hora de finalizacion.");
            }

            turno.HoraInicio = inicio;
            turno.HoraFin = fin;
        }

        if (request.Activo.HasValue)
        {
            turno.Activo = request.Activo.Value;
        }

        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(string tipoTurnoId, string currentUserRole)
    {
        if (string.IsNullOrWhiteSpace(currentUserRole))
        {
            return Result.Fail("No se ha podido identificar el rol del usuario.");
        }

        if (currentUserRole != "SuperAdmin" && currentUserRole != "Admin")
        {
            return Result.Fail("No tiene permiso para realizar esta accion.");
        }

        var turno = await _db.TipoTurnos.SingleOrDefaultAsync(t => t.TipoTurnoId == tipoTurnoId);
        if (turno == null)
        {
            return Result.Fail("Tipo de turno no existe.");
        }

        var hasRegistros = await _db.RegistroTurnos
            .AsNoTracking()
            .AnyAsync(rt => rt.TipoTurnoId == tipoTurnoId);
        if (hasRegistros)
        {
            return Result.Fail("No se puede eliminar el turno porque tiene registros relacionados.");
        }

        try
        {
            _db.TipoTurnos.Remove(turno);
            await _db.SaveChangesAsync();
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteAsync fallo inesperadamente para TipoTurnoId {TipoTurnoId}.", tipoTurnoId);
            return Result.Fail("Eliminar tipo de turno fallo debido a un error inesperado.");
        }
    }
}

