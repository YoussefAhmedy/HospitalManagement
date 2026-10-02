namespace Hospital.BLL.Services.Abstraction;

/// <summary>Queues an email outside the primary request path. False means it was not queued.</summary>
public interface IEmailQueue
{
    bool TryEnqueue(string to, string subject, string htmlBody);
}
