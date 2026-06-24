using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Moq;
using Coinwarden.API.Core;
using Coinwarden.API.Data.Repositories;
using Coinwarden.API.Models.Auth;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Services.Auth;
using Coinwarden.API.Services.Domain;

namespace Coinwarden.API.Tests.Services;

/// <summary>
/// Tests for <see cref="ExternalLoginService"/>.
/// </summary>
public sealed class ExternalLoginServiceTests
{
    private const string ProviderSubjectId = "external-subject-123";
    private const string SuggestedUserName = "newuser";
    private const string Email = "newuser@example.com";

    private readonly Mock<IUserRepository> userRepositoryMock;
    private readonly Mock<IUserService> userServiceMock;
    private readonly ExternalLoginService sut;

    public ExternalLoginServiceTests()
    {
        this.userRepositoryMock = new Mock<IUserRepository>();
        this.userServiceMock = new Mock<IUserService>();

        this.sut = new ExternalLoginService(
            this.userRepositoryMock.Object,
            this.userServiceMock.Object);
    }

    [Fact]
    public async Task ResolveOrProvisionUserAsync_ExistingLinkedAccount_ReturnsUserWithoutLinkingOrCreating()
    {
        var existing = BuildUser(7);
        this.userRepositoryMock
            .Setup(r => r.GetByLinkedAccountAsync(ProviderSubjectId, LinkedAccountType.Google, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await this.sut.ResolveOrProvisionUserAsync(BuildIdentity(LinkedAccountType.Google), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(existing, result.ValueOrThrow);

        this.userRepositoryMock.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
        this.userRepositoryMock.Verify(
            r => r.CreateUserWithoutPasswordAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveOrProvisionUserAsync_EmailMatchesExistingUser_VerifiedAndConfirmed_Links()
    {
        var existing = BuildUser(11);
        existing.EmailConfirmed = true;

        this.userRepositoryMock
            .Setup(r => r.GetByLinkedAccountAsync(ProviderSubjectId, LinkedAccountType.GitHub, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        this.userRepositoryMock
            .Setup(r => r.GetByEmailAsync(Email, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        this.userRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await this.sut.ResolveOrProvisionUserAsync(BuildIdentity(LinkedAccountType.GitHub, emailVerified: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(existing, result.ValueOrThrow);
        Assert.True(existing.EmailConfirmed);

        var linked = Assert.Single(existing.LinkedAccounts);
        Assert.Equal(ProviderSubjectId, linked.Id);
        Assert.Equal(LinkedAccountType.GitHub, linked.LinkedAccountType);

        this.userRepositoryMock.Verify(
            r => r.CreateUserWithoutPasswordAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveOrProvisionUserAsync_EmailMatchesExistingUser_ProviderUnverified_FailsWithoutLinking()
    {
        var existing = BuildUser(11);
        existing.EmailConfirmed = true;

        this.userRepositoryMock
            .Setup(r => r.GetByLinkedAccountAsync(ProviderSubjectId, LinkedAccountType.Google, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        this.userRepositoryMock
            .Setup(r => r.GetByEmailAsync(Email, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await this.sut.ResolveOrProvisionUserAsync(BuildIdentity(LinkedAccountType.Google, emailVerified: false), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Unauthorized, result.ErrorType);
        Assert.Empty(existing.LinkedAccounts);

        this.userRepositoryMock.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        this.userRepositoryMock.Verify(
            r => r.CreateUserWithoutPasswordAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveOrProvisionUserAsync_EmailMatchesExistingUser_LocalEmailUnconfirmed_FailsWithoutLinking()
    {
        var existing = BuildUser(11);
        existing.EmailConfirmed = false;

        this.userRepositoryMock
            .Setup(r => r.GetByLinkedAccountAsync(ProviderSubjectId, LinkedAccountType.Google, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        this.userRepositoryMock
            .Setup(r => r.GetByEmailAsync(Email, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await this.sut.ResolveOrProvisionUserAsync(BuildIdentity(LinkedAccountType.Google, emailVerified: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Unauthorized, result.ErrorType);
        Assert.Empty(existing.LinkedAccounts);

        this.userRepositoryMock.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        this.userRepositoryMock.Verify(
            r => r.CreateUserWithoutPasswordAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveOrProvisionUserAsync_LinkSaveReturnsZero_ReturnsUnknownFailure()
    {
        var existing = BuildUser(11);
        existing.EmailConfirmed = true;

        this.userRepositoryMock
            .Setup(r => r.GetByLinkedAccountAsync(ProviderSubjectId, LinkedAccountType.Google, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        this.userRepositoryMock
            .Setup(r => r.GetByEmailAsync(Email, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        this.userRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await this.sut.ResolveOrProvisionUserAsync(BuildIdentity(LinkedAccountType.Google, emailVerified: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Unknown, result.ErrorType);
    }

    [Fact]
    public async Task ResolveOrProvisionUserAsync_NoMatchAndUnconfirmed_CreatesUserAndSendsConfirmationEmail()
    {
        this.userRepositoryMock
            .Setup(r => r.GetByLinkedAccountAsync(ProviderSubjectId, LinkedAccountType.GitHub, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        this.userRepositoryMock
            .Setup(r => r.GetByEmailAsync(Email, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        this.userRepositoryMock
            .Setup(r => r.CreateUserWithoutPasswordAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await this.sut.ResolveOrProvisionUserAsync(BuildIdentity(LinkedAccountType.GitHub, emailVerified: false), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var created = result.ValueOrThrow;
        Assert.Equal(SuggestedUserName, created.UserName);
        Assert.Equal(Email, created.Email);
        Assert.False(created.EmailConfirmed);

        var linked = Assert.Single(created.LinkedAccounts);
        Assert.Equal(ProviderSubjectId, linked.Id);
        Assert.Equal(LinkedAccountType.GitHub, linked.LinkedAccountType);

        this.userServiceMock.Verify(
            s => s.SendEmailConfirmationAsync(created, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveOrProvisionUserAsync_NoMatchAndVerified_CreatesConfirmedUserWithoutSendingEmail()
    {
        this.userRepositoryMock
            .Setup(r => r.GetByLinkedAccountAsync(ProviderSubjectId, LinkedAccountType.Google, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        this.userRepositoryMock
            .Setup(r => r.GetByEmailAsync(Email, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        this.userRepositoryMock
            .Setup(r => r.CreateUserWithoutPasswordAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await this.sut.ResolveOrProvisionUserAsync(BuildIdentity(LinkedAccountType.Google, emailVerified: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.ValueOrThrow.EmailConfirmed);

        this.userServiceMock.Verify(
            s => s.SendEmailConfirmationAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveOrProvisionUserAsync_CreateFails_ReturnsValidationFailure()
    {
        this.userRepositoryMock
            .Setup(r => r.GetByLinkedAccountAsync(ProviderSubjectId, LinkedAccountType.Google, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        this.userRepositoryMock
            .Setup(r => r.GetByEmailAsync(Email, It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        this.userRepositoryMock
            .Setup(r => r.CreateUserWithoutPasswordAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName", Description = "Username already taken." }));

        var result = await this.sut.ResolveOrProvisionUserAsync(BuildIdentity(LinkedAccountType.Google), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Validation, result.ErrorType);
        Assert.Contains("Username already taken.", result.ErrorMessage, StringComparison.Ordinal);

        this.userServiceMock.Verify(
            s => s.SendEmailConfirmationAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ExternalLoginIdentity BuildIdentity(LinkedAccountType providerType, bool emailVerified = false) => new()
    {
        ProviderSubjectId = ProviderSubjectId,
        ProviderType = providerType,
        Email = Email,
        EmailVerified = emailVerified,
        SuggestedUserName = SuggestedUserName
    };

    private static User BuildUser(int id) => new()
    {
        Id = id,
        UserName = "user" + id,
        Email = $"user{id}@example.com"
    };
}
