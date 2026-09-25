using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.Paging;
using EnglishCenter.Api.Modules.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Modules.Courses;

[ApiController, Route("api/courses")]
[Authorize]
public sealed class CoursesController(ICourseService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<PagedResponse<CourseResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<CourseResponse>>> GetAll([FromQuery] CourseListQuery query, CancellationToken ct) => Ok(await service.GetAllAsync(query, ct));

    [HttpGet("{courseId:long:min(1)}")]
    [ProducesResponseType<CourseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseResponse>> GetById(long courseId, CancellationToken ct)
    {
        var item = await service.GetByIdAsync(courseId, ct);
        Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion);
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.StaffOperations)]
    [ProducesResponseType<CourseResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CourseResponse>> Create([FromBody] CreateCourseRequest request, CancellationToken ct)
    {
        var item = await service.CreateAsync(request, ct);
        Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion);
        return CreatedAtAction(nameof(GetById), new { courseId = item.CourseId }, item);
    }

    [HttpPut("{courseId:long:min(1)}")]
    [Authorize(Policy = AuthorizationPolicies.StaffOperations)]
    [ProducesResponseType<CourseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CourseResponse>> Update(long courseId, [FromBody] UpdateCourseRequest request, CancellationToken ct)
    {
        var item = await service.UpdateAsync(courseId, request, ct);
        Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion);
        return Ok(item);
    }

    [HttpDelete("{courseId:long:min(1)}")]
    [Authorize(Policy = AuthorizationPolicies.StaffOperations)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long courseId, [FromHeader(Name = "If-Match")] string? rowVersion, CancellationToken ct)
    {
        await service.DeleteAsync(courseId, rowVersion, ct);
        return NoContent();
    }
}
