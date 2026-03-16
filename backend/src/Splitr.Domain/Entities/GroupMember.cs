using Splitr.Domain.Enums;

namespace Splitr.Domain.Entities;

public class GroupMember
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public GroupRole Role { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
