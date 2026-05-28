using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Security;
using TurneroTcs.Services.Interfaces;

namespace TurneroTcs.Services;

public class PermisoAccesoService : IPermisoAccesoService
{
    private static readonly Regex PermissionCodeRegex = new(
        "^[a-z0-9]+(?:\\.[a-z0-9_]+)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ApplicationDbContext _db;
    private readonly IPermisoAccesoResolver _permisoAccesoResolver;

    public PermisoAccesoService(
        ApplicationDbContext db,
        IPermisoAccesoResolver permisoAccesoResolver)
    {
        _db = db;
        _permisoAccesoResolver = permisoAccesoResolver;
    }

    public async Task<IReadOnlyList<PermisoAccesoListItem>> GetAllAsync()
    {
        var permisos = await _db.PermisosAcceso
            .AsNoTracking()
            .OrderBy(p => p.Modulo)
            .ThenBy(p => p.CodigoPermiso)
            .ToListAsync();

        var roleLinks = await (
            from rp in _db.RolesPermisosAcceso.AsNoTracking()
            join role in _db.Roles.AsNoTracking() on rp.RoleId equals role.Id
            select new
            {
                rp.PermisoAccesoId,
                rp.RoleId,
                RoleName = role.Name ?? string.Empty
            })
            .ToListAsync();

        var roleLookup = roleLinks
            .GroupBy(x => x.PermisoAccesoId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    RoleIds = g.Select(x => x.RoleId).Distinct().OrderBy(x => x).ToList(),
                    RoleNames = g.Select(x => x.RoleName).Distinct().OrderBy(x => x).ToList()
                });

        var userLinks = await _db.UsuariosPermisosAcceso
            .AsNoTracking()
            .Select(x => new
            {
                x.PermisoAccesoId,
                x.EsDenegado
            })
            .ToListAsync();

        var userLookup = userLinks
            .GroupBy(x => x.PermisoAccesoId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Allow = g.Count(x => !x.EsDenegado),
                    Deny = g.Count(x => x.EsDenegado)
                });

