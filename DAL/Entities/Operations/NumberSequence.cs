using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Operations;

/// <summary>
/// Row-version guarded counter used to allocate human-readable document numbers
/// (PT-000001, ENC-000001, LAB-000001, ...). Centralising this prevents duplicate
/// identifiers under concurrency.
/// </summary>
public class NumberSequence
{
    [Key, MaxLength(20)]
    public string Key { get; set; } = string.Empty;

    public long NextValue { get; set; } = 1;

    public byte[] RowVersion { get; set; } = [];
}
