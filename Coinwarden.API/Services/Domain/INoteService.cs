using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Core;
using Coinwarden.API.Models.Dtos;
using Coinwarden.API.Models.QueryParameters;
using Coinwarden.API.Models.Requests;

namespace Coinwarden.API.Services.Domain;

/// <summary>
/// Service interface for note management.
/// </summary>
public interface INoteService
{
    /// <summary>
    /// Gets a cursor-paginated list of notes for the current user.
    /// </summary>
    /// <param name="queryParameters">The pagination and filter parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paginated list of note DTOs.</returns>
    Task<CursorPaginatedList<NoteDto, int>> GetNotesAsync(NoteQueryParameters queryParameters, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a single note by ID.
    /// </summary>
    /// <param name="id">The note ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The note DTO if found and accessible; otherwise a failure result.</returns>
    Task<Result<NoteDto>> GetNoteByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new note for the current user.
    /// </summary>
    /// <param name="request">The create request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created note DTO.</returns>
    Task<Result<NoteDto>> CreateNoteAsync(CreateNoteRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing note.
    /// </summary>
    /// <param name="id">The note ID.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated note DTO.</returns>
    Task<Result<NoteDto>> UpdateNoteAsync(int id, UpdateNoteRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a note.
    /// </summary>
    /// <param name="id">The note ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result<bool>> DeleteNoteAsync(int id, CancellationToken cancellationToken);
}
