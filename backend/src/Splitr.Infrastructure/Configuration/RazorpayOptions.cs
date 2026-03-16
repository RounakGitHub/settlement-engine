namespace Splitr.Infrastructure.Configuration;

public sealed class RazorpayOptions
{
    public const string SectionName = "Razorpay";
    public required string WebhookSecret { get; init; }
    public required string[] AllowedIps { get; init; }
}
