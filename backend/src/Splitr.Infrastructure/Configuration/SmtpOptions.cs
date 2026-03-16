namespace Splitr.Infrastructure.Configuration;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string FromAddress { get; init; }
    public required string FromName { get; init; }
    public required bool EnableSsl { get; init; }
}
