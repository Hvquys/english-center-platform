using Microsoft.Extensions.Options;

namespace EnglishCenter.Api.Modules.Caching;

public static class CachingModule
{
    public static IServiceCollection AddCacheModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(RedisCacheOptions.SectionName);
        var options = section.Get<RedisCacheOptions>() ?? new RedisCacheOptions();
        services.Configure<RedisCacheOptions>(section);

        ValidateOptions(options);
        if (options.Enabled)
        {
            services.AddStackExchangeRedisCache(redis =>
            {
                redis.Configuration = options.ConnectionString;
                redis.InstanceName = options.InstanceName;
            });
            services.AddHealthChecks().AddCheck<RedisCacheHealthCheck>("redis");
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<IApplicationCache, ApplicationCache>();
        services.AddSingleton<ICourseCache, CourseCache>();
        return services;
    }

    private static void ValidateOptions(RedisCacheOptions options)
    {
        var errors = new List<string>();
        if (options.Enabled && string.IsNullOrWhiteSpace(options.ConnectionString))
            errors.Add("Redis connection string is required when caching is enabled.");
        if (string.IsNullOrWhiteSpace(options.InstanceName))
            errors.Add("Redis instance name is required.");
        if (options.DefaultTtlSeconds is < 1 or > 86_400)
            errors.Add("Default cache TTL must be between 1 and 86400 seconds.");
        if (options.CourseListTtlSeconds is < 1 or > 3_600)
            errors.Add("Course list cache TTL must be between 1 and 3600 seconds.");

        if (errors.Count > 0)
            throw new OptionsValidationException(RedisCacheOptions.SectionName, typeof(RedisCacheOptions), errors);
    }
}
