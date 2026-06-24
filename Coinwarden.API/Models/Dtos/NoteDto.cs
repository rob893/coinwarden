using System;
using Coinwarden.API.Models.Entities;

namespace Coinwarden.API.Models.Dtos;

/// <summary>
/// Data transfer object representing a note.
/// </summary>
public sealed record NoteDto : IIdentifiable<int>
{
    /// <summary>Gets the note ID.</summary>
    public required int Id { get; init; }

    /// <summary>Gets the ID of the owning user.</summary>
    public required int UserId { get; init; }

    /// <summary>Gets the note title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the note content.</summary>
    public required string Content { get; init; }

    /// <summary>Gets when the note was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the note was last updated.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Creates a <see cref="NoteDto"/> from a <see cref="Note"/> entity.
    /// </summary>
    /// <param name="note">The note entity.</param>
    /// <returns>A mapped <see cref="NoteDto"/>.</returns>
    public static NoteDto FromEntity(Note note)
    {
        ArgumentNullException.ThrowIfNull(note);

        return new NoteDto
        {
            Id = note.Id,
            UserId = note.UserId,
            Title = note.Title,
            Content = note.Content,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        };
    }
}
