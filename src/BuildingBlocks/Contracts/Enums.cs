namespace MoneyMovement.Contracts;

public enum TransferStatus
{
    Created,
    AwaitingSourceFunds,
    SourceConfirmed,
    FxQuoted,
    FxExecuted,
    PayoutQueued,
    PayoutSent,
    Completed,
    Failed,
    ManualReview
}

public enum TransferLegType
{
    SourceCollection,
    FxConversion,
    DestinationPayout
}

public enum TransferLegStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed
}

public enum SettlementStatus
{
    Pending,
    Settled,
    Reversed
}

public enum LedgerOperationType
{
    NGN_RECEIVED,
    FX_EXECUTED,
    USD_PAYOUT_SENT,
    REVERSAL,
    RESERVE,
    RELEASE_RESERVE
}
