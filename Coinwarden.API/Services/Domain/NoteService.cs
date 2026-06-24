using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Core;
using Coinwarden.API.Data.Repositories;
using Coinwarden.API.Extensions;
using Coinwarden.API.Models.Dtos;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.QueryParameters;
using Coinwarden.API.Models.Requests;
using Coinwarden.API.Services.Auth;
using Microsoft.Extensions.Logging;

namespace Coinwarden.API.Services.Domain;

/// <summary>
/// Service for note management.
/// </summary>
public sealed class NoteService : INoteService
{
    private readonly ILogger<NoteService> logger;

    private readonly INoteRepository noteRepository;

    private readonly ICurrentUserService currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoteService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="noteRepository">The note repository.</param>
    /// <param name="currentUserService">The current user service.</param>
    public NoteService(
        ILogger<NoteService> logger,
        INoteRepository noteRepository,
        ICurrentUserService currentUserService)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.noteRepository = noteRepository ?? throw new ArgumentNullException(nameof(noteRepository));
        this.currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc />
    public async Task<CursorPaginatedList<NoteDto, int>> GetNotesAsync(NoteQueryParameters queryParameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryParameters);

        // Scope to current user unless admin viewing all
        var userId = this.currentUserService.UserId;
        var originalTitle = queryParameters.Title;

        // Inject the user filter via a custom query
        var pagedList = await this.noteRepository.SearchAsync(
            n => n.UserId == userId,
            track: false,
            cancellationToken);

        // Apply title filter in memory if specified
        var filtered = string.IsNullOrWhiteSpace(queryParameters.Title)
            ? pagedList
            : pagedList.Where(n => n.Title.Contains(queryParameters.Title, StringComparison.OrdinalIgnoreCase)).ToList();

        var mapped = filtered.Select(NoteDto.FromEntity).ToList();

        return mapped.ToCursorPaginatedList(queryParameters);
    }

    /// <inheritdoc />
    public async Task<Result<NoteDto>> GetNoteByIdAsync(int id, CancellationToken cancellationToken)
    {
        var note = await this.noteRepository.GetByIdAsync(id, track: false, cancellationToken);

        if (note == null)
        {
            this.logger.LogWarning("Note {NoteId} not found", id);
            return Result<NoteDto>.Failure(DomainErrorType.NotFound, "Note not found");
        }

        if (note.UserId != this.currentUserService.UserId && !this.currentUserService.IsAdmin)
        {
            this.logger.LogWarning("User {UserId} attempted to access note {NoteId} owned by {OwnerId}", this.currentUserService.UserId, id, note.UserId);
            return Result<NoteDto>.Failure(DomainErrorType.Forbidden, "You can only access your own notes");
        }

        return Result<NoteDto>.Success(NoteDto.FromEntity(note));
    }

    /// <inheritdoc />
    public async Task<Result<NoteDto>> CreateNoteAsync(CreateNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var note = new Note
        {
            UserId = this.currentUserService.UserId,
            Title = request.Title,
            Content = request.Content,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        this.noteRepository.Add(note);
        await this.noteRepository.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation("Created note {NoteId} for user {UserId}", note.Id, note.UserId);

        return Result<NoteDto>.Success(NoteDto.FromEntity(note));
    }

    /// <inheritdoc />
    public async Task<Result<NoteDto>> UpdateNoteAsync(int id, UpdateNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var note = await this.noteRepository.GetByIdAsync(id, track: true, cancellationToken);

        if (note == null)
        {
            this.logger.LogWarning("Note {NoteId} not found for update", id);
            return Result<NoteDto>.Failure(DomainErrorType.NotFound, "Note not found");
        }

        if (note.UserId != this.currentUserService.UserId && !this.currentUserService.IsAdmin)
        {
            this.logger.LogWarning("User {UserId} attempted to update note {NoteId} owned by {OwnerId}", this.currentUserService.UserId, id, note.UserId);
            return Result<NoteDto>.Failure(DomainErrorType.Forbidden, "You can only update your own notes");
        }

        note.Title = request.Title;
        note.Content = request.Content;
        note.UpdatedAt = DateTimeOffset.UtcNow;

        await this.noteRepository.SaveChangesAsync(cancellationToken);

        return Result<NoteDto>.Success(NoteDto.FromEntity(note));
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteNoteAsync(int id, CancellationToken cancellationToken)
    {
        var note = await this.noteRepository.GetByIdAsync(id, track: true, cancellationToken);

        if (note == null)
        {
            this.logger.LogWarning("Note {NoteId} not found for deletion", id);
            return Result<bool>.Failure(DomainErrorType.NotFound, "Note not found");
        }

        if (note.UserId != this.currentUserService.UserId && !this.currentUserService.IsAdmin)
        {
            this.logger.LogWarning("User {UserId} attempted to delete note {NoteId} owned by {OwnerId}", this.currentUserService.UserId, id, note.UserId);
            return Result<bool>.Failure(DomainErrorType.Forbidden, "You can only delete your own notes");
        }

        this.noteRepository.Remove(note);
        await this.noteRepository.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation("Deleted note {NoteId} for user {UserId}", id, this.currentUserService.UserId);

        return Result<bool>.Success(true);
    }
}
