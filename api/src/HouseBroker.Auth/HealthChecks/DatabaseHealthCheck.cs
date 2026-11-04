#pragma warning disable CA1031
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HouseBroker.Auth.HealthChecks;

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
