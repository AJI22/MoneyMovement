namespace MoneyMovement.Contracts.Dtos;

public record NgCollectRequest(
    Guid TransferId,
    decimal Amount,
    string Currency,
    string BankAccountRef);

public record NgCollectResponse(
    string ReferenceId,
    string Status);

public record NgPayoutRequest(
    Guid TransferId,
    decimal Amount,
    string Currency,
    string BankAccountRef);

public record NgPayoutResponse(
    string ReferenceId,
    string Status);

public record UsPayoutRequest(
    Guid TransferId,
    decimal Amount,
    string Currency,
    string BankAccountRef);

public record UsPayoutResponse(
    string ReferenceId,
    string Status);

public record RailTransactionStatusResponse(
    string ReferenceId,
    string Status,
    string? ExternalReference);
