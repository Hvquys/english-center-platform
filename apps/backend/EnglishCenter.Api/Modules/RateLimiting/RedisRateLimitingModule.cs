using EnglishCenter.Api.Modules.Caching;
using Microsoft.Extensions.Options;

namespace EnglishCenter.Api.Modules.RateLimiting;

public static class RedisRateLimitingModule
{
    public static IServiceCollection AddRedisRateLimitingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(RedisRateLimitingOptions.SectionName);
        var options = section.Get<RedisRateLimitingOptions>() ?? new RedisRateLimitingOptions();
        var cacheOptions = configuration
            .GetSection(RedisCacheOptions.SectionName)
            .Get<RedisCacheOptions>() ?? new RedisCacheOptions();
        services.Configure<RedisRateLimitingOptions>(section);

        ValidateOptions(options, cacheOptions);
        if (options.Enabled)
        {
            services.AddSingleton<IRedisRateLimiter, RedisRateLimiter>();
        }
        else
        {
            services.AddSingleton<IRedisRateLimiter, DisabledRedisRateLimiter>();
        }

        return services;
    }

    public static IApplicationBuilder UseRedisRateLimiting(this IApplicationBuilder app) =>
        app.UseMiddleware<RedisRateLimitingMiddleware>();

    private static void ValidateOptions(
        RedisRateLimitingOptions options,
        RedisCacheOptions cacheOptions)
    {
        var errors = new List<string>();
        if (options.Enabled && (!cacheOptions.Enabled || string.IsNullOrWhiteSpace(cacheOptions.ConnectionString)))
            errors.Add("Redis cache must be enabled and configured when Redis rate limiting is enabled.");

        foreach (var (name, policy) in new[]
                 {
                     (nameof(options.Login), options.Login),
                     (nameof(options.Refresh), options.Refresh),
                     (nameof(options.Notifications), options.Notifications)
                 })
        {
            if (policy.PermitLimit is < 1 or > 10_000)
                errors.Add($"{name} permit limit must be between 1 and 10000.");
            if (policy.WindowSeconds is < 1 or > 86_400)
                errors.Add($"{name} window must be between 1 and 86400 seconds.");
        }

        if (errors.Count > 0)
        {
            throw new OptionsValidationException(
                RedisRateLimitingOptions.SectionName,
                typeof(RedisRateLimitingOptions),
                errors);
        }
    }
}
