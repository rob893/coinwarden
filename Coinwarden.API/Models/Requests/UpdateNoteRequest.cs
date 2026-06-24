using System.ComponentModel.DataAnnotations;

namespace Coinwarden.API.Models.Requests;

/// <summary>
/// Request model for updating an existing note.
/// </summary>
public sealed record UpdateNoteRequest
{
    /// <summary>Gets the updated note title.</summary>
    [Required]
    [MaxLength(255)]
    public string Title { get; init; } = default!;

    /// <summary>Gets the updated note content.</summary>
    [Required]
    public string Content { get; init; } = default!;
}
