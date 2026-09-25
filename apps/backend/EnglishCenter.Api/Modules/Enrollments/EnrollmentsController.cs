using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.Paging;
using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Modules.Enrollments;

[ApiController, Route("api/enrollments")]
public sealed class EnrollmentsController(IEnrollmentService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<PagedResponse<EnrollmentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<EnrollmentResponse>>> GetAll([FromQuery] EnrollmentListQuery query, CancellationToken ct) => Ok(await service.GetAllAsync(query, ct));

    [HttpGet("{enrollmentId:long:min(1)}")]
    [ProducesResponseType<EnrollmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrollmentResponse>> GetById(long enrollmentId, CancellationToken ct)
    {
        var item = await service.GetByIdAsync(enrollmentId, ct); Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion); return Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<EnrollmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrollmentResponse>> Create([FromBody] CreateEnrollmentRequest request, CancellationToken ct)
    {
        var item = await service.CreateAsync(request, ct); Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion);
        return CreatedAtAction(nameof(GetById), new { enrollmentId = item.EnrollmentId }, item);
    }

    [HttpPut("{enrollmentId:long:min(1)}")]
    [ProducesResponseType<EnrollmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrollmentResponse>> Update(long enrollmentId, [FromBody] UpdateEnrollmentRequest request, CancellationToken ct)
    {
        var item = await service.UpdateAsync(enrollmentId, request, ct); Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion); return Ok(item);
    }

    [HttpDelete("{enrollmentId:long:min(1)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long enrollmentId, [FromHeader(Name = "If-Match")] string? rowVersion, CancellationToken ct)
    {
        await service.DeleteAsync(enrollmentId, rowVersion, ct); return NoContent();
    }
}
