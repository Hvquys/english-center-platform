using EnglishCenter.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnglishCenter.Api.Modules.Health;

public sealed class DatabaseHealthCheck(IServiceScopeFactory serviceScopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnglishCenterDbContext>();

        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("SQL Server connection succeeded.")
                : HealthCheckResult.Unhealthy("SQL Server connection failed.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SQL Server connection failed.", exception);
        }
    }
}
