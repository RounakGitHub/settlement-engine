using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Splitr.Domain.Entities;

namespace Splitr.Infrastructure.Persistence.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(g => g.Currency)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("INR");

        builder.Property(g => g.InviteCode)
            .HasMaxLength(8);

        builder.HasIndex(g => g.InviteCode)
            .IsUnique()
            .HasFilter("invite_code IS NOT NULL");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Supports lazy cleanup queries: WHERE is_archived AND delete_after <= now
        builder.HasIndex(g => g.DeleteAfter)
            .HasFilter("is_archived = true AND delete_after IS NOT NULL");

    }
}
