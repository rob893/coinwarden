using System;
using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Constants;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.Settings;
using Coinwarden.API.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;

namespace Coinwarden.API.Tests.Services;

/// <summary>
/// Tests for <see cref="AuthTokenCookieService"/>.
/// </summary>
public sealed class AuthTokenCookieServiceTests
{
    private const string DeviceId = "device-123";
    private readonly DefaultHttpContext httpContext;
    private readonly Mock<IJwtTokenService> jwtTokenServiceMock;

    public AuthTokenCookieServiceTests()
    {
        this.httpContext = new DefaultHttpContext();
        var httpContextAccessor = new HttpContextAccessor { HttpContext = this.httpContext };
        this.jwtTokenServiceMock = new Mock<IJwtTokenService>();
        this.jwtTokenServiceMock
            .Setup(s => s.GenerateJwtTokenForUser(It.IsAny<User>()))
            .Returns("access-token");
        this.jwtTokenServiceMock
            .Setup(s => s.GenerateAndSaveRefreshTokenForUserAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");

        this.Sut = new AuthTokenCookieService(
            this.jwtTokenServiceMock.Object,
            Options.Create(new AuthenticationSettings
            {
                RefreshTokenExpirationTimeInMinutes = 60,
                CookieDomain = "example.com"
            }),
            httpContextAccessor);
    }

    private AuthTokenCookieService Sut { get; }

    [Fact]
    public async Task GenerateAndSaveAccessAndRefreshTokensAsync_IssuesAccessTokenAndAuthCookies()
    {
        var user = BuildUser();

        var token = await this.Sut.GenerateAndSaveAccessAndRefreshTokensAsync(user, DeviceId, CancellationToken.None);

        Assert.Equal("access-token", token);
        Assert.True(user.LastLogin > DateTimeOffset.UtcNow.AddMinutes(-1));
        var setCookie = this.httpContext.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{CookieKeys.RefreshToken}=refresh-token", setCookie, StringComparison.Ordinal);
        Assert.Contains($"{CookieKeys.CsrfToken}=", setCookie, StringComparison.Ordinal);
        Assert.Contains("samesite=none", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("domain=example.com", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAndSaveAccessAndRefreshTokensAsync_WhenSkippingLastLogin_DoesNotUpdateLastLogin()
    {
        var lastLogin = DateTimeOffset.UtcNow.AddDays(-7);
        var user = BuildUser();
        user.LastLogin = lastLogin;

        await this.Sut.GenerateAndSaveAccessAndRefreshTokensAsync(user, DeviceId, CancellationToken.None, updateLastLogin: false);

        Assert.Equal(lastLogin, user.LastLogin);
    }

    [Fact]
    public void DeleteAuthCookies_DeletesRefreshAndCsrfCookiesWithCrossOriginOptions()
    {
        this.Sut.DeleteAuthCookies();

        var setCookie = this.httpContext.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{CookieKeys.RefreshToken}=", setCookie, StringComparison.Ordinal);
        Assert.Contains($"{CookieKeys.CsrfToken}=", setCookie, StringComparison.Ordinal);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=none", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("domain=example.com", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    private static User BuildUser() => new()
    {
        Id = 42,
        UserName = "jane.doe",
        Email = "jane.doe@example.com",
        RefreshTokens = []
    };
}
