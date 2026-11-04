using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;

namespace HouseBroker.Auth.Data;

/// <summary>Periodically prunes expired OpenIddict tokens and authorizations.</summary>
public sealed class TokenPruningService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var tokenManager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
            var authorizationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();

            await tokenManager.PruneAsync(clock.GetUtcNow(), stoppingToken);
            await authorizationManager.PruneAsync(clock.GetUtcNow(), stoppingToken);

            await Task.Delay(TimeSpan.FromHours(1), clock, stoppingToken);
        }
    }
}
