namespace EnglishCenter.Api.Modules.Caching;

public sealed class RedisCacheOptions
{
    public const string SectionName = "Cache:Redis";

    public bool Enabled { get; init; }
    public string? ConnectionString { get; init; }
    public string InstanceName { get; init; } = "english-center:";
    public int DefaultTtlSeconds { get; init; } = 300;
    public int CourseListTtlSeconds { get; init; } = 60;
}
