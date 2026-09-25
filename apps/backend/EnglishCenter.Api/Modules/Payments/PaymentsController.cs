using EnglishCenter.Api.Api.Concurrency;
using EnglishCenter.Api.Api.Paging;
using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Modules.Payments;

[ApiController, Route("api/payments")]
public sealed class PaymentsController(IPaymentService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<PagedResponse<PaymentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PaymentResponse>>> GetAll([FromQuery] PaymentListQuery query, CancellationToken ct) => Ok(await service.GetAllAsync(query, ct));

    [HttpGet("{paymentId:long:min(1)}")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetById(long paymentId, CancellationToken ct)
    {
        var item = await service.GetByIdAsync(paymentId, ct); Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion); return Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> Create([FromBody] CreatePaymentRequest request, CancellationToken ct)
    {
        var item = await service.CreateAsync(request, ct); Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion);
        return CreatedAtAction(nameof(GetById), new { paymentId = item.PaymentId }, item);
    }

    [HttpPut("{paymentId:long:min(1)}")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> Update(long paymentId, [FromBody] UpdatePaymentRequest request, CancellationToken ct)
    {
        var item = await service.UpdateAsync(paymentId, request, ct); Response.Headers.ETag = RowVersionCodec.ToETag(item.RowVersion); return Ok(item);
    }

    [HttpDelete("{paymentId:long:min(1)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long paymentId, [FromHeader(Name = "If-Match")] string? rowVersion, CancellationToken ct)
    {
        await service.DeleteAsync(paymentId, rowVersion, ct); return NoContent();
    }
}
