using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyMovement.Outbox;

public static class OutboxEntityConfiguration
{
    public static void ConfigureOutboxMessage(this EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages");
        b.HasKey(x => x.Id);
        b.Property(x => x.EventType).HasMaxLength(256).IsRequired();
        b.Property(x => x.Payload).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.HasIndex(x => x.PublishedAt);
    }
}
