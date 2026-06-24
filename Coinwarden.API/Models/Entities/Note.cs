using System;
using System.ComponentModel.DataAnnotations;

namespace Coinwarden.API.Models.Entities;

/// <summary>
/// Represents a note owned by a user.
/// </summary>
public sealed class Note : IIdentifiable<int>, IOwnedByUser<int>
{
    /// <summary>Gets or sets the note ID.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the ID of the owning user.</summary>
    public int UserId { get; set; }

    /// <summary>Gets or sets the owning user navigation property.</summary>
    public User User { get; set; } = default!;

    /// <summary>Gets or sets the note title.</summary>
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the note content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets when the note was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the note was last updated (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
