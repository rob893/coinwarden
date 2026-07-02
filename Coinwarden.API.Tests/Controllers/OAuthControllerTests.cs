using System;
using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Constants;
using Coinwarden.API.Controllers.V1;
using Coinwarden.API.Core;
using Coinwarden.API.Models.Auth;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.GitHub;
using Coinwarden.API.Models.Requests.Auth;
using Coinwarden.API.Services.Auth;
using Coinwarden.API.Services.Core;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Coinwarden.API.Tests.Controllers;

/// <summary>
/// Tests for <see cref="OAuthController"/> (v1).
/// </summary>
public sealed class OAuthControllerTests
{
    private const string DeviceId = "device-1";

    private readonly Mock<IGitHubOAuthService> gitHubOAuthServiceMock;
    private readonly Mock<IGoogleOAuthService> googleOAuthServiceMock;
    private readonly Mock<IExternalLoginService> externalLoginServiceMock;
    private readonly Mock<IAuthTokenCookieService> authTokenCookieServiceMock;
    private readonly Mock<IOAuthFlowCookieService> oauthFlowCookieServiceMock;
    private readonly OAuthController sut;

    public OAuthControllerTests()
    {
        this.gitHubOAuthServiceMock = new Mock<IGitHubOAuthService>();
        this.googleOAuthServiceMock = new Mock<IGoogleOAuthService>();
        this.externalLoginServiceMock = new Mock<IExternalLoginService>();
        this.authTokenCookieServiceMock = new Mock<IAuthTokenCookieService>();
        this.oauthFlowCookieServiceMock = new Mock<IOAuthFlowCookieService>();
        var correlationIdServiceMock = new Mock<ICorrelationIdService>();
        correlationIdServiceMock.Setup(s => s.CorrelationId).Returns("corr-id");

        this.authTokenCookieServiceMock
            .Setup(s => s.GenerateAndSaveAccessAndRefreshTokensAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync("access-token");

        this.sut = new OAuthController(
            this.gitHubOAuthServiceMock.Object,
            this.googleOAuthServiceMock.Object,
            this.externalLoginServiceMock.Object,
            this.authTokenCookieServiceMock.Object,
            this.oauthFlowCookieServiceMock.Object,
            correlationIdServiceMock.Object,
            NullLogger<OAuthController>.Instance);

        this.sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public void GitHubStart_RedirectsToGitHubAuthorizeUrl()
    {
        this.oauthFlowCookieServiceMock.Setup(s => s.BeginGitHubFlow()).Returns("https://github.com/login/oauth/authorize?state=state");

        var result = this.sut.GitHubStart();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith(OAuthConstants.GitHubAuthorizeUrl, redirect.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void GoogleStart_RedirectsToGoogleAuthorizeUrl()
    {
        this.oauthFlowCookieServiceMock.Setup(s => s.BeginGoogleFlow()).Returns("https://accounts.google.com/o/oauth2/v2/auth?state=state");

        var result = this.sut.GoogleStart();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith(OAuthConstants.GoogleAuthorizeUrl, redirect.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void GitHubCallback_RedirectsToFlowServiceResult()
    {
        this.oauthFlowCookieServiceMock
            .Setup(s => s.BuildCallbackRedirectUrl(OAuthConstants.GitHubProvider, "the-code", "state"))
            .Returns("http://localhost:5173#/auth/github/callback?code=the-code");

        var result = this.sut.GitHubCallback("the-code", "state");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("http://localhost:5173#/auth/github/callback?code=the-code", redirect.Url);
    }

    [Fact]
    public async Task LoginGitHubAsync_MissingOAuthFlowCookie_ReturnsUnauthorizedAndClearsFlowCookie()
    {
        var flow = default(OAuthFlowState)!;
        this.oauthFlowCookieServiceMock
            .Setup(s => s.TryReadOAuthFlowCookie(OAuthConstants.GitHubProvider, out flow))
            .Returns(false);

        var result = await this.sut.LoginGitHubAsync(new OAuthCodeLoginRequest { Code = "the-code", DeviceId = DeviceId }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        this.oauthFlowCookieServiceMock.Verify(s => s.DeleteOAuthFlowCookie(), Times.Once);
    }

    [Fact]
    public async Task LoginGoogleAsync_MissingOAuthFlowCookie_ReturnsUnauthorizedAndClearsFlowCookie()
    {
        var flow = default(OAuthFlowState)!;
        this.oauthFlowCookieServiceMock
            .Setup(s => s.TryReadOAuthFlowCookie(OAuthConstants.GoogleProvider, out flow))
            .Returns(false);

        var result = await this.sut.LoginGoogleAsync(new OAuthCodeLoginRequest { Code = "the-code", DeviceId = DeviceId }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        this.oauthFlowCookieServiceMock.Verify(s => s.DeleteOAuthFlowCookie(), Times.Once);
    }

    [Fact]
    public async Task LoginGitHubAsync_ValidFlow_IssuesTokenAndClearsOAuthFlowCookie()
    {
        var flow = new OAuthFlowState { Provider = OAuthConstants.GitHubProvider, State = "state", CodeVerifier = "verifier" };
        var user = BuildUser();
        this.oauthFlowCookieServiceMock
            .Setup(s => s.TryReadOAuthFlowCookie(OAuthConstants.GitHubProvider, out flow))
            .Returns(true);
        this.gitHubOAuthServiceMock
            .Setup(s => s.ExchangeCodeForGithubAccessTokenAsync("the-code", "verifier", It.IsAny<CancellationToken>()))
            .ReturnsAsync("github-token");
        this.gitHubOAuthServiceMock
            .Setup(s => s.GetGitHubUserAsync("github-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubUser { Id = 123, Login = "jane", Email = "jane@example.com" });
        this.externalLoginServiceMock
            .Setup(s => s.ResolveOrProvisionUserAsync(It.IsAny<ExternalLoginIdentity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Success(user));

        var result = await this.sut.LoginGitHubAsync(new OAuthCodeLoginRequest { Code = "the-code", DeviceId = DeviceId }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        this.authTokenCookieServiceMock.Verify(
            s => s.GenerateAndSaveAccessAndRefreshTokensAsync(user, DeviceId, It.IsAny<CancellationToken>(), true),
            Times.Once);
        this.oauthFlowCookieServiceMock.Verify(s => s.DeleteOAuthFlowCookie(), Times.Once);
    }

    [Fact]
    public async Task LoginGoogleAsync_ValidFlow_IssuesTokenAndClearsOAuthFlowCookie()
    {
        var flow = new OAuthFlowState { Provider = OAuthConstants.GoogleProvider, State = "state", CodeVerifier = "verifier" };
        var user = BuildUser();
        this.oauthFlowCookieServiceMock
            .Setup(s => s.TryReadOAuthFlowCookie(OAuthConstants.GoogleProvider, out flow))
            .Returns(true);
        this.googleOAuthServiceMock
            .Setup(s => s.ExchangeCodeForGoogleIdTokenAsync("the-code", "verifier", It.IsAny<CancellationToken>()))
            .ReturnsAsync("id-token");
        this.googleOAuthServiceMock
            .Setup(s => s.ValidateIdTokenAsync("id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleJsonWebSignature.Payload { Subject = "sub", Email = "jane@example.com", EmailVerified = true });
        this.externalLoginServiceMock
            .Setup(s => s.ResolveOrProvisionUserAsync(It.IsAny<ExternalLoginIdentity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<User>.Success(user));

        var result = await this.sut.LoginGoogleAsync(new OAuthCodeLoginRequest { Code = "the-code", DeviceId = DeviceId }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        this.authTokenCookieServiceMock.Verify(
            s => s.GenerateAndSaveAccessAndRefreshTokensAsync(user, DeviceId, It.IsAny<CancellationToken>(), true),
            Times.Once);
        this.oauthFlowCookieServiceMock.Verify(s => s.DeleteOAuthFlowCookie(), Times.Once);
    }

    private static User BuildUser() => new()
    {
        Id = 7,
        UserName = "jane",
        Email = "jane@example.com",
        RefreshTokens = []
    };
}
