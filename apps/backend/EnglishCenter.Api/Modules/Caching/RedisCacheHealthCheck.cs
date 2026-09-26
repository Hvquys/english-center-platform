using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnglishCenter.Api.Modules.Caching;

internal sealed class RedisCacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var key = $"health:{Guid.NewGuid():N}";
        var value = Guid.NewGuid().ToString("N");

        try
        {
            await cache.SetStringAsync(
                key,
                value,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15) },
                cancellationToken);
            var stored = await cache.GetStringAsync(key, cancellationToken);
            await cache.RemoveAsync(key, cancellationToken);

            return stored == value
                ? HealthCheckResult.Healthy("Redis cache accepted a write and read it back.")
                : HealthCheckResult.Unhealthy("Redis cache returned an unexpected value.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis cache is unavailable.", exception);
        }
    }
}