        return permisos
            .Select(p =>
            {
                var roles = roleLookup.TryGetValue(p.PermisoAccesoId, out var data)
                    ? data
                    : new { RoleIds = new List<string>(), RoleNames = new List<string>() };
                var users = userLookup.TryGetValue(p.PermisoAccesoId, out var userData)
                    ? userData
                    : new { Allow = 0, Deny = 0 };

                return new PermisoAccesoListItem(
                    p.PermisoAccesoId,
                    p.CodigoPermiso,
                    p.NombrePermiso,
                    p.Descripcion,
                    p.Modulo,
                    roles.RoleIds,
                    roles.RoleNames,
                    users.Allow,
                    users.Deny);
            })
            .ToList();
    }

    public async Task<Result> PatchAsync(string permisoAccesoId, PermisoAccesoPatchRequest request, string currentUserId, string currentUserRole)
    {
        var authorize = await EnsureCanAsync(currentUserId, currentUserRole, PermisosAccesoCodigos.PermisoAccesoEditar);
        if (authorize != null)
        {
            return authorize;
        }

        if (string.IsNullOrWhiteSpace(permisoAccesoId))
        {
            return Result.Fail("Permiso es requerido.");
        }

        var permiso = await _db.PermisosAcceso
            .SingleOrDefaultAsync(p => p.PermisoAccesoId == permisoAccesoId);
        if (permiso == null)
        {
            return Result.Fail("Permiso no existe.");
        }

        if (request.CodigoPermiso != null)
        {
            var nextCodigo = NormalizeCode(request.CodigoPermiso);
            if (string.IsNullOrWhiteSpace(nextCodigo))
            {
                return Result.Fail("El codigo del permiso es requerido.");
            }

            if (!PermissionCodeRegex.IsMatch(nextCodigo))
            {
                return Result.Fail("El codigo del permiso debe tener formato modulo.accion.");
            }

            if (permiso.EsSistema && !string.Equals(permiso.CodigoPermiso, nextCodigo, StringComparison.Ordinal))
            {
                return Result.Fail("No se puede cambiar el codigo de un permiso del sistema.");
            }

            var duplicated = await _db.PermisosAcceso
                .AnyAsync(p => p.PermisoAccesoId != permisoAccesoId && p.CodigoPermiso == nextCodigo);
            if (duplicated)
            {
                return Result.Fail("Ya existe un permiso con ese codigo.");
            }

            permiso.CodigoPermiso = nextCodigo;
        }

        if (request.NombrePermiso != null)
        {
            var nombre = request.NombrePermiso.Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                return Result.Fail("El nombre del permiso es requerido.");
            }

            permiso.NombrePermiso = nombre;
        }

        if (request.Modulo != null)
        {
            var modulo = request.Modulo.Trim();
            if (string.IsNullOrWhiteSpace(modulo))
            {
                return Result.Fail("El modulo es requerido.");
            }

            permiso.Modulo = modulo;
        }

        if (request.Descripcion != null)
        {
            permiso.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion)
                ? null
                : request.Descripcion.Trim();
        }

        await _db.SaveChangesAsync();
        await InvalidateUsersByPermissionAsync(permisoAccesoId);
        return Result.Ok();
    }

    public async Task<Result> AssignRoleAsync(string permisoAccesoId, string roleId, string currentUserId, string currentUserRole)
    {
        var authorize = await EnsureCanAsync(currentUserId, currentUserRole, PermisosAccesoCodigos.PermisoAccesoAsignarRol);
        if (authorize != null)
        {
            return authorize;
        }

        if (string.IsNullOrWhiteSpace(permisoAccesoId) || string.IsNullOrWhiteSpace(roleId))
        {
            return Result.Fail("Permiso y rol son requeridos.");
        }

        var permissionExists = await _db.PermisosAcceso
            .AnyAsync(x => x.PermisoAccesoId == permisoAccesoId);
        if (!permissionExists)
        {
            return Result.Fail("Permiso no existe.");
        }

        var roleExists = await _db.Roles
            .AnyAsync(r => r.Id == roleId);
        if (!roleExists)
        {
            return Result.Fail("Rol no existe.");
        }

        var linkExists = await _db.RolesPermisosAcceso
            .AnyAsync(x => x.PermisoAccesoId == permisoAccesoId && x.RoleId == roleId);
        if (!linkExists)
        {
            _db.RolesPermisosAcceso.Add(new RolPermisoAcceso
            {
                PermisoAccesoId = permisoAccesoId,
                RoleId = roleId
            });
            await _db.SaveChangesAsync();
        }

        await InvalidateUsersByRoleAsync(roleId);
        return Result.Ok();
    }

    public async Task<Result> UnassignRoleAsync(string permisoAccesoId, string roleId, string currentUserId, string currentUserRole)
    {
        var authorize = await EnsureCanAsync(currentUserId, currentUserRole, PermisosAccesoCodigos.PermisoAccesoAsignarRol);
        if (authorize != null)
        {
            return authorize;
        }

        if (string.IsNullOrWhiteSpace(permisoAccesoId) || string.IsNullOrWhiteSpace(roleId))
        {
            return Result.Fail("Permiso y rol son requeridos.");
        }

        var link = await _db.RolesPermisosAcceso
            .SingleOrDefaultAsync(x => x.PermisoAccesoId == permisoAccesoId && x.RoleId == roleId);
        if (link != null)
        {
            _db.RolesPermisosAcceso.Remove(link);
            await _db.SaveChangesAsync();
        }

        await InvalidateUsersByRoleAsync(roleId);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<PermisoAccesoUserListItem>> GetUsersAsync(string permisoAccesoId)
    {
        if (string.IsNullOrWhiteSpace(permisoAccesoId))
        {
            return Array.Empty<PermisoAccesoUserListItem>();
        }

        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new
            {
                u.Id,
                UserName = u.UserName ?? string.Empty
            })
            .ToListAsync();

        var links = await _db.UsuariosPermisosAcceso
            .AsNoTracking()
            .Where(x => x.PermisoAccesoId == permisoAccesoId)
            .Select(x => new
            {
                x.UserId,
                x.EsDenegado
            })
            .ToDictionaryAsync(x => x.UserId, x => x.EsDenegado);

        return users
            .Select(u =>
            {
                var estado = "none";
                if (links.TryGetValue(u.Id, out var esDenegado))
                {
                    estado = esDenegado ? "deny" : "allow";
                }

                return new PermisoAccesoUserListItem(u.Id, u.UserName, estado);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<PermisoAccesoUserPermissionItem>> GetUserPermissionsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<PermisoAccesoUserPermissionItem>();
        }

        var userRoleIds = await _db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        List<(string PermisoAccesoId, string RoleName)> roleGrantRows;
        if (userRoleIds.Count == 0)
        {
            roleGrantRows = new List<(string PermisoAccesoId, string RoleName)>();
        }
        else
        {
            var roleGrantRawRows = await (
                from rp in _db.RolesPermisosAcceso.AsNoTracking()
                join role in _db.Roles.AsNoTracking() on rp.RoleId equals role.Id
                where userRoleIds.Contains(rp.RoleId)
                select new
                {
                    rp.PermisoAccesoId,
                    RoleName = role.Name ?? string.Empty
                })
                .ToListAsync();

            roleGrantRows = roleGrantRawRows
                .Select(x => (x.PermisoAccesoId, x.RoleName))
                .ToList();
        }

        var roleLookup = roleGrantRows
            .GroupBy(x => x.PermisoAccesoId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.RoleName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList());

        var directLookup = await _db.UsuariosPermisosAcceso
            .AsNoTracking()
            .Where(up => up.UserId == userId)
            .ToDictionaryAsync(up => up.PermisoAccesoId, up => up.EsDenegado);

        var permisos = await _db.PermisosAcceso
            .AsNoTracking()
            .OrderBy(p => p.Modulo)
            .ThenBy(p => p.CodigoPermiso)
            .ToListAsync();

        return permisos.Select(permiso =>
        {
            var grantedByRole = roleLookup.ContainsKey(permiso.PermisoAccesoId);

            var directAssignment = "none";
            if (directLookup.TryGetValue(permiso.PermisoAccesoId, out var isDenied))
            {
                directAssignment = isDenied ? "deny" : "allow";
            }

            var effective = directAssignment switch
            {
                "deny" => "deny",
                "allow" => "allow",
                _ => grantedByRole ? "allow" : "deny"
            };

            var roleNames = roleLookup.TryGetValue(permiso.PermisoAccesoId, out var roles)
                ? roles
                : new List<string>();

            return new PermisoAccesoUserPermissionItem(
                permiso.PermisoAccesoId,
                permiso.CodigoPermiso,
                permiso.NombrePermiso,
                permiso.Modulo,
                permiso.EsSistema,
                grantedByRole,
                directAssignment,
                effective,
                roleNames);
        }).ToList();
    }

    public async Task<Result> AssignUserAsync(string permisoAccesoId, string userId, bool esDenegado, string currentUserId, string currentUserRole)
    {
        var authorize = await EnsureCanAsync(currentUserId, currentUserRole, PermisosAccesoCodigos.PermisoAccesoAsignarUsuario);
        if (authorize != null)
        {
            return authorize;
        }

        if (string.IsNullOrWhiteSpace(permisoAccesoId) || string.IsNullOrWhiteSpace(userId))
        {
            return Result.Fail("Permiso y usuario son requeridos.");
        }

        var permissionExists = await _db.PermisosAcceso
            .AnyAsync(x => x.PermisoAccesoId == permisoAccesoId);
        if (!permissionExists)
        {
            return Result.Fail("Permiso no existe.");
        }

        var userExists = await _db.Users
            .AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return Result.Fail("Usuario no existe.");
        }

        var link = await _db.UsuariosPermisosAcceso
            .SingleOrDefaultAsync(x => x.PermisoAccesoId == permisoAccesoId && x.UserId == userId);
        if (link == null)
        {
            _db.UsuariosPermisosAcceso.Add(new UsuarioPermisoAcceso
            {
                PermisoAccesoId = permisoAccesoId,
                UserId = userId,
                EsDenegado = esDenegado
            });
        }
        else
        {
            link.EsDenegado = esDenegado;
        }

        await _db.SaveChangesAsync();
        await _permisoAccesoResolver.InvalidarCacheUsuarioAsync(userId);
        return Result.Ok();
    }

    public async Task<Result> UnassignUserAsync(string permisoAccesoId, string userId, string currentUserId, string currentUserRole)
    {
        var authorize = await EnsureCanAsync(currentUserId, currentUserRole, PermisosAccesoCodigos.PermisoAccesoAsignarUsuario);
        if (authorize != null)
        {
            return authorize;
        }

        if (string.IsNullOrWhiteSpace(permisoAccesoId) || string.IsNullOrWhiteSpace(userId))
        {
            return Result.Fail("Permiso y usuario son requeridos.");
        }

        var link = await _db.UsuariosPermisosAcceso
            .SingleOrDefaultAsync(x => x.PermisoAccesoId == permisoAccesoId && x.UserId == userId);
        if (link != null)
        {
            _db.UsuariosPermisosAcceso.Remove(link);
            await _db.SaveChangesAsync();
        }

        await _permisoAccesoResolver.InvalidarCacheUsuarioAsync(userId);
        return Result.Ok();
    }

    private async Task<Result?> EnsureCanAsync(string currentUserId, string currentUserRole, string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(currentUserRole))
        {
            return Result.Fail("No se ha podido identificar el rol del usuario.");
        }

        if (string.Equals(currentUserRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Result.Fail("No se ha podido identificar el usuario autenticado.");
        }

        var hasPermission = await _permisoAccesoResolver.TienePermisoAsync(currentUserId, permissionCode);
        if (!hasPermission)
        {
            return Result.Fail("No tiene permiso para realizar esta accion.");
        }

        return null;
    }

    private async Task InvalidateUsersByPermissionAsync(string permisoAccesoId)
    {
        var userIds = await GetUsersAffectedByPermissionAsync(permisoAccesoId);
        await InvalidateUsersAsync(userIds);
    }

    private async Task<List<string>> GetUsersAffectedByPermissionAsync(string permisoAccesoId)
    {
        var userIdsByUserAssignment = await _db.UsuariosPermisosAcceso
            .AsNoTracking()
            .Where(x => x.PermisoAccesoId == permisoAccesoId)
            .Select(x => x.UserId)
            .ToListAsync();

        var roleIds = await _db.RolesPermisosAcceso
            .AsNoTracking()
            .Where(x => x.PermisoAccesoId == permisoAccesoId)
            .Select(x => x.RoleId)
            .ToListAsync();

        var userIdsByRoleAssignment = roleIds.Count == 0
            ? new List<string>()
            : await _db.UserRoles
                .AsNoTracking()
                .Where(ur => roleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .ToListAsync();

        return userIdsByUserAssignment
            .Concat(userIdsByRoleAssignment)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task InvalidateUsersByRoleAsync(string roleId)
    {
        var userIds = await _db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .ToListAsync();

        await InvalidateUsersAsync(userIds);
    }

    private async Task InvalidateUsersAsync(IEnumerable<string> userIds)
    {
        foreach (var userId in userIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
        {
            await _permisoAccesoResolver.InvalidarCacheUsuarioAsync(userId);
        }
    }

    private static string NormalizeCode(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();
}
