using System;
using System.Security.Claims;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TurneroTcs.Data;
using TurneroTcs.Services.Interfaces;

namespace TurneroTcs.Services;

public class PermisoAccesoResolver : IPermisoAccesoResolver
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public PermisoAccesoResolver(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<bool> TienePermisoAsync(ClaimsPrincipal user, string codigoPermiso, CancellationToken ct = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole("SuperAdmin"))
        {
            return true;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await TienePermisoAsync(userId, codigoPermiso, ct);
    }

    public async Task<bool> TienePermisoAsync(string userId, string codigoPermiso, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(codigoPermiso))
        {
            return false;
        }

        var permisos = await ObtenerPermisosEfectivosAsync(userId, ct);
        return permisos.Any(p => string.Equals(p, codigoPermiso, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyCollection<string>> ObtenerPermisosEfectivosAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<string>();
        }

        var cacheKey = BuildCacheKey(userId);
        if (_cache.TryGetValue(cacheKey, out string[]? cached) && cached != null)
        {
            return cached;
        }

        var rolePermissionCodes = await (
            from ur in _db.UserRoles.AsNoTracking()
            join rp in _db.RolesPermisosAcceso.AsNoTracking() on ur.RoleId equals rp.RoleId
            join pa in _db.PermisosAcceso.AsNoTracking() on rp.PermisoAccesoId equals pa.PermisoAccesoId
            where ur.UserId == userId
            select pa.CodigoPermiso)
            .Distinct()
            .ToListAsync(ct);

        var userOverrides = await (
            from up in _db.UsuariosPermisosAcceso.AsNoTracking()
            join pa in _db.PermisosAcceso.AsNoTracking() on up.PermisoAccesoId equals pa.PermisoAccesoId
            where up.UserId == userId
            select new
            {
                pa.CodigoPermiso,
                up.EsDenegado
            })
            .ToListAsync(ct);

        var effective = new HashSet<string>(rolePermissionCodes, StringComparer.OrdinalIgnoreCase);
        foreach (var item in userOverrides.Where(x => !x.EsDenegado))
        {
            effective.Add(item.CodigoPermiso);
        }

        foreach (var item in userOverrides.Where(x => x.EsDenegado))
        {
            effective.Remove(item.CodigoPermiso);
        }

        var result = effective.OrderBy(x => x).ToArray();
        _cache.Set(
            cacheKey,
            result,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(10),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            });

        return result;
    }

    public Task InvalidarCacheUsuarioAsync(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _cache.Remove(BuildCacheKey(userId));
        }

        return Task.CompletedTask;
    }

    private static string BuildCacheKey(string userId) => $"permiso-acceso:efectivo:{userId}";
}
