using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Constants;
using Coinwarden.API.Controllers.V1;
using Coinwarden.API.Core;
using Coinwarden.API.Data.Repositories;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.Requests.Auth;
using Coinwarden.API.Services.Auth;
using Coinwarden.API.Services.Core;
using Coinwarden.API.Services.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Coinwarden.API.Tests.Controllers;

/// <summary>
/// Tests for <see cref="AuthController"/> (v1).
/// </summary>
public sealed class AuthControllerTests
{
    private const string DeviceId = "device-1";

    private readonly Mock<IUserRepository> userRepositoryMock;
    private readonly Mock<IJwtTokenService> jwtTokenServiceMock;
    private readonly Mock<IUserService> userServiceMock;
    private readonly Mock<IAuthTokenCookieService> authTokenCookieServiceMock;
    private readonly Mock<IOAuthFlowCookieService> oauthFlowCookieServiceMock;
    private readonly Mock<ICorrelationIdService> correlationIdServiceMock;
    private readonly AuthController sut;

    public AuthControllerTests()
    {
        this.userRepositoryMock = new Mock<IUserRepository>();
        this.jwtTokenServiceMock = new Mock<IJwtTokenService>();
        this.userServiceMock = new Mock<IUserService>();
        this.authTokenCookieServiceMock = new Mock<IAuthTokenCookieService>();
        this.oauthFlowCookieServiceMock = new Mock<IOAuthFlowCookieService>();
        this.correlationIdServiceMock = new Mock<ICorrelationIdService>();
        this.correlationIdServiceMock.Setup(s => s.CorrelationId).Returns("corr-id");

        this.authTokenCookieServiceMock
            .Setup(s => s.GenerateAndSaveAccessAndRefreshTokensAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync("access-token");

        this.sut = new AuthController(
            this.userRepositoryMock.Object,
            this.jwtTokenServiceMock.Object,
            this.userServiceMock.Object,
            this.authTokenCookieServiceMock.Object,
            this.oauthFlowCookieServiceMock.Object,
            this.correlationIdServiceMock.Object);

        this.sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task RegisterAsync_Success_Returns201()
    {
        var userManagerMock = BuildUserManagerMock();
        userManagerMock
            .Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<User>()))
            .ReturnsAsync("email-token");
        this.userRepositoryMock.Setup(r => r.UserManager).Returns(userManagerMock.Object);
        this.userRepositoryMock
            .Setup(r => r.CreateUserWithPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var request = new RegisterUserRequest
        {
            UserName = "newuser",
            Email = "newuser@example.com",
            Password = "Password1!",
            DeviceId = DeviceId
        };

        var result = await this.sut.RegisterAsync(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtRouteResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
    }

    [Fact]
    public async Task RegisterAsync_NullRequest_ReturnsBadRequest()
    {
        var result = await this.sut.RegisterAsync(null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RegisterAsync_CreateFails_ReturnsBadRequest()
    {
        this.userRepositoryMock
            .Setup(r => r.CreateUserWithPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email taken" }));

        var request = new RegisterUserRequest
        {
            UserName = "newuser",
            Email = "taken@example.com",
            Password = "Password1!",
            DeviceId = DeviceId
        };

        var result = await this.sut.RegisterAsync(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateUserName_ReturnsGenericMessageWithoutDisclosure()
    {
        this.userRepositoryMock
            .Setup(r => r.CreateUserWithPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = nameof(IdentityErrorDescriber.DuplicateUserName),
                Description = "Username 'newuser' is already taken."
            }));

        var request = new RegisterUserRequest
        {
            UserName = "newuser",
            Email = "newuser@example.com",
            Password = "Password1!",
            DeviceId = DeviceId
        };

        var result = await this.sut.RegisterAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetailsWithErrors>(badRequest.Value);
        var messages = Assert.IsType<List<string>>(problem.Extensions["errors"]);
        Assert.Equal(new[] { "Registration could not be completed." }, messages);
        Assert.DoesNotContain(messages, m => m.Contains("newuser", System.StringComparison.Ordinal));
        Assert.DoesNotContain(messages, m => m.Contains("taken", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsGenericMessageWithoutDisclosure()
    {
        this.userRepositoryMock
            .Setup(r => r.CreateUserWithPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = nameof(IdentityErrorDescriber.DuplicateEmail),
                Description = "Email 'taken@example.com' is already taken."
            }));

        var request = new RegisterUserRequest
        {
            UserName = "newuser",
            Email = "taken@example.com",
            Password = "Password1!",
            DeviceId = DeviceId
        };

        var result = await this.sut.RegisterAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetailsWithErrors>(badRequest.Value);
        var messages = Assert.IsType<List<string>>(problem.Extensions["errors"]);
        Assert.Equal(new[] { "Registration could not be completed." }, messages);
        Assert.DoesNotContain(messages, m => m.Contains("taken@example.com", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task RegisterAsync_NonEnumeratingError_RetainsDescription()
    {
        this.userRepositoryMock
            .Setup(r => r.CreateUserWithPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = nameof(IdentityErrorDescriber.PasswordTooShort),
                Description = "Passwords must be at least 8 characters."
            }));

        var request = new RegisterUserRequest
        {
            UserName = "newuser",
            Email = "newuser@example.com",
            Password = "short",
            DeviceId = DeviceId
        };

        var result = await this.sut.RegisterAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetailsWithErrors>(badRequest.Value);
        var messages = Assert.IsType<List<string>>(problem.Extensions["errors"]);
        Assert.Contains("Passwords must be at least 8 characters.", messages);
    }

    [Fact]
    public async Task LoginAsync_Success_Returns200()
    {
        var user = BuildUser();
        this.userRepositoryMock
            .Setup(r => r.GetByUsernameOrEmailAsync("jane", It.IsAny<Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        this.userRepositoryMock
            .Setup(r => r.CheckPasswordAsync(user, "Password1!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await this.sut.LoginAsync(new LoginRequest { UserName = "jane", Password = "Password1!", DeviceId = DeviceId }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);

        // The combined username-or-email query must be used exactly once, and the legacy
        // two-call lookup must not be invoked on the login path.
        this.userRepositoryMock.Verify(
            r => r.GetByUsernameOrEmailAsync("jane", It.IsAny<Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
        this.userRepositoryMock.Verify(
            r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
        this.userRepositoryMock.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithEmail_Success_Returns200()
    {
        var user = BuildUser();
        this.userRepositoryMock
            .Setup(r => r.GetByUsernameOrEmailAsync("jane@example.com", It.IsAny<Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        this.userRepositoryMock
            .Setup(r => r.CheckPasswordAsync(user, "Password1!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await this.sut.LoginAsync(new LoginRequest { UserName = "jane@example.com", Password = "Password1!", DeviceId = DeviceId }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);

        this.userRepositoryMock.Verify(
            r => r.GetByUsernameOrEmailAsync("jane@example.com", It.IsAny<Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_UnknownUser_Returns401()
    {
        var userManagerMock = BuildUserManagerMock();
        userManagerMock.Object.PasswordHasher = new PasswordHasher<User>();
        this.userRepositoryMock.Setup(r => r.UserManager).Returns(userManagerMock.Object);

        this.userRepositoryMock
            .Setup(r => r.GetByUsernameOrEmailAsync(It.IsAny<string>(), It.IsAny<Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await this.sut.LoginAsync(new LoginRequest { UserName = "ghost", Password = "x", DeviceId = DeviceId }, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);

        // The not-found path must still invoke the dummy-hash verification to equalize timing,
        // and must not fall back to the legacy username/email lookups.
        this.userRepositoryMock.Verify(
            r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
        this.userRepositoryMock.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_Returns401()
    {
        var user = BuildUser();
        this.userRepositoryMock
            .Setup(r => r.GetByUsernameOrEmailAsync("jane", It.IsAny<Expression<Func<User, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        this.userRepositoryMock
            .Setup(r => r.CheckPasswordAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await this.sut.LoginAsync(new LoginRequest { UserName = "jane", Password = "wrong", DeviceId = DeviceId }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task LoginAsync_NullRequest_ReturnsBadRequest()
    {
        var result = await this.sut.LoginAsync(null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RefreshTokenAsync_CsrfMismatch_Returns401()
    {
        this.sut.ControllerContext.HttpContext.Request.Headers[AppHeaderNames.CsrfToken] = "header-value";
        this.sut.ControllerContext.HttpContext.Request.Headers["Cookie"] = $"{CookieKeys.CsrfToken}=different-value";

        var result = await this.sut.RefreshTokenAsync(new RefreshTokenRequest { DeviceId = DeviceId }, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenAsync_MissingRefreshCookie_Returns401()
    {
        this.sut.ControllerContext.HttpContext.Request.Headers[AppHeaderNames.CsrfToken] = "csrf-value";
        this.sut.ControllerContext.HttpContext.Request.Headers["Cookie"] = $"{CookieKeys.CsrfToken}=csrf-value";

        var result = await this.sut.RefreshTokenAsync(new RefreshTokenRequest { DeviceId = DeviceId }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidCsrfAndCookieButIneligibleToken_Returns401()
    {
        this.sut.ControllerContext.HttpContext.Request.Headers[AppHeaderNames.CsrfToken] = "csrf-value";
        this.sut.ControllerContext.HttpContext.Request.Headers["Cookie"] = $"{CookieKeys.CsrfToken}=csrf-value; {CookieKeys.RefreshToken}=some-refresh-token";

        this.jwtTokenServiceMock
            .Setup(s => s.IsTokenEligibleForRefreshAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (User?)null));

        var result = await this.sut.RefreshTokenAsync(new RefreshTokenRequest { DeviceId = DeviceId }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task RefreshTokenAsync_NullRequest_ReturnsBadRequest()
    {
        var result = await this.sut.RefreshTokenAsync(null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task LogoutAsync_AuthenticatedUser_DeletesRefreshCsrfAndOAuthFlowCookies()
    {
        var user = BuildUser();
        this.sut.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "Test"));
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<Expression<Func<User, object>>[]>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await this.sut.LogoutAsync(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        this.authTokenCookieServiceMock.Verify(s => s.DeleteAuthCookies(), Times.Once);
        this.oauthFlowCookieServiceMock.Verify(s => s.DeleteOAuthFlowCookie(), Times.Once);
    }

    private static Mock<UserManager<User>> BuildUserManagerMock()
    {
        var store = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static User BuildUser() => new()
    {
        Id = 7,
        UserName = "jane",
        Email = "jane@example.com",
        RefreshTokens = []
    };
}
