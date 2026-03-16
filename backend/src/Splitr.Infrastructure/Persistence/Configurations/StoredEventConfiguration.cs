using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Splitr.Domain.Entities;

namespace Splitr.Infrastructure.Persistence.Configurations;

public class StoredEventConfiguration : IEntityTypeConfiguration<StoredEvent>
{
    public void Configure(EntityTypeBuilder<StoredEvent> builder)
    {
        builder.ToTable("events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.AggregateType)
            .IsRequired();

        builder.Property(e => e.EventType)
            .IsRequired();

        builder.Property(e => e.Payload)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.HasIndex(e => new { e.AggregateId, e.Version })
            .IsUnique();
    }
}
