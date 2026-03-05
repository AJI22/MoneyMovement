using Microsoft.EntityFrameworkCore;
using MoneyMovement.Outbox;

namespace Ledger.Service;

/// <summary>
/// EF Core context for the Ledger Service. Holds mappings from logical account names to TigerBeetle account ids,
/// idempotency records (to prevent duplicate postings/reserves on retry), and reservations (locked funds).
/// The ledger is the single source of truth; this DB supports idempotency and reservation tracking only.
/// </summary>
public class LedgerDbContext : DbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }

    public DbSet<AccountMapping> AccountMappings => Set<AccountMapping>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<ReservationRecord> Reservations => Set<ReservationRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountMapping>(e =>
        {
            e.ToTable("AccountMappings");
            e.HasKey(x => x.InternalName);
            e.Property(x => x.InternalName).HasMaxLength(64);
            e.Property(x => x.TigerBeetleAccountId);
        });
        modelBuilder.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("IdempotencyRecords");
            e.HasKey(x => new { x.TransferId, x.OperationType, x.IdempotencyKey });
            e.Property(x => x.IdempotencyKey).HasMaxLength(256);
            e.Property(x => x.OperationType).HasMaxLength(64);
        });
        modelBuilder.Entity<ReservationRecord>(e =>
        {
            e.ToTable("Reservations");
            e.HasKey(x => x.ReservationId);
            e.Property(x => x.FromAccount).HasMaxLength(64);
            e.Property(x => x.ToAccount).HasMaxLength(64);
            e.Property(x => x.Currency).HasMaxLength(8);
        });
        modelBuilder.Entity<OutboxMessage>(OutboxEntityConfiguration.ConfigureOutboxMessage);
    }
}

/// <summary>Maps logical account name (e.g. USD_BANK) to TigerBeetle account id. Used when posting or querying balance.</summary>
public class AccountMapping
{
    public string InternalName { get; set; } = string.Empty;
    public ulong TigerBeetleAccountId { get; set; }
}

/// <summary>Stores processed idempotency keys per transfer and operation type. Prevents duplicate money movement when callers retry.</summary>
public class IdempotencyRecord
{
    public Guid TransferId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}

/// <summary>Tracks active reservations (funds locked from one account to another). Release removes the record; TigerBeetle holds the actual balance state.</summary>
public class ReservationRecord
{
    public Guid ReservationId { get; set; }
    public Guid TransferId { get; set; }
    public string FromAccount { get; set; } = string.Empty;
    public string ToAccount { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
