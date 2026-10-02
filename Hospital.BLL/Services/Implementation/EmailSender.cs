using Hospital.BLL.Helpers;
using Hospital.BLL.Services.Abstraction;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Hospital.BLL.Services.Implementation;

public sealed class EmailSender(
    IOptionsMonitor<EmailOptions> options,
    ILogger<EmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var settings = options.CurrentValue;
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("Email delivery is disabled or not configured.");
        }

        var message = new MimeMessage
        {
            Sender = MailboxAddress.Parse(settings.From),
            Subject = subject,
            Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody()
        };
        message.From.Add(new MailboxAddress("CareAxis", settings.From));
        message.To.Add(MailboxAddress.Parse(to));

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(settings.SmtpServer, settings.Port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(settings.From, settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception exception)
        {
            // Hangfire retries thrown failures. Avoid logging recipient or body content.
            logger.LogError(exception, "Transactional email delivery failed.");
            throw;
        }
    }
}
