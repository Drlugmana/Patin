using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace TurneroTcs.Authorization;

public class PermisoAccesoPolicyProvider : IAuthorizationPolicyProvider
{
    public const string PolicyPrefix = "permiso:";

    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

    public PermisoAccesoPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => _fallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => _fallbackPolicyProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var codigoPermiso = policyName[PolicyPrefix.Length..];
            if (string.IsNullOrWhiteSpace(codigoPermiso))
            {
                return Task.FromResult<AuthorizationPolicy?>(null);
            }

            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermisoAccesoRequirement(codigoPermiso))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }
}
