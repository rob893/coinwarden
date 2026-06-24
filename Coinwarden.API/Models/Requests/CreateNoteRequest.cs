using System.ComponentModel.DataAnnotations;

namespace Coinwarden.API.Models.Requests;

/// <summary>
/// Request model for creating a new note.
/// </summary>
public sealed record CreateNoteRequest
{
    /// <summary>Gets the note title.</summary>
    [Required]
    [MaxLength(255)]
    public string Title { get; init; } = default!;

    /// <summary>Gets the note content.</summary>
    [Required]
    public string Content { get; init; } = default!;
}
