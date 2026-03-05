using Microsoft.AspNetCore.Mvc;
using MoneyMovement.Contracts.Dtos;
using MoneyMovement.Http;
using MoneyMovement.Observability;

namespace FX.Service.Controllers;

/// <summary>
/// HTTP API for the FX Service. Exposes quote, accept, and execute. Execute accepts an idempotency key
/// so that the orchestrator (or Temporal) can safely retry without duplicate ledger postings.
/// </summary>
[ApiController]
[Route("fx")]
public class FxController : ControllerBase
{
    private readonly FxServiceApp _fx;

    public FxController(FxServiceApp fx) => _fx = fx;

    /// <summary>Request a quote for a given source/dest currency and amount. Tries venues in order until one succeeds.</summary>
    [HttpPost("quote")]
    public async Task<ActionResult<FxQuoteResponse>> Quote([FromBody] FxQuoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _fx.QuoteAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("accept")]
    public async Task<ActionResult<FxAcceptResponse>> Accept([FromBody] FxAcceptRequest request, CancellationToken cancellationToken)
    {
        var result = await _fx.AcceptAsync(request, cancellationToken);
        if (result == null) return BadRequest("Quote expired or not found");
        return Ok(result);
    }

    /// <summary>Execute FX conversion and post to ledger. Idempotency key required for safe retries.</summary>
    [HttpPost("execute")]
    public async Task<ActionResult<FxExecuteResponse>> Execute(
        [FromBody] FxExecuteRequest request,
        [FromHeader(Name = IdempotencyHeader.Name)] string? idempotencyKey,
        [FromHeader(Name = CorrelationMiddleware.HeaderName)] string? correlationId,
        CancellationToken cancellationToken)
    {
        var result = await _fx.ExecuteAsync(request, idempotencyKey, correlationId ?? CorrelationMiddleware.GetOrCreate(HttpContext), cancellationToken);
        return Ok(result);
    }
}
