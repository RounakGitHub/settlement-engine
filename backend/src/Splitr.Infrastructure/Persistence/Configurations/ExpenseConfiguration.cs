using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Splitr.Domain.Entities;

namespace Splitr.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.AmountPaise)
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(e => e.Description)
            .IsRequired();

        builder.Property(e => e.SplitType)
            .IsRequired()
            .HasConversion<string>();

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(e => e.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.PaidBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.GroupId);

        builder.HasQueryFilter(e => e.DeletedAt == null);

    }
}
