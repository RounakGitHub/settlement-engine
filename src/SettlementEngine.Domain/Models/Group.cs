namespace SettlementEngine.Domain.Models;

public sealed class Group
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    public DateTime CreatedAt { get; init; }
}
