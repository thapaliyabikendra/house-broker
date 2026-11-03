using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HouseBroker.Infrastructure;

/// <summary>Cleans expired idempotency keys.</summary>
public sealed class IdempotencyCleanupService(IServiceProvider serviceProvider, TimeProvider clock, ILogger<IdempotencyCleanupService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger<IdempotencyCleanupService> _logger = logger;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = _clock.GetUtcNow().UtcDateTime;
                await dbContext.IdempotencyKeys
                    .Where(key => key.ExpiresAt <= now)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Idempotency key cleanup failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
