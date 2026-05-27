using System.Security.Claims;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurneroTcs.Services.Interfaces;

public interface IPermisoAccesoResolver
{
    Task<bool> TienePermisoAsync(ClaimsPrincipal user, string codigoPermiso, CancellationToken ct = default);
    Task<bool> TienePermisoAsync(string userId, string codigoPermiso, CancellationToken ct = default);
    Task<IReadOnlyCollection<string>> ObtenerPermisosEfectivosAsync(string userId, CancellationToken ct = default);
    Task InvalidarCacheUsuarioAsync(string userId);
}
