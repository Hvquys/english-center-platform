using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get()
    {
        var response = new HealthResponse(
            Status: "Healthy",
            Service: "EnglishCenter.Api",
            TimestampUtc: DateTimeOffset.UtcNow);

        return Ok(response);
    }
}

public sealed record HealthResponse(
    string Status,
    string Service,
    DateTimeOffset TimestampUtc);