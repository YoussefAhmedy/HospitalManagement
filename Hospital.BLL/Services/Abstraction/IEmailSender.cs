namespace Hospital.BLL.Services.Abstraction;

/// <summary>Provider-level email delivery. Failures are thrown so a background processor can retry.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody);
}
