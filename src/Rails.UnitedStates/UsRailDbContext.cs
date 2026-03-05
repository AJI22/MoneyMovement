using Microsoft.EntityFrameworkCore;
using MoneyMovement.Outbox;

namespace Rails.UnitedStates;

public class UsRailDbContext : DbContext
{
    public UsRailDbContext(DbContextOptions<UsRailDbContext> options) : base(options) { }
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

/// <summary>Circuit breaker state per US payout provider. Unhealthy providers are tried last.</summary>
public class ProviderHealthRecord
{
    public string ProviderName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; } = true;
    public int ConsecutiveFailures { get; set; }
}

/// <summary>Idempotency record for payout; stores cached response for retries.</summary>
public class IdempotencyRecord
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ResponsePayload { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}
