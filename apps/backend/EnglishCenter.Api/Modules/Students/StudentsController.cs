using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.Paging;
using EnglishCenter.Api.Modules.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Modules.Students;

[ApiController]
[Route("api/students")]
[Authorize(Policy = AuthorizationPolicies.StaffOperations)]
public sealed class StudentsController(IStudentService studentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<StudentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<StudentResponse>>> GetAll(
        [FromQuery] StudentListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await studentService.GetAllAsync(query, cancellationToken));
    }

    [HttpGet("{studentId:long:min(1)}")]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentResponse>> GetById(
        long studentId,
        CancellationToken cancellationToken)
    {
        var student = await studentService.GetByIdAsync(studentId, cancellationToken);
        Response.Headers.ETag = RowVersionCodec.ToETag(student.RowVersion);
        return Ok(student);
    }

    [HttpPost]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentResponse>> Create(
        [FromBody] CreateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var student = await studentService.CreateAsync(request, cancellationToken);
        Response.Headers.ETag = RowVersionCodec.ToETag(student.RowVersion);

        return CreatedAtAction(
            nameof(GetById),
            new { studentId = student.StudentId },
            student);
    }

    [HttpPut("{studentId:long:min(1)}")]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentResponse>> Update(
        long studentId,
        [FromBody] UpdateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var student = await studentService.UpdateAsync(studentId, request, cancellationToken);
        Response.Headers.ETag = RowVersionCodec.ToETag(student.RowVersion);
        return Ok(student);
    }

    [HttpDelete("{studentId:long:min(1)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        long studentId,
        [FromHeader(Name = "If-Match")] string? rowVersion,
        CancellationToken cancellationToken)
    {
        await studentService.DeleteAsync(studentId, rowVersion, cancellationToken);
        return NoContent();
    }
}
