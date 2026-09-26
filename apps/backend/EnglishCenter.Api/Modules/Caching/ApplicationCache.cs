using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace EnglishCenter.Api.Modules.Caching;

public readonly record struct CacheReadResult<T>(bool Found, T? Value);

public interface IApplicationCache
{
    Task<CacheReadResult<T>> GetAsync<T>(string key, CancellationToken cancellationToken);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken);
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

internal sealed class ApplicationCache(
    IDistributedCache cache,
    ILogger<ApplicationCache> logger) : IApplicationCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CacheReadResult<T>> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            var json = await cache.GetStringAsync(key, cancellationToken);
            return json is null
                ? new CacheReadResult<T>(false, default)
                : new CacheReadResult<T>(true, JsonSerializer.Deserialize<T>(json, JsonOptions));
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Cache read failed for {CacheKey}; using the source of truth.", key);
            return new CacheReadResult<T>(false, default);
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetStringAsync(
                key,
                JsonSerializer.Serialize(value, JsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Cache write failed for {CacheKey}; the request will continue.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Cache invalidation failed for {CacheKey}; the request will continue.", key);
        }
    }
}
