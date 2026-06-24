using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Coinwarden.API.Core;
using Coinwarden.API.Data.Repositories;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.Requests;
using Coinwarden.API.Services.Auth;
using Coinwarden.API.Services.Domain;
using Coinwarden.API.Services.Email;

namespace Coinwarden.API.Tests.Services;

/// <summary>
/// Tests for <see cref="UserService"/>.
/// </summary>
public sealed class UserServiceTests
{
    private const int UserId = 42;
    private const int OtherUserId = 99;
    private const int SystemUserId = 1;

    private readonly Mock<IUserRepository> userRepositoryMock;
    private readonly Mock<IEmailService> emailServiceMock;
    private readonly Mock<ICurrentUserService> currentUserServiceMock;
    private readonly UserService sut;

    public UserServiceTests()
    {
        this.userRepositoryMock = new Mock<IUserRepository>();
        this.emailServiceMock = new Mock<IEmailService>();
        this.currentUserServiceMock = new Mock<ICurrentUserService>();
        this.currentUserServiceMock.Setup(s => s.UserId).Returns(UserId);
        this.currentUserServiceMock.Setup(s => s.IsAdmin).Returns(false);

        this.sut = new UserService(
            NullLogger<UserService>.Instance,
            this.userRepositoryMock.Object,
            this.emailServiceMock.Object,
            this.currentUserServiceMock.Object);
    }

    [Fact]
    public async Task GetUserByIdAsync_OtherUserAndNotAdmin_ReturnsForbidden()
    {
        var result = await this.sut.GetUserByIdAsync(OtherUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Forbidden, result.ErrorType);
    }

