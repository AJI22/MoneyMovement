using Microsoft.EntityFrameworkCore;
using MoneyMovement.Outbox;

namespace Transfer.Orchestrator;

/// <summary>EF context for orchestrator: transfer records (status, correlation) and transfer legs. No ledger data—ledger is the single source of truth for money.</summary>
public class OrchestratorDbContext : DbContext
{
    public OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options) : base(options) { }
    public DbSet<TransferRecord> Transfers => Set<TransferRecord>();
    public DbSet<TransferLegRecord> TransferLegs => Set<TransferLegRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransferRecord>(e =>
        {
            e.ToTable("Transfers");
            e.HasKey(x => x.TransferId);
        });
        modelBuilder.Entity<TransferLegRecord>(e =>
        {
            e.ToTable("TransferLegs");
            e.HasKey(x => x.LegId);
        });
        modelBuilder.Entity<OutboxMessage>(OutboxEntityConfiguration.ConfigureOutboxMessage);
    }
}

/// <summary>Orchestrator view of a transfer: id, parties, amounts, status. Status is updated as the workflow progresses.</summary>
public class TransferRecord
{
    public Guid TransferId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public string SourceCurrency { get; set; } = string.Empty;
    public decimal SourceAmount { get; set; }
    public string DestinationCurrency { get; set; } = string.Empty;
    public string Status { get; set; } = "Created";
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Represents a leg of a transfer (e.g. collect, FX, payout) for tracking.</summary>
public class TransferLegRecord
{
    public Guid LegId { get; set; }
    public Guid TransferId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? ExternalReference { get; set; }
}
