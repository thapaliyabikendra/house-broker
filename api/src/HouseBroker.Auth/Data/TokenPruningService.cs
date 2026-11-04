using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;

namespace HouseBroker.Auth.Data;

/// <summary>Periodically prunes expired OpenIddict tokens and authorizations.</summary>
public sealed class TokenPruningService(
    IOpenIddictTokenManager tokenManager,
    IOpenIddictAuthorizationManager authorizationManager,
    TimeProvider clock) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await tokenManager.PruneAsync(clock.GetUtcNow(), stoppingToken);
            await authorizationManager.PruneAsync(clock.GetUtcNow(), stoppingToken);

            await Task.Delay(TimeSpan.FromHours(1), clock, stoppingToken);
        }
    }
}