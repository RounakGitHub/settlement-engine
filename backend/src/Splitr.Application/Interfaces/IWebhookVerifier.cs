namespace Splitr.Application.Interfaces;

public interface IWebhookVerifier
{
    bool VerifySignature(string rawBody, string signature);
    bool IsAllowedIp(string sourceIp);
}
