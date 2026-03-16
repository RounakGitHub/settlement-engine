using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Splitr.Domain.Entities;

namespace Splitr.Infrastructure.Persistence.Configurations;

public class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.ToTable("settlements");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.AmountPaise)
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.PayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.PayeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.GroupId, s.Status });

        // Supports lazy expiry queries: WHERE group_id = X AND status = 'Pending' AND expires_at <= now
        builder.HasIndex(s => new { s.GroupId, s.ExpiresAt })
            .HasFilter("status = 'Pending'");

    }
}
