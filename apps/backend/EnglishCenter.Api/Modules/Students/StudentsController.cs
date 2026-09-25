using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Modules.Students;

[ApiController]
[Route("api/students")]
public sealed class StudentsController(IStudentService studentService) : ControllerBase
{
    [HttpGet("{studentId:long:min(1)}")]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentResponse>> GetById(
        long studentId,
        CancellationToken cancellationToken)
    {
        return Ok(await studentService.GetByIdAsync(studentId, cancellationToken));
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

        return CreatedAtAction(
            nameof(GetById),
            new { studentId = student.StudentId },
            student);
    }
}
