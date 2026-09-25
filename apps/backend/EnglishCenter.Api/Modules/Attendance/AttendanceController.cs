using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.Paging;
using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Modules.Attendance;

[ApiController, Route("api/attendance")]
public sealed class AttendanceController(IAttendanceService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<PagedResponse<AttendanceResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AttendanceResponse>>> GetAll([FromQuery] AttendanceListQuery query, CancellationToken ct) => Ok(await service.GetAllAsync(query, ct));

    [HttpGet("{attendanceId:long:min(1)}")]
    [ProducesResponseType<AttendanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceResponse>> GetById(long attendanceId, CancellationToken ct)
    {
        var item = await service.GetByIdAsync(attendanceId, ct); Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion); return Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<AttendanceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttendanceResponse>> Create([FromBody] CreateAttendanceRequest request, CancellationToken ct)
    {
        var item = await service.CreateAsync(request, ct); Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion);
        return CreatedAtAction(nameof(GetById), new { attendanceId = item.AttendanceId }, item);
    }

    [HttpPut("{attendanceId:long:min(1)}")]
    [ProducesResponseType<AttendanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttendanceResponse>> Update(long attendanceId, [FromBody] UpdateAttendanceRequest request, CancellationToken ct)
    {
        var item = await service.UpdateAsync(attendanceId, request, ct); Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion); return Ok(item);
    }

    [HttpDelete("{attendanceId:long:min(1)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long attendanceId, [FromHeader(Name = "If-Match")] string? rowVersion, CancellationToken ct)
    {
        await service.DeleteAsync(attendanceId, rowVersion, ct); return NoContent();
    }
}
