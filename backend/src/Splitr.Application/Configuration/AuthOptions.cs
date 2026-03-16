namespace Splitr.Application.Configuration;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public required int BcryptCost { get; init; }
    public required int MaxFailedAttempts { get; init; }
    public required int LockoutMinutes { get; init; }
    public required int RefreshTokenExpiryDays { get; init; }
    public required string CookieName { get; init; }
    public required string CookiePath { get; init; }
}
