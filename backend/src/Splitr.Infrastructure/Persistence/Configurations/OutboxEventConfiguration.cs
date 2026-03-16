using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Splitr.Domain.Entities;

namespace Splitr.Infrastructure.Persistence.Configurations;

public class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("outbox_events");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.EventType)
            .IsRequired();

        builder.Property(o => o.Payload)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.HasIndex(o => o.PublishedAt);

    }
}
