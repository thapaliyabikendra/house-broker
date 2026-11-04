#pragma warning disable CA1031
using System.Globalization;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HouseBroker.Api.HealthChecks;

/// <summary>Checks SQL Server reachability and clock drift.</summary>
public sealed class DatabaseHealthCheck(string connectionString, TimeProvider timeProvider) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT SYSUTCDATETIME();";
            var databaseUtc = Convert.ToDateTime(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            var driftSeconds = Math.Abs((timeProvider.GetUtcNow().UtcDateTime - databaseUtc).TotalSeconds);
            var data = new Dictionary<string, object> { ["driftSeconds"] = driftSeconds };
            if (driftSeconds > 5)
            {
                return HealthCheckResult.Degraded($"Database clock drift is {driftSeconds:F1}s.", exception: null, data);
            }

            return HealthCheckResult.Healthy("Database reachable.", data);
        }
#pragma warning disable CA1031
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database unreachable.", exception);
        }
#pragma warning restore CA1031
    }
}

/// <summary>Checks basic TCP reachability for the configured Redis instance.</summary>
public sealed class RedisHealthCheck(string configuration) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        try
        {
            var target = configuration.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "localhost:6379";
            var parts = target.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var host = parts.Length > 0 ? parts[0] : "localhost";
            var port = parts.Length > 1 && int.TryParse(parts[1], out var parsed) ? parsed : 6379;

#pragma warning disable CA1031
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port, cancellationToken).AsTask();
            var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(3), cancellationToken));
            if (completed != connectTask)
            {
                return HealthCheckResult.Unhealthy("Redis connection timed out.");
            }

            await connectTask;
            return HealthCheckResult.Healthy("Redis reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis unreachable.", exception);
        }
    }
}
