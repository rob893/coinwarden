namespace Coinwarden.API.Models.QueryParameters;

/// <summary>
/// Query parameters for searching notes.
/// </summary>
public sealed record NoteQueryParameters : CursorPaginationQueryParameters
{
    /// <summary>Gets or sets an optional title filter (case-insensitive substring match).</summary>
    public string? Title { get; set; }
}
