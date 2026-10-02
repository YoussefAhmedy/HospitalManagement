using Hospital.DAL.DataBase;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HospitalManagement.Infrastructure;

public sealed class DatabaseHealthCheck(HospitalDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database is reachable.")
                : HealthCheckResult.Unhealthy("Database is not reachable.");
        }
        catch
        {
            // Do not expose connection strings, server names, or driver details to probes.
            return HealthCheckResult.Unhealthy("Database is not reachable.");
        }
    }
}
