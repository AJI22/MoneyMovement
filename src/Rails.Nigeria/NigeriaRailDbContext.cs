using Microsoft.EntityFrameworkCore;
using MoneyMovement.Outbox;

namespace Rails.Nigeria;

/// <summary>EF context for Nigeria rail: provider health (circuit breaker state) and idempotency records for collect.</summary>
public class NigeriaRailDbContext : DbContext
{
    public NigeriaRailDbContext(DbContextOptions<NigeriaRailDbContext> options) : base(options) { }
    public DbSet<ProviderHealthRecord> ProviderHealth => Set<ProviderHealthRecord>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProviderHealthRecord>(e =>
        {
            e.ToTable("ProviderHealth");
            e.HasKey(x => x.ProviderName);
            e.Property(x => x.ProviderName).HasMaxLength(64);
        });
        modelBuilder.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("IdempotencyRecords");
            e.HasKey(x => x.IdempotencyKey);
            e.Property(x => x.IdempotencyKey).HasMaxLength(256);
            e.Property(x => x.ResponsePayload).IsRequired();
        });
        modelBuilder.Entity<OutboxMessage>(OutboxEntityConfiguration.ConfigureOutboxMessage);
    }
}

public class ProviderHealthRecord
{
    public string ProviderName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; } = true;
    public int ConsecutiveFailures { get; set; }
    public DateTimeOffset? LastFailureAt { get; set; }
}

/// <summary>Idempotency record keyed by idempotency key; stores cached response for collect retries.</summary>
public class IdempotencyRecord
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ResponsePayload { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}
