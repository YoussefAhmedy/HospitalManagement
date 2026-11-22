using Hospital.BLL.Common;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Operations;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Implementation;

public sealed class NumberSequenceService(HospitalDbContext dbContext) : INumberSequenceService
{
    private const int MaxAttempts = 8;

    public async Task<long> NextAsync(string key, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var sequence = await dbContext.NumberSequences.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
            if (sequence is null)
            {
                sequence = new NumberSequence { Key = key, NextValue = 2 };
                dbContext.NumberSequences.Add(sequence);
            }
            else
            {
                sequence.NextValue++;
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return sequence.NextValue - 1;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request incremented the same row first; retry with fresh state.
                foreach (var entry in dbContext.ChangeTracker.Entries<NumberSequence>().ToList())
                {
                    await entry.ReloadAsync(cancellationToken);
                    if (entry.Entity.NextValue == 1)
                    {
                        // Row was created by a competitor after our read; treat as read-only.
                        continue;
                    }
                }
            }
        }

        throw new InvalidOperationException($"Could not allocate the next '{key}' number after {MaxAttempts} attempts.");
    }

    public async Task<string> NextFormattedAsync(string prefix, string key, CancellationToken cancellationToken = default) =>
        ClinicalNumber.Format(prefix, await NextAsync(key, cancellationToken));
}
