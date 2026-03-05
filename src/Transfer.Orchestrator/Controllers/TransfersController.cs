using Microsoft.AspNetCore.Mvc;
using MoneyMovement.Contracts.Dtos;
using MoneyMovement.Observability;

namespace Transfer.Orchestrator.Controllers;

/// <summary>
/// HTTP API for creating and querying transfers. Create starts the workflow asynchronously; Get returns current status.
/// The orchestrator coordinates rails and FX but does not know internal vendor routing—each service handles that.
/// </summary>
[ApiController]
[Route("[controller]")]
public class TransfersController : ControllerBase
{
    private readonly TransferFlowService _flow;

    public TransfersController(TransferFlowService flow) => _flow = flow;

    /// <summary>Creates a transfer and starts the flow (collect → FX → reserve → payout). Returns Accepted with transfer id.</summary>
    [HttpPost]
    public async Task<ActionResult<CreateTransferResponse>> Create(
        [FromBody] CreateTransferRequest request,
        [FromHeader(Name = CorrelationMiddleware.HeaderName)] string? correlationId,
        CancellationToken cancellationToken)
    {
        var correlation = correlationId ?? CorrelationMiddleware.GetOrCreate(HttpContext);
        var result = await _flow.CreateAndRunAsync(request, correlation, cancellationToken);
        return Accepted(result);
    }

    /// <summary>Returns transfer status by id.</summary>
    [HttpGet("{transferId:guid}")]
    public async Task<ActionResult<TransferDto>> Get(Guid transferId, CancellationToken cancellationToken)
    {
        var result = await _flow.GetAsync(transferId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }
}
