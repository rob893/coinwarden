using System;
using System.Linq;
using Coinwarden.API.Constants;
using Coinwarden.API.Models.Settings;
using Coinwarden.API.Services.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Coinwarden.API.Tests.Services;

/// <summary>
/// Tests for <see cref="OAuthFlowCookieService"/>.
/// </summary>
public sealed class OAuthFlowCookieServiceTests
{
    private readonly DefaultHttpContext httpContext;
    private readonly OAuthFlowCookieService sut;

    public OAuthFlowCookieServiceTests()
    {
        this.httpContext = new DefaultHttpContext();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        this.sut = new OAuthFlowCookieService(
            Options.Create(new AuthenticationSettings
            {
                UIBaseUrl = new Uri("http://localhost:5173"),
                GitHubOAuthClientId = "github-client-id",
                GitHubOAuthRedirectUri = new Uri("https://localhost:7234/api/v1/auth/github/callback"),
                GoogleOAuthClientId = "google-client-id",
                GoogleOAuthRedirectUri = new Uri("https://localhost:7234/api/v1/auth/google/callback"),
                CookieDomain = "example.com"
            }),
            dataProtectionProvider,
            new HttpContextAccessor { HttpContext = this.httpContext });
    }

    [Fact]
    public void BeginGitHubFlow_SetsProtectedCookieAndBuildsAuthorizeUrl()
    {
        var url = this.sut.BeginGitHubFlow();

        Assert.StartsWith(OAuthConstants.GitHubAuthorizeUrl, url, StringComparison.Ordinal);
        Assert.Contains("client_id=github-client-id", url, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", url, StringComparison.Ordinal);
        Assert.Contains("state=", url, StringComparison.Ordinal);
        Assert.Contains($"{CookieKeys.OAuthFlow}=", this.httpContext.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCallbackRedirectUrl_WithValidState_ReturnsUiCodeRedirect()
    {
        var url = this.sut.BeginGoogleFlow();
        var state = new Uri(url).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Single(part => part.StartsWith("state=", StringComparison.Ordinal))
            .Split('=')[1];
        this.httpContext.Request.Headers.Cookie = this.httpContext.Response.Headers.SetCookie.ToString().Split(';')[0];

        var redirectUrl = this.sut.BuildCallbackRedirectUrl(OAuthConstants.GoogleProvider, "the-code", Uri.UnescapeDataString(state));

        Assert.Equal("http://localhost:5173/#/auth/google/callback?code=the-code", redirectUrl);
    }
}
