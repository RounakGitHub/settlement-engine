namespace SettlementEngine.Domain.Models;

public sealed class Settlement
{
    public Guid Id { get; init; }
    public Guid GroupId { get; init; }
    public Guid FromUserId { get; init; }
    public Guid ToUserId { get; init; }
    public decimal Amount { get; init; }
    public string? Note { get; init; }
    public SettlementStatus Status { get; set; }
    public DateTime ProposedAt { get; init; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime ExpiresAt { get; init; }
}

public enum SettlementStatus
{
    Proposed,
    Confirmed,
    Cancelled,
    Expired
}
