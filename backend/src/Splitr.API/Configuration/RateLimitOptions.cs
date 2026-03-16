namespace Splitr.API.Configuration;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public required RateLimitPolicyOptions AuthLogin { get; init; }
    public required RateLimitPolicyOptions AuthRegister { get; init; }
    public required RateLimitPolicyOptions Write { get; init; }
    public required RateLimitPolicyOptions Settlement { get; init; }
    public required RateLimitPolicyOptions General { get; init; }
}

public sealed class RateLimitPolicyOptions
{
    public required int PermitLimit { get; init; }
    public required int WindowSeconds { get; init; }
    public required int SegmentsPerWindow { get; init; }
}
