namespace Splitr.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GoogleId { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
}
