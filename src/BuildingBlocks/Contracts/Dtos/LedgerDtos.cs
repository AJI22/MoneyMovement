namespace MoneyMovement.Contracts.Dtos;

public record LedgerEntryDto(
    string Account,
    decimal Debit,
    decimal Credit,
    string Currency);

public record LedgerPostingRequest(
    Guid TransferId,
    string OperationType,
    IReadOnlyList<LedgerEntryDto> Entries);

public record LedgerPostingResponse(
    Guid PostingId,
    bool Success,
    string? ErrorCode = null);

public record LedgerReserveRequest(
    Guid TransferId,
    string FromAccount,
    string ToAccount,
    decimal Amount,
    string Currency);

public record LedgerReserveResponse(
    Guid ReservationId,
    bool Success,
    string? ErrorCode = null);

public record LedgerReleaseRequest(
    Guid ReservationId);

public record LedgerReleaseResponse(
    bool Success,
    string? ErrorCode = null);

public record LedgerBalanceResponse(
    string Account,
    string Currency,
    decimal Balance);
