using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.Paging;
using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Modules.Teachers;

[ApiController]
[Route("api/teachers")]
public sealed class TeachersController(ITeacherService teacherService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<TeacherResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<TeacherResponse>>> GetAll(
        [FromQuery] TeacherListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await teacherService.GetAllAsync(query, cancellationToken));
    }

    [HttpGet("{teacherId:long:min(1)}")]
    [ProducesResponseType<TeacherResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeacherResponse>> GetById(
        long teacherId,
        CancellationToken cancellationToken)
    {
        var teacher = await teacherService.GetByIdAsync(teacherId, cancellationToken);
        Response.Headers.ETag = RowVersionCodec.ToETag(teacher.RowVersion);
        return Ok(teacher);
    }

    [HttpPost]
    [ProducesResponseType<TeacherResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeacherResponse>> Create(
        [FromBody] CreateTeacherRequest request,
        CancellationToken cancellationToken)
    {
        var teacher = await teacherService.CreateAsync(request, cancellationToken);
        Response.Headers.ETag = RowVersionCodec.ToETag(teacher.RowVersion);

        return CreatedAtAction(
            nameof(GetById),
            new { teacherId = teacher.TeacherId },
            teacher);
    }

    [HttpPut("{teacherId:long:min(1)}")]
    [ProducesResponseType<TeacherResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeacherResponse>> Update(
        long teacherId,
        [FromBody] UpdateTeacherRequest request,
        CancellationToken cancellationToken)
    {
        var teacher = await teacherService.UpdateAsync(teacherId, request, cancellationToken);
        Response.Headers.ETag = RowVersionCodec.ToETag(teacher.RowVersion);
        return Ok(teacher);
    }

    [HttpDelete("{teacherId:long:min(1)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        long teacherId,
        [FromHeader(Name = "If-Match")] string? rowVersion,
        CancellationToken cancellationToken)
    {
        await teacherService.DeleteAsync(teacherId, rowVersion, cancellationToken);
        return NoContent();
    }
}
