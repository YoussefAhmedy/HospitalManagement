using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Clinical;

/// <summary>
/// Clinical documentation. A signed note cannot be edited; corrections create a new
/// note whose <see cref="AmendsNoteId"/> points at the original, preserving the trail.
/// </summary>
public class ClinicalNote
{
    public int Id { get; set; }

    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    [Required, MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? AuthorUserId { get; set; }

    [MaxLength(60)]
    public string? AuthorRole { get; set; }

    public NoteStatus Status { get; set; } = NoteStatus.Draft;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? SignedAtUtc { get; set; }

    [MaxLength(450)]
    public string? SignedByUserId { get; set; }

    public int? AmendsNoteId { get; set; }
    public ClinicalNote? AmendsNote { get; set; }
}
