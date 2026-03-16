using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Splitr.Domain.Entities;

namespace Splitr.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired(false);

        builder.Property(u => u.Name)
            .IsRequired();

        builder.Property(u => u.GoogleId);

        builder.HasIndex(u => u.GoogleId)
            .IsUnique()
            .HasFilter("google_id IS NOT NULL");

        builder.Property(u => u.FailedLoginAttempts)
            .HasDefaultValue(0);

    }
}
