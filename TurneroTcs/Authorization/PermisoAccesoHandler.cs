using Microsoft.AspNetCore.Authorization;
using TurneroTcs.Services.Interfaces;

namespace TurneroTcs.Authorization;

public class PermisoAccesoHandler : AuthorizationHandler<PermisoAccesoRequirement>
{
    private readonly IPermisoAccesoResolver _resolver;

    public PermisoAccesoHandler(IPermisoAccesoResolver resolver)
    {
        _resolver = resolver;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermisoAccesoRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var hasPermission = await _resolver.TienePermisoAsync(context.User, requirement.CodigoPermiso);
        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
