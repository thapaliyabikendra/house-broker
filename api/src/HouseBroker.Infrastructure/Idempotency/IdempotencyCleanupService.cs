using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HouseBroker.Infrastructure.Idempotency;

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
            var runId = Guid.NewGuid().ToString("N");
            using var logScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = runId,
            });

            try
            {
                _logger.LogInformation("Idempotency key cleanup run started. CorrelationId {CorrelationId}.", runId);

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = _clock.GetUtcNow().UtcDateTime;
                var deleted = await dbContext.IdempotencyKeys
                    .Where(key => key.ExpiresAt <= now)
                    .ExecuteDeleteAsync(stoppingToken);

                _logger.LogInformation("Idempotency key cleanup run completed. DeletedCount {DeletedCount}. CorrelationId {CorrelationId}.", deleted, runId);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Idempotency key cleanup failed. CorrelationId {CorrelationId}.", runId);
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
