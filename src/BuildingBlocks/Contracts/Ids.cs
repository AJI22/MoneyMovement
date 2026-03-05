namespace MoneyMovement.Contracts;

public readonly record struct TransferId(Guid Value)
{
    public static TransferId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct LegId(Guid Value)
{
    public static LegId New() => new(Guid.NewGuid());
}

public readonly record struct QuoteId(Guid Value)
{
    public static QuoteId New() => new(Guid.NewGuid());
}

public readonly record struct ExecutionId(Guid Value)
{
    public static ExecutionId New() => new(Guid.NewGuid());
}

public readonly record struct PostingId(Guid Value)
{
    public static PostingId New() => new(Guid.NewGuid());
}

public readonly record struct ReservationId(Guid Value)
{
    public static ReservationId New() => new(Guid.NewGuid());
}
