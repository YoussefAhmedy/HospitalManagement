using System.Text.Json;
using Hangfire;
using Hospital.BLL.Helpers;
using Hospital.BLL.Services.Abstraction;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace HospitalManagement.Infrastructure;

public sealed class HangfireEmailQueue : IEmailQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IDataProtector _protector;
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly ILogger<HangfireEmailQueue> _logger;

    public HangfireEmailQueue(
        IBackgroundJobClient backgroundJobs,
        IDataProtectionProvider protectionProvider,
        IOptionsMonitor<EmailOptions> options,
        ILogger<HangfireEmailQueue> logger)
    {
        _backgroundJobs = backgroundJobs;
        _protector = protectionProvider.CreateProtector("CareAxis.TransactionalEmail.v1");
        _options = options;
        _logger = logger;
    }

    public bool TryEnqueue(string to, string subject, string htmlBody)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _logger.LogInformation("Email delivery is disabled; no email was queued.");
            return false;
        }

        try
        {
            var payload = JsonSerializer.Serialize(
                new EmailDeliveryJob.EmailPayload(to, subject, htmlBody), JsonOptions);
            var protectedPayload = _protector.Protect(payload);
            _backgroundJobs.Enqueue<IEmailDeliveryJob>(job => job.DeliverAsync(protectedPayload));
            return true;
        }
        catch (Exception exception)
        {
            // Do not log recipient addresses, reset links, or message bodies.
            _logger.LogError(exception, "Unable to enqueue an email notification.");
            return false;
        }
    }
}
