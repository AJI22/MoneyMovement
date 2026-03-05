using Microsoft.AspNetCore.Mvc;
using MoneyMovement.Contracts.Dtos;
using MoneyMovement.Http;
using MoneyMovement.Observability;

namespace Rails.Nigeria.Controllers;

/// <summary>HTTP API for Nigeria rail (collect NGN). Collect accepts idempotency key for safe retries.</summary>
[ApiController]
[Route("ng")]
public class NgController : ControllerBase
{
    private readonly NgRailService _rail;

    public NgController(NgRailService rail)
    {
        _rail = rail;
    }

    /// <summary>Collect NGN (inbound). Idempotency key prevents duplicate ledger postings on retry.</summary>
    [HttpPost("collect")]
    public async Task<ActionResult<NgCollectResponse>> Collect(
        [FromBody] NgCollectRequest request,
        [FromHeader(Name = IdempotencyHeader.Name)] string? idempotencyKey,
        [FromHeader(Name = CorrelationMiddleware.HeaderName)] string? correlationId,
        CancellationToken cancellationToken)
    {
        var result = await _rail.CollectAsync(request, idempotencyKey, correlationId ?? CorrelationMiddleware.GetOrCreate(HttpContext), cancellationToken);
        return Ok(result);
    }

    /// <summary>Get transaction status by reference id (queries providers until one returns a result).</summary>
    [HttpGet("transactions/{referenceId}")]
    public async Task<ActionResult<RailTransactionStatusResponse>> GetTransaction(string referenceId, CancellationToken cancellationToken)
    {
        var result = await _rail.GetTransactionAsync(referenceId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(new RailTransactionStatusResponse(result.ReferenceId, result.Status, null));
    }
}
