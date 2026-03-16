using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Splitr.Application.Interfaces;
using Splitr.Infrastructure.Configuration;

namespace Splitr.Infrastructure.Services;

public class RazorpayWebhookVerifier(IOptions<RazorpayOptions> options) : IWebhookVerifier
{
    private readonly RazorpayOptions _options = options.Value;

    public bool VerifySignature(string rawBody, string signature)
    {
        var secretBytes = Encoding.UTF8.GetBytes(_options.WebhookSecret);
        var bodyBytes = Encoding.UTF8.GetBytes(rawBody);
        var computedHash = HMACSHA256.HashData(secretBytes, bodyBytes);
        var expectedSignature = Convert.ToHexStringLower(computedHash);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(signature)
        );
    }

    public bool IsAllowedIp(string sourceIp)
    {
        // Empty list = allow all (dev mode)
        if (_options.AllowedIps.Length == 0)
            return true;

        return _options.AllowedIps.Contains(sourceIp);
    }
}
