using System.Collections.Generic;
using System.Threading.Tasks;
using TurneroTcs.Records;

namespace TurneroTcs.Services.Interfaces;

public interface IPermisoAccesoService
{
    Task<IReadOnlyList<PermisoAccesoListItem>> GetAllAsync();
    Task<Result> PatchAsync(string permisoAccesoId, PermisoAccesoPatchRequest request, string currentUserId, string currentUserRole);
    Task<Result> AssignRoleAsync(string permisoAccesoId, string roleId, string currentUserId, string currentUserRole);
    Task<Result> UnassignRoleAsync(string permisoAccesoId, string roleId, string currentUserId, string currentUserRole);
    Task<IReadOnlyList<PermisoAccesoUserListItem>> GetUsersAsync(string permisoAccesoId);
    Task<IReadOnlyList<PermisoAccesoUserPermissionItem>> GetUserPermissionsAsync(string userId);
    Task<Result> AssignUserAsync(string permisoAccesoId, string userId, bool esDenegado, string currentUserId, string currentUserRole);
    Task<Result> UnassignUserAsync(string permisoAccesoId, string userId, string currentUserId, string currentUserRole);
}
