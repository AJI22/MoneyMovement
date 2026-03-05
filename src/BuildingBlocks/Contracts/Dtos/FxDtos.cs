namespace MoneyMovement.Contracts.Dtos;

public record FxQuoteRequest(
    Guid TransferId,
    string SourceCurrency,
    string DestinationCurrency,
    decimal SourceAmount);

public record FxQuoteResponse(
    Guid QuoteId,
    decimal Rate,
    DateTimeOffset ExpiresAt,
    decimal EstimatedDestinationAmount,
    decimal FeeAmount);

public record FxAcceptRequest(
    Guid TransferId,
    Guid QuoteId);

public record FxAcceptResponse(
    Guid AcceptedQuoteId,
    DateTimeOffset ExpiresAt);

public record FxExecuteRequest(
    Guid TransferId,
    Guid AcceptedQuoteId);

public record FxExecuteResponse(
    Guid ExecutionId,
    string Status,
    decimal FilledAmount,
    decimal Rate);
