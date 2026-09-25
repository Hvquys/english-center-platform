using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnglishCenter.Api.Modules.Health;

[ApiController]
[Route("api/health")]
public sealed class HealthController(HealthCheckService healthCheckService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);
        var response = new HealthResponse(
            Status: report.Status.ToString(),
            Service: "EnglishCenter.Api",
            TimestampUtc: DateTimeOffset.UtcNow,
            Checks: report.Entries.Select(entry => new HealthCheckItemResponse(
                Name: entry.Key,
                Status: entry.Value.Status.ToString(),
                Description: entry.Value.Description,
                DurationMilliseconds: Math.Round(entry.Value.Duration.TotalMilliseconds, 2)))
                .ToArray());

        return report.Status == HealthStatus.Healthy
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
