namespace MoneyMovement.Contracts.Dtos;

public record CreateTransferRequest(
    string UserId,
    string RecipientId,
    string SourceCurrency,
    decimal SourceAmount,
    string DestinationCurrency);

public record CreateTransferResponse(
    Guid TransferId,
    string Status,
    string? CorrelationId);

public record TransferDto(
    Guid TransferId,
    string UserId,
    string RecipientId,
    string SourceCurrency,
    decimal SourceAmount,
    string DestinationCurrency,
    string Status,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
