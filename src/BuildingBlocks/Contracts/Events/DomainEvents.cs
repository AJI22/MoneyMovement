namespace MoneyMovement.Contracts.Events;

public record TransferCreated(
    Guid TransferId,
    string UserId,
    string RecipientId,
    string SourceCurrency,
    decimal SourceAmount,
    string DestinationCurrency,
    string CorrelationId);

public record SourceFundsCollectionRequested(
    Guid TransferId,
    string Currency,
    decimal Amount,
    string CorrelationId);

public record SourceFundsConfirmed(
    Guid TransferId,
    string ReferenceId,
    string CorrelationId);

public record FxQuoteCreated(
    Guid TransferId,
    Guid QuoteId,
    decimal Rate,
    DateTimeOffset ExpiresAt,
    string CorrelationId);

public record FxQuoteAccepted(
    Guid TransferId,
    Guid QuoteId,
    string CorrelationId);

public record FxExecuted(
    Guid TransferId,
    Guid ExecutionId,
    decimal FilledAmount,
    decimal Rate,
    string CorrelationId);

public record PayoutRequested(
    Guid TransferId,
    string Currency,
    decimal Amount,
    string CorrelationId);

public record PayoutSent(
    Guid TransferId,
    string ReferenceId,
    string CorrelationId);

public record TransferCompleted(
    Guid TransferId,
    string CorrelationId);

public record TransferFailed(
    Guid TransferId,
    string Reason,
    string CorrelationId);

public record LiquidityLowDetected(
    string Account,
    string Currency,
    decimal CurrentBalance,
    decimal Threshold,
    string CorrelationId);
