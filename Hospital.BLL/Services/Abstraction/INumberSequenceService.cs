namespace Hospital.BLL.Services.Abstraction;

/// <summary>Allocates gap-free document numbers under concurrency.</summary>
public interface INumberSequenceService
{
    Task<long> NextAsync(string key, CancellationToken cancellationToken = default);

    Task<string> NextFormattedAsync(string prefix, string key, CancellationToken cancellationToken = default);
}
