using Microsoft.AspNetCore.Authorization;

namespace TurneroTcs.Authorization;

public class PermisoAccesoRequirement : IAuthorizationRequirement
{
    public PermisoAccesoRequirement(string codigoPermiso)
    {
        CodigoPermiso = codigoPermiso ?? string.Empty;
    }

    public string CodigoPermiso { get; }
}
