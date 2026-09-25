using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.Paging;
using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Modules.Classes;

[ApiController, Route("api/classes")]
public sealed class ClassesController(IClassService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<PagedResponse<ClassResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ClassResponse>>> GetAll([FromQuery] ClassListQuery query, CancellationToken ct) => Ok(await service.GetAllAsync(query, ct));

    [HttpGet("{classId:long:min(1)}")]
    [ProducesResponseType<ClassResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassResponse>> GetById(long classId, CancellationToken ct)
    {
        var item = await service.GetByIdAsync(classId, ct); Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion); return Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<ClassResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClassResponse>> Create([FromBody] CreateClassRequest request, CancellationToken ct)
    {
        var item = await service.CreateAsync(request, ct); Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion);
        return CreatedAtAction(nameof(GetById), new { classId = item.ClassId }, item);
    }

    [HttpPut("{classId:long:min(1)}")]
    [ProducesResponseType<ClassResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClassResponse>> Update(long classId, [FromBody] UpdateClassRequest request, CancellationToken ct)
    {
        var item = await service.UpdateAsync(classId, request, ct); Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion); return Ok(item);
    }

    [HttpDelete("{classId:long:min(1)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long classId, [FromHeader(Name = "If-Match")] string? rowVersion, CancellationToken ct)
    {
        await service.DeleteAsync(classId, rowVersion, ct); return NoContent();
    }
}
