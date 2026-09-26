using EnglishCenter.Api.Modules.Caching;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EnglishCenter.Api.Modules.RateLimiting;

public sealed record RateLimitDecision(
    bool IsAllowed,
    int Limit,
    int Remaining,
    TimeSpan RetryAfter);

public interface IRedisRateLimiter
{
    Task<RateLimitDecision> AcquireAsync(
        string policyName,
        string partitionHash,
        RateLimitPolicyOptions policy,
        CancellationToken cancellationToken);
}

internal sealed class DisabledRedisRateLimiter : IRedisRateLimiter
{
    public Task<RateLimitDecision> AcquireAsync(
        string policyName,
        string partitionHash,
        RateLimitPolicyOptions policy,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RateLimitDecision(true, policy.PermitLimit, policy.PermitLimit, TimeSpan.Zero));
}

internal sealed class RedisRateLimiter(
    IOptions<RedisCacheOptions> cacheOptions,
    ILogger<RedisRateLimiter> logger) : IRedisRateLimiter, IAsyncDisposable
{
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        local ttl = redis.call('PTTL', KEYS[1])
        return {count, ttl}
        """;

    private readonly RedisCacheOptions _cacheOptions = cacheOptions.Value;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private IConnectionMultiplexer? _connection;

    public async Task<RateLimitDecision> AcquireAsync(
        string policyName,
        string partitionHash,
        RateLimitPolicyOptions policy,
        CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var database = connection.GetDatabase();
        var prefix = _cacheOptions.InstanceName.EndsWith(':')
            ? _cacheOptions.InstanceName
            : $"{_cacheOptions.InstanceName}:";
        var key = (RedisKey)$"{prefix}rate-limit:{policyName}:{partitionHash}";
        var windowMilliseconds = checked(policy.WindowSeconds * 1000L);

        var result = await database.ScriptEvaluateAsync(
                IncrementScript,
                [key],
                [windowMilliseconds])
            .WaitAsync(cancellationToken);
        var values = (RedisResult[])result!;
        var count = (long)values[0];
        var ttlMilliseconds = Math.Max(1, (long)values[1]);
        var remaining = Math.Max(0, policy.PermitLimit - checked((int)Math.Min(count, int.MaxValue)));

        return new RateLimitDecision(
            IsAllowed: count <= policy.PermitLimit,
            Limit: policy.PermitLimit,
            Remaining: remaining,
            RetryAfter: TimeSpan.FromMilliseconds(ttlMilliseconds));
    }

    private async Task<IConnectionMultiplexer> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is null)
            {
                _connection = await ConnectionMultiplexer.ConnectAsync(_cacheOptions.ConnectionString!);
                logger.LogInformation("Redis rate-limiter connection initialized.");
            }

            return _connection;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connectionGate.Dispose();
    }
}
