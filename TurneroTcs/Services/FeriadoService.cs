using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Services.Interfaces;

namespace TurneroTcs.Services;

public class FeriadoService : IFeriadoService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<FeriadoService> _logger;

    public FeriadoService(ApplicationDbContext db, ILogger<FeriadoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Feriado>> GetAllAsync()
    {
        var list = await _db.Feriados
            .AsNoTracking()
            .OrderBy(f => f.InicioFeriado)
            .ThenBy(f => f.NombreFeriado)
            .ToListAsync();
        _logger.LogDebug("Feriados cargados: {Count}", list.Count);
        return list;
    }

    public async Task<Result> CreateAsync(FeriadoCreateRequest request)
    {
        var validation = ValidateRequest(request);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var entity = new Feriado
        {
            NombreFeriado = request.NombreFeriado.Trim(),
            InicioFeriado = request.InicioFeriado,
            FinFeriado = request.FinFeriado
        };

        _db.Feriados.Add(entity);
        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> PatchAsync(string feriadoId, FeriadoPatchRequest request, string currentUserRole)
    {
        if (string.IsNullOrWhiteSpace(currentUserRole))
        {
            return Result.Fail("No se ha podido identificar el rol del usuario.");
        }

        if (currentUserRole is not ("SuperAdmin" or "Admin"))
        {
            return Result.Fail("No tiene permiso para realizar esta accion.");
        }

        var entity = await _db.Feriados.SingleOrDefaultAsync(f => f.FeriadoId == feriadoId);
        if (entity == null)
        {
            return Result.Fail("Feriado no existe.");
        }

        if (request.NombreFeriado != null)
        {
            if (string.IsNullOrWhiteSpace(request.NombreFeriado))
            {
                return Result.Fail("El nombre del feriado es requerido.");
            }

            entity.NombreFeriado = request.NombreFeriado.Trim();
        }

        var inicio = request.InicioFeriado ?? entity.InicioFeriado;
        var fin = request.FinFeriado ?? entity.FinFeriado;
        if (fin < inicio)
        {
            return Result.Fail("La fecha fin no puede ser menor a la fecha inicio.");
        }

        if (request.InicioFeriado.HasValue)
        {
            entity.InicioFeriado = request.InicioFeriado.Value;
        }

        if (request.FinFeriado.HasValue)
        {
            entity.FinFeriado = request.FinFeriado.Value;
        }

        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(string feriadoId, string currentUserRole)
    {
        if (string.IsNullOrWhiteSpace(currentUserRole))
        {
            return Result.Fail("No se ha podido identificar el rol del usuario.");
        }

        if (currentUserRole is not ("SuperAdmin" or "Admin"))
        {
            return Result.Fail("No tiene permiso para realizar esta accion.");
        }

        var entity = await _db.Feriados.SingleOrDefaultAsync(f => f.FeriadoId == feriadoId);
        if (entity == null)
        {
            return Result.Fail("Feriado no existe.");
        }

        _db.Feriados.Remove(entity);
        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    private static Result ValidateRequest(FeriadoCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreFeriado))
        {
            return Result.Fail("El nombre del feriado es requerido.");
        }

        if (request.NombreFeriado.Trim().Length > 120)
        {
            return Result.Fail("El nombre del feriado excede 120 caracteres.");
        }

        if (request.FinFeriado < request.InicioFeriado)
        {
            return Result.Fail("La fecha fin no puede ser menor a la fecha inicio.");
        }

        return Result.Ok();
    }
}
