using System.Text.Json;
using Hospital.BLL.Services.Abstraction;
using Microsoft.AspNetCore.DataProtection;

namespace HospitalManagement.Infrastructure;

public interface IEmailDeliveryJob
{
    Task DeliverAsync(string protectedPayload);
}

/// <summary>
/// Hangfire stores only a Data Protection ciphertext, not recipient addresses, reset
/// links, or HTML message bodies. Production must persist/share the Data Protection key ring.
/// </summary>
public sealed class EmailDeliveryJob : IEmailDeliveryJob
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector;
    private readonly IEmailSender _sender;

    public EmailDeliveryJob(IDataProtectionProvider protectionProvider, IEmailSender sender)
    {
        _protector = protectionProvider.CreateProtector("CareAxis.TransactionalEmail.v1");
        _sender = sender;
    }

    public async Task DeliverAsync(string protectedPayload)
    {
        var json = _protector.Unprotect(protectedPayload);
        var payload = JsonSerializer.Deserialize<EmailPayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("Queued email payload was invalid.");

        await _sender.SendAsync(payload.To, payload.Subject, payload.HtmlBody);
    }

    internal sealed record EmailPayload(string To, string Subject, string HtmlBody);
}
