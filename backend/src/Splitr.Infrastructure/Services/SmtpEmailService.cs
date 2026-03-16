using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Splitr.Application.Interfaces;
using Splitr.Infrastructure.Configuration;

namespace Splitr.Infrastructure.Services;

public class SmtpEmailService(IOptions<SmtpOptions> options, ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly SmtpOptions _smtp = options.Value;

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_smtp.FromAddress, _smtp.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        using var client = new SmtpClient(_smtp.Host, _smtp.Port)
        {
            Credentials = new NetworkCredential(_smtp.Username, _smtp.Password),
            EnableSsl = _smtp.EnableSsl
        };

        await client.SendMailAsync(message, ct);

        logger.LogDebug("Email sent to {Recipient}: {Subject}", to, subject);
    }
}