    [Fact]
    public async Task GetUserByIdAsync_NotFound_ReturnsNotFound()
    {
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(UserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await this.sut.GetUserByIdAsync(UserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetUserByIdAsync_OwnUser_ReturnsSuccess()
    {
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(UserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildUser(UserId));

        var result = await this.sut.GetUserByIdAsync(UserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserId, result.ValueOrThrow.Id);
    }

    [Fact]
    public async Task GetUserByIdAsync_AdminAccessingOtherUser_ReturnsSuccess()
    {
        this.currentUserServiceMock.Setup(s => s.IsAdmin).Returns(true);
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(OtherUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildUser(OtherUserId));

        var result = await this.sut.GetUserByIdAsync(OtherUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteUserAsync_OtherUserAndNotAdmin_ReturnsForbidden()
    {
        var result = await this.sut.DeleteUserAsync(OtherUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Forbidden, result.ErrorType);
    }

    [Fact]
    public async Task DeleteUserAsync_SystemUser_ReturnsForbidden()
    {
        this.currentUserServiceMock.Setup(s => s.UserId).Returns(SystemUserId);

        var result = await this.sut.DeleteUserAsync(SystemUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Forbidden, result.ErrorType);
    }

    [Fact]
    public async Task DeleteUserAsync_NotFound_ReturnsNotFound()
    {
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(UserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await this.sut.DeleteUserAsync(UserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteUserAsync_SaveReturnsZero_ReturnsUnknownFailure()
    {
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(UserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildUser(UserId));
        this.userRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await this.sut.DeleteUserAsync(UserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Unknown, result.ErrorType);
    }

    [Fact]
    public async Task DeleteUserAsync_Success_ReturnsTrue()
    {
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(UserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildUser(UserId));
        this.userRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await this.sut.DeleteUserAsync(UserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.ValueOrThrow);
    }

    [Fact]
    public async Task DeleteUserLinkedAccountAsync_LinkedAccountNotFound_ReturnsNotFound()
    {
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(UserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildUser(UserId));

        var result = await this.sut.DeleteUserLinkedAccountAsync(UserId, LinkedAccountType.Google, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteUserLinkedAccountAsync_Success_RemovesAccount()
    {
        var user = BuildUser(UserId);
        user.LinkedAccounts.Add(new LinkedAccount { Id = "abc", LinkedAccountType = LinkedAccountType.Google });
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(UserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        this.userRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await this.sut.DeleteUserLinkedAccountAsync(UserId, LinkedAccountType.Google, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.LinkedAccounts);
    }

    [Fact]
    public async Task AddRolesToUserAsync_NoRolesSpecified_ReturnsValidation()
    {
        var result = await this.sut.AddRolesToUserAsync(UserId, new EditRoleRequest { RoleNames = [] }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task AddRolesToUserAsync_UserNotFound_ReturnsNotFound()
    {
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(UserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await this.sut.AddRolesToUserAsync(UserId, new EditRoleRequest { RoleNames = ["Admin"] }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task RemoveRolesFromUserAsync_NoRolesSpecified_ReturnsValidation()
    {
        var result = await this.sut.RemoveRolesFromUserAsync(UserId, new EditRoleRequest { RoleNames = [] }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateUsernameAsync_OtherUserAndNotAdmin_ReturnsForbidden()
    {
        var result = await this.sut.UpdateUsernameAsync(OtherUserId, new UpdateUsernameRequest { NewUsername = "newname" }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Forbidden, result.ErrorType);
    }

    [Fact]
    public async Task UpdateUsernameAsync_NoRecentAuthentication_ReturnsValidation()
    {
        var user = BuildUser(UserId);
        user.LastLogin = DateTimeOffset.UtcNow.AddHours(-2);
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(UserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await this.sut.UpdateUsernameAsync(UserId, new UpdateUsernameRequest { NewUsername = "newname" }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdatePasswordAsync_OtherUserAndNotAdmin_ReturnsForbidden()
    {
        var result = await this.sut.UpdatePasswordAsync(OtherUserId, new UpdatePasswordRequest { OldPassword = "a", NewPassword = "b" }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Forbidden, result.ErrorType);
    }

    [Fact]
    public async Task SendEmailConfirmationAsync_AlreadyConfirmed_ReturnsValidation()
    {
        var user = BuildUser(UserId);
        user.EmailConfirmed = true;
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(UserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await this.sut.SendEmailConfirmationAsync(UserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task SendEmailConfirmationAsync_NoEmail_ReturnsValidation()
    {
        var user = BuildUser(UserId);
        user.Email = null;
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(UserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await this.sut.SendEmailConfirmationAsync(UserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task ConfirmEmailAsync_UserNotFound_ReturnsValidation()
    {
        var userManagerMock = BuildUserManagerMock();
        userManagerMock.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        this.userRepositoryMock.Setup(r => r.UserManager).Returns(userManagerMock.Object);

        var result = await this.sut.ConfirmEmailAsync(new ConfirmEmailRequest { Email = "a@b.com", Token = "tok" }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task ForgotPasswordAsync_UnknownEmail_ReturnsSuccessToPreventEnumeration()
    {
        var userManagerMock = BuildUserManagerMock();
        userManagerMock.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        this.userRepositoryMock.Setup(r => r.UserManager).Returns(userManagerMock.Object);

        var result = await this.sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "unknown@b.com" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.ValueOrThrow);
        this.emailServiceMock.Verify(
            e => e.SendResetPasswordToUserAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_UnknownEmail_ReturnsSuccessToPreventEnumeration()
    {
        var userManagerMock = BuildUserManagerMock();
        userManagerMock.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        this.userRepositoryMock.Setup(r => r.UserManager).Returns(userManagerMock.Object);

        var result = await this.sut.ResetPasswordAsync(new ResetPasswordRequest { Email = "unknown@b.com", Password = "Password1!", Token = "tok" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.ValueOrThrow);
    }

    private static Mock<UserManager<User>> BuildUserManagerMock()
    {
        var store = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static User BuildUser(int id) => new()
    {
        Id = id,
        UserName = "user" + id,
        Email = $"user{id}@example.com"
    };
}
