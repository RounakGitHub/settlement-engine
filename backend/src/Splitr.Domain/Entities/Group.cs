namespace Splitr.Domain.Entities;

public class Group : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "INR";
    public string? Category { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public string? InviteCode { get; set; }
    public DateTime? DeleteAfter { get; set; }
}
