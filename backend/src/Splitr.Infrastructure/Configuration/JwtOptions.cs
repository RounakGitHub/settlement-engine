namespace Splitr.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public required string RsaPrivateKeyPem { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required int ExpiryMinutes { get; init; }
}
