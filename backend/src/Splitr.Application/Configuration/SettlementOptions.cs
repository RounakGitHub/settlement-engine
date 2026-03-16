namespace Splitr.Application.Configuration;

public sealed class SettlementOptions
{
    public const string SectionName = "Settlement";

    public required int ExpiryHours { get; init; }
    public required int LockTimeoutSeconds { get; init; }
    public required int IdempotencyTtlHours { get; init; }
}
