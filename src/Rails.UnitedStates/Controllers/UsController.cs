using Microsoft.AspNetCore.Mvc;
using MoneyMovement.Contracts.Dtos;
using MoneyMovement.Http;
using MoneyMovement.Observability;

namespace Rails.UnitedStates.Controllers;

/// <summary>HTTP API for US rail (USD payout). Payout accepts idempotency key for safe retries.</summary>
[ApiController]
[Route("us")]
public class UsController : ControllerBase
{
    private readonly UsRailService _rail;

    public UsController(UsRailService rail) => _rail = rail;

    /// <summary>Pay out USD. Idempotency key prevents duplicate ledger postings on retry.</summary>
    [HttpPost("payout")]
    public async Task<ActionResult<UsPayoutResponse>> Payout(
        [FromBody] UsPayoutRequest request,
        [FromHeader(Name = IdempotencyHeader.Name)] string? idempotencyKey,
        [FromHeader(Name = CorrelationMiddleware.HeaderName)] string? correlationId,
        CancellationToken cancellationToken)
    {
        var result = await _rail.PayoutAsync(request, idempotencyKey, correlationId ?? CorrelationMiddleware.GetOrCreate(HttpContext), cancellationToken);
        return Ok(result);
    }

    /// <summary>Get payout status by reference id.</summary>
    [HttpGet("transactions/{referenceId}")]
    public async Task<ActionResult<RailTransactionStatusResponse>> GetTransaction(string referenceId, CancellationToken cancellationToken)
    {
        var result = await _rail.GetTransactionAsync(referenceId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(new RailTransactionStatusResponse(result.ReferenceId, result.Status, result.ExternalReference));
    }
}
