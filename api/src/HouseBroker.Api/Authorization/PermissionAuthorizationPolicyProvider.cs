
namespace HouseBroker.Api.Authorization;

/// <summary>Permission-aware policy provider.</summary>
public sealed class PermissionAuthorizationPolicyProvider(IAuthorizationPolicyProvider fallback) : IAuthorizationPolicyProvider
{
    private readonly IAuthorizationPolicyProvider _fallback = fallback;

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("Permission:", StringComparison.Ordinal))
        {
            var permission = policyName["Permission:".Length..];
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
