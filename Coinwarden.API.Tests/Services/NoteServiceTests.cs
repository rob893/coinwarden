using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Coinwarden.API.Core;
using Coinwarden.API.Data.Repositories;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.Requests;
using Coinwarden.API.Services.Auth;
using Coinwarden.API.Services.Domain;

namespace Coinwarden.API.Tests.Services;

/// <summary>
/// Tests for <see cref="NoteService"/>.
/// </summary>
public sealed class NoteServiceTests
{
    private readonly Mock<INoteRepository> noteRepositoryMock;
    private readonly Mock<ICurrentUserService> currentUserServiceMock;
    private readonly NoteService sut;

    private const int UserId = 42;
    private const int OtherUserId = 99;

    public NoteServiceTests()
    {
        this.noteRepositoryMock = new Mock<INoteRepository>();
        this.currentUserServiceMock = new Mock<ICurrentUserService>();
        this.currentUserServiceMock.Setup(s => s.UserId).Returns(UserId);
        this.currentUserServiceMock.Setup(s => s.IsAdmin).Returns(false);

        this.sut = new NoteService(
            NullLogger<NoteService>.Instance,
            this.noteRepositoryMock.Object,
            this.currentUserServiceMock.Object);
    }

    [Fact]
    public async Task GetNoteByIdAsync_ExistingOwnNote_ReturnsSuccess()
    {
        var note = BuildNote(id: 1, userId: UserId);
        this.noteRepositoryMock
            .Setup(r => r.GetByIdAsync(1, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var result = await this.sut.GetNoteByIdAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(note.Title, result.ValueOrThrow.Title);
    }

    [Fact]
    public async Task GetNoteByIdAsync_NotFound_ReturnsNotFoundFailure()
    {
        this.noteRepositoryMock
            .Setup(r => r.GetByIdAsync(99, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Note?)null);

        var result = await this.sut.GetNoteByIdAsync(99, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetNoteByIdAsync_OtherUsersNote_ReturnsForbiddenFailure()
    {
        var note = BuildNote(id: 2, userId: OtherUserId);
        this.noteRepositoryMock
            .Setup(r => r.GetByIdAsync(2, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var result = await this.sut.GetNoteByIdAsync(2, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Forbidden, result.ErrorType);
    }

    [Fact]
    public async Task CreateNoteAsync_ValidRequest_ReturnsSuccess()
    {
        var request = new CreateNoteRequest { Title = "Test", Content = "Body" };
        Note? capturedNote = null;

        this.noteRepositoryMock
            .Setup(r => r.Add(It.IsAny<Note>()))
            .Callback<Note>(n => capturedNote = n);
        this.noteRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await this.sut.CreateNoteAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedNote);
        Assert.Equal(UserId, capturedNote!.UserId);
        Assert.Equal("Test", capturedNote.Title);
    }

    [Fact]
    public async Task UpdateNoteAsync_OtherUsersNote_ReturnsForbiddenFailure()
    {
        var note = BuildNote(id: 3, userId: OtherUserId);
        this.noteRepositoryMock
            .Setup(r => r.GetByIdAsync(3, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var request = new UpdateNoteRequest { Title = "New", Content = "New content" };
        var result = await this.sut.UpdateNoteAsync(3, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Forbidden, result.ErrorType);
    }

    [Fact]
    public async Task DeleteNoteAsync_NotFound_ReturnsNotFoundFailure()
    {
        this.noteRepositoryMock
            .Setup(r => r.GetByIdAsync(100, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Note?)null);

        var result = await this.sut.DeleteNoteAsync(100, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteNoteAsync_OwnNote_ReturnsSuccess()
    {
        var note = BuildNote(id: 5, userId: UserId);
        this.noteRepositoryMock
            .Setup(r => r.GetByIdAsync(5, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);
        this.noteRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await this.sut.DeleteNoteAsync(5, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.ValueOrThrow);
    }

    private static Note BuildNote(int id, int userId) => new()
    {
        Id = id,
        UserId = userId,
        Title = "Test Note",
        Content = "Content here",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
