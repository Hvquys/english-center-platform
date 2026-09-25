namespace EnglishCenter.Api.Modules.Health;

public sealed record HealthCheckItemResponse(
    string Name,
    string Status,
    string? Description,
    double DurationMilliseconds);

public sealed record HealthResponse(
    string Status,
    string Service,
    DateTimeOffset TimestampUtc,
    IReadOnlyCollection<HealthCheckItemResponse> Checks);
