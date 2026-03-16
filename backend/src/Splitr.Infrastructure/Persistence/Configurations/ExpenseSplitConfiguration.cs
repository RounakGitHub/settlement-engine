using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Splitr.Domain.Entities;

namespace Splitr.Infrastructure.Persistence.Configurations;

public class ExpenseSplitConfiguration : IEntityTypeConfiguration<ExpenseSplit>
{
    public void Configure(EntityTypeBuilder<ExpenseSplit> builder)
    {
        builder.ToTable("expense_splits");

        builder.HasKey(es => es.Id);

        builder.Property(es => es.AmountPaise)
            .HasColumnType("bigint")
            .IsRequired();

        builder.HasOne<Expense>()
            .WithMany(e => e.Splits)
            .HasForeignKey(es => es.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(es => es.UserId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}
