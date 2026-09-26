namespace EnglishCenter.Api.Modules.RateLimiting;

public sealed class RedisRateLimitingOptions
{
    public const string SectionName = "RateLimiting:Redis";

    public bool Enabled { get; init; }
    public RateLimitPolicyOptions Login { get; init; } = new(20, 60);
    public RateLimitPolicyOptions Refresh { get; init; } = new(10, 60);
    public RateLimitPolicyOptions Notifications { get; init; } = new(30, 60);

    public RateLimitPolicyOptions For(RedisRateLimitPolicy policy) => policy switch
    {
        RedisRateLimitPolicy.Login => Login,
        RedisRateLimitPolicy.Refresh => Refresh,
        RedisRateLimitPolicy.Notifications => Notifications,
        _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported rate-limit policy.")
    };
}

public sealed record RateLimitPolicyOptions(int PermitLimit = 20, int WindowSeconds = 60);
