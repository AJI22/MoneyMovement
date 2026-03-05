using Microsoft.AspNetCore.Mvc;
using MoneyMovement.Contracts.Dtos;
using MoneyMovement.Http;
using MoneyMovement.Observability;

namespace Ledger.Service.Controllers;

/// <summary>
/// HTTP API for the Ledger Service. Exposes post, reserve, release, and balance operations.
/// All posting and reserve endpoints accept an optional idempotency key header so that callers (or Temporal retries)
/// can safely retry without creating duplicate ledger entries. The ledger is the single source of truth for balances.
/// </summary>
[ApiController]
[Route("ledger")]
public class LedgerController : ControllerBase
{
    private readonly ILedgerService _ledger;

    public LedgerController(ILedgerService ledger)
    {
        _ledger = ledger;
    }

    /// <summary>One-time setup: creates TigerBeetle accounts and DB mappings. Safe to call multiple times.</summary>
    [HttpPost("bootstrap")]
    public async Task<IActionResult> Bootstrap(CancellationToken cancellationToken)
    {
        await _ledger.BootstrapAsync(cancellationToken);
        return Ok(new { status = "ok" });
    }

    /// <summary>Posts a double-entry transfer. Idempotency key from header prevents duplicate postings on retry.</summary>
    [HttpPost("posting")]
    public async Task<ActionResult<LedgerPostingResponse>> Posting(
        [FromBody] LedgerPostingRequest request,
        [FromHeader(Name = IdempotencyHeader.Name)] string? idempotencyKey,
        [FromHeader(Name = CorrelationMiddleware.HeaderName)] string? correlationId,
        CancellationToken cancellationToken)
    {
        var correlation = correlationId ?? CorrelationMiddleware.GetOrCreate(HttpContext);
        var result = await _ledger.PostAsync(request, idempotencyKey, correlation, cancellationToken);
        return Ok(result);
    }

    /// <summary>Reserves funds between accounts. Balance checks enforced by TigerBeetle; idempotency key required for retries.</summary>
    [HttpPost("reserve")]
    public async Task<ActionResult<LedgerReserveResponse>> Reserve(
        [FromBody] LedgerReserveRequest request,
        [FromHeader(Name = IdempotencyHeader.Name)] string? idempotencyKey,
        [FromHeader(Name = CorrelationMiddleware.HeaderName)] string? correlationId,
        CancellationToken cancellationToken)
    {
        var correlation = correlationId ?? CorrelationMiddleware.GetOrCreate(HttpContext);
        var result = await _ledger.ReserveAsync(request, idempotencyKey, correlation, cancellationToken);
        return Ok(result);
    }

    /// <summary>Releases a reservation (e.g. cancelled payout). Removes reservation record only.</summary>
    [HttpPost("release")]
    public async Task<ActionResult<LedgerReleaseResponse>> Release(
        [FromBody] LedgerReleaseRequest request,
        [FromHeader(Name = CorrelationMiddleware.HeaderName)] string? correlationId,
        CancellationToken cancellationToken)
    {
        var result = await _ledger.ReleaseAsync(request, correlationId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns current balance (posted + pending) for a logical account. Used for balance checks before reserve.</summary>
    [HttpGet("balances/{account}")]
    public async Task<ActionResult<LedgerBalanceResponse>> GetBalance(string account, CancellationToken cancellationToken)
    {
        var result = await _ledger.GetBalanceAsync(account, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }
}
