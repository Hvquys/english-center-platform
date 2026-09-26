namespace EnglishCenter.Api.Modules.RateLimiting;

public enum RedisRateLimitPolicy
{
    Login,
    Refresh,
    Notifications
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RedisRateLimitAttribute(RedisRateLimitPolicy policy) : Attribute
{
    public RedisRateLimitPolicy Policy { get; } = policy;
}
