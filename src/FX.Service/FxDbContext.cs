using Microsoft.EntityFrameworkCore;
using MoneyMovement.Outbox;

namespace FX.Service;

/// <summary>EF Core context for FX Service: quotes, accepted quotes, and idempotency records. Idempotency records ensure Execute is safe to retry.</summary>
public class FxDbContext : DbContext
{
    public FxDbContext(DbContextOptions<FxDbContext> options) : base(options) { }
    public DbSet<QuoteRecord> Quotes => Set<QuoteRecord>();
    public DbSet<AcceptedQuoteRecord> AcceptedQuotes => Set<AcceptedQuoteRecord>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuoteRecord>(e =>
        {
            e.ToTable("Quotes");
            e.HasKey(x => x.QuoteId);
        });
        modelBuilder.Entity<AcceptedQuoteRecord>(e =>
        {
            e.ToTable("AcceptedQuotes");
            e.HasKey(x => x.AcceptedQuoteId);
        });
        modelBuilder.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("IdempotencyRecords");
            e.HasKey(x => new { x.TransferId, x.OperationType, x.IdempotencyKey });
        });
        modelBuilder.Entity<OutboxMessage>(OutboxEntityConfiguration.ConfigureOutboxMessage);
    }
}

/// <summary>Stored quote for a transfer; used to validate accept and execute.</summary>
public class QuoteRecord
{
    public Guid QuoteId { get; set; }
    public Guid TransferId { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public string DestCurrency { get; set; } = string.Empty;
    public decimal SourceAmount { get; set; }
    public decimal Rate { get; set; }
    public decimal FeeAmount { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AcceptedQuoteRecord
{
    public Guid AcceptedQuoteId { get; set; }
    public Guid TransferId { get; set; }
    public Guid QuoteId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>Idempotency key for FX_EXECUTE; prevents duplicate ledger postings on retry.</summary>
public class IdempotencyRecord
{
    public Guid TransferId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}
