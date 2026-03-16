using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Splitr.Domain.Entities;

namespace Splitr.Infrastructure.Persistence.Configurations;

public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("group_members");

        builder.HasKey(gm => new { gm.GroupId, gm.UserId });

        builder.Property(gm => gm.Role)
            .IsRequired()
            .HasConversion<string>();

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(gm => gm.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(gm => gm.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
