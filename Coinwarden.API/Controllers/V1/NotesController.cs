using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Coinwarden.API.Extensions;
using Coinwarden.API.Models.Dtos;
using Coinwarden.API.Models.QueryParameters;
using Coinwarden.API.Models.Requests;
using Coinwarden.API.Models.Responses.Pagination;
using Coinwarden.API.Services.Core;
using Coinwarden.API.Services.Domain;

namespace Coinwarden.API.Controllers.V1;

/// <summary>
/// Controller for managing notes.
/// </summary>
[Route("api/v{version:apiVersion}/notes")]
[ApiVersion("1")]
[ApiController]
public sealed class NotesController : ServiceControllerBase
{
    private readonly INoteService noteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotesController"/> class.
    /// </summary>
    /// <param name="noteService">The note service.</param>
    /// <param name="correlationIdService">The correlation ID service.</param>
    public NotesController(INoteService noteService, ICorrelationIdService correlationIdService)
        : base(correlationIdService)
    {
        this.noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
    }

    /// <summary>
    /// Gets a cursor-paginated list of notes for the current user.
    /// </summary>
    /// <param name="queryParameters">The pagination and filter query parameters.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A paginated list of note DTOs.</returns>
    /// <response code="200">Returns the paginated list of notes.</response>
    [HttpGet(Name = nameof(GetNotesAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPaginatedResponse<NoteDto>>> GetNotesAsync([FromQuery] NoteQueryParameters queryParameters, CancellationToken cancellationToken)
    {
        var notes = await this.noteService.GetNotesAsync(queryParameters, cancellationToken);
        var response = notes.ToCursorPaginatedResponse(queryParameters);

        return this.Ok(response);
    }

    /// <summary>
    /// Gets a specific note by ID.
    /// </summary>
    /// <param name="id">The note ID.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The note DTO.</returns>
    /// <response code="200">Returns the note.</response>
    /// <response code="403">If the note does not belong to the current user.</response>
    /// <response code="404">If the note is not found.</response>
    [HttpGet("{id}", Name = nameof(GetNoteByIdAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NoteDto>> GetNoteByIdAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
        var result = await this.noteService.GetNoteByIdAsync(id, cancellationToken);

        if (!result.IsSuccess)
        {
            return this.HandleServiceFailureResult(result);
        }

        return this.Ok(result.ValueOrThrow);
    }

    /// <summary>
    /// Creates a new note for the current user.
    /// </summary>
    /// <param name="request">The create note request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created note.</returns>
    /// <response code="201">The note was created successfully.</response>
    /// <response code="400">If the request is invalid.</response>
    [HttpPost(Name = nameof(CreateNoteAsync))]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NoteDto>> CreateNoteAsync([FromBody] CreateNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await this.noteService.CreateNoteAsync(request, cancellationToken);

        if (!result.IsSuccess)
        {
            return this.HandleServiceFailureResult(result);
        }

        var note = result.ValueOrThrow;

        return this.CreatedAtRoute(nameof(this.GetNoteByIdAsync), new { id = note.Id }, note);
    }

    /// <summary>
    /// Updates an existing note.
    /// </summary>
    /// <param name="id">The note ID.</param>
    /// <param name="request">The update note request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The updated note.</returns>
    /// <response code="200">Returns the updated note.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="403">If the note does not belong to the current user.</response>
    /// <response code="404">If the note is not found.</response>
    [HttpPut("{id}", Name = nameof(UpdateNoteAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NoteDto>> UpdateNoteAsync([FromRoute] int id, [FromBody] UpdateNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await this.noteService.UpdateNoteAsync(id, request, cancellationToken);

        if (!result.IsSuccess)
        {
            return this.HandleServiceFailureResult(result);
        }

        return this.Ok(result.ValueOrThrow);
    }

    /// <summary>
    /// Deletes a note.
    /// </summary>
    /// <param name="id">The note ID.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">The note was deleted successfully.</response>
    /// <response code="403">If the note does not belong to the current user.</response>
    /// <response code="404">If the note is not found.</response>
    [HttpDelete("{id}", Name = nameof(DeleteNoteAsync))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteNoteAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
        var result = await this.noteService.DeleteNoteAsync(id, cancellationToken);

        if (!result.IsSuccess)
        {
            return this.HandleServiceFailureResult(result);
        }

        return this.NoContent();
    }
}
