using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coinwarden.API.Constants;
using Coinwarden.API.Models.Settings;
using Coinwarden.API.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Coinwarden.API.Services.Auth;

/// <summary>
/// Protects OAuth flow state in cookies and builds provider authorization/callback URLs.
/// </summary>
public sealed class OAuthFlowCookieService : IOAuthFlowCookieService
{
    private readonly AuthenticationSettings authSettings;

    private readonly IDataProtector oauthFlowProtector;

    private readonly IHttpContextAccessor httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthFlowCookieService"/> class.
    /// </summary>
    /// <param name="authSettings">OAuth client and cookie settings.</param>
    /// <param name="dataProtectionProvider">The data-protection provider for cookie payloads.</param>
    /// <param name="httpContextAccessor">Accessor for current request and response cookies.</param>
    public OAuthFlowCookieService(
        IOptions<AuthenticationSettings> authSettings,
        IDataProtectionProvider dataProtectionProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        this.authSettings = authSettings?.Value ?? throw new ArgumentNullException(nameof(authSettings));
        this.oauthFlowProtector = dataProtectionProvider.CreateProtector(OAuthConstants.DataProtectionPurpose);
        this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    public string BeginGitHubFlow()
    {
        return this.BeginOAuthFlow(
            OAuthConstants.GitHubProvider,
            OAuthConstants.GitHubAuthorizeUrl,
            this.authSettings.GitHubOAuthClientId,
            this.authSettings.GitHubOAuthRedirectUri,
            OAuthConstants.GitHubScope,
            additionalParameters: null);
    }

    /// <inheritdoc />
    public string BeginGoogleFlow()
    {
        return this.BeginOAuthFlow(
            OAuthConstants.GoogleProvider,
            OAuthConstants.GoogleAuthorizeUrl,
            this.authSettings.GoogleOAuthClientId,
            this.authSettings.GoogleOAuthRedirectUri,
            OAuthConstants.GoogleScope,
            additionalParameters:
            [
                ("response_type", "code"),
                ("access_type", "offline"),
                ("prompt", "consent")
            ]);
    }

    /// <inheritdoc />
    public string BuildCallbackRedirectUrl(string provider, string code, string state)
    {
        if (!this.TryReadOAuthFlowCookie(provider, out var flow) ||
            string.IsNullOrEmpty(state) ||
            !UtilityFunctions.FixedTimeEquals(state, flow.State))
        {
            return $"{this.authSettings.UIBaseUrl}#/auth/{provider}/callback?error=invalid_state";
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return $"{this.authSettings.UIBaseUrl}#/auth/{provider}/callback?error=missing_code";
        }

        return $"{this.authSettings.UIBaseUrl}#/auth/{provider}/callback?code={Uri.EscapeDataString(code)}";
    }

    /// <inheritdoc />
    public bool TryReadOAuthFlowCookie(string expectedProvider, out OAuthFlowState flow)
    {
        flow = default!;

        if (!this.Request.Cookies.TryGetValue(CookieKeys.OAuthFlow, out var protectedValue) || string.IsNullOrEmpty(protectedValue))
        {
            return false;
        }

        try
        {
            var payload = this.oauthFlowProtector.Unprotect(protectedValue);
            var parsed = JsonSerializer.Deserialize<OAuthFlowState>(payload);

            if (parsed == null ||
                string.IsNullOrEmpty(parsed.State) ||
                string.IsNullOrEmpty(parsed.CodeVerifier) ||
                !string.Equals(parsed.Provider, expectedProvider, StringComparison.Ordinal))
            {
                return false;
            }

            flow = parsed;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void DeleteOAuthFlowCookie()
    {
        this.Response.Cookies.Delete(CookieKeys.OAuthFlow, this.BuildOAuthFlowCookieOptions(includeExpiry: false));
    }

    private HttpRequest Request => this.httpContextAccessor.HttpContext?.Request
        ?? throw new InvalidOperationException("No current HTTP request.");

    private HttpResponse Response => this.httpContextAccessor.HttpContext?.Response
        ?? throw new InvalidOperationException("No current HTTP response.");

    private string BeginOAuthFlow(
        string provider,
        string authorizeUrl,
        string clientId,
        Uri redirectUri,
        string scope,
        (string Key, string Value)[]? additionalParameters)
    {
        var state = GenerateUrlToken(32);
        var codeVerifier = GenerateUrlToken(32);
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        var payload = JsonSerializer.Serialize(new OAuthFlowState
        {
            Provider = provider,
            State = state,
            CodeVerifier = codeVerifier
        });

        this.Response.Cookies.Append(CookieKeys.OAuthFlow, this.oauthFlowProtector.Protect(payload), this.BuildOAuthFlowCookieOptions(includeExpiry: true));

        var query = new StringBuilder();
        query.Append("client_id=").Append(Uri.EscapeDataString(clientId));
        query.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri.ToString()));
        query.Append("&scope=").Append(Uri.EscapeDataString(scope));
        query.Append("&state=").Append(Uri.EscapeDataString(state));
        query.Append("&code_challenge=").Append(Uri.EscapeDataString(codeChallenge));
        query.Append("&code_challenge_method=").Append(Uri.EscapeDataString(OAuthConstants.CodeChallengeMethod));

        if (additionalParameters != null)
        {
            foreach (var (key, value) in additionalParameters)
            {
                query.Append('&').Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
            }
        }

        return $"{authorizeUrl}?{query}";
    }

    private CookieOptions BuildOAuthFlowCookieOptions(bool includeExpiry)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            IsEssential = true,
            Domain = this.authSettings.CookieDomain,
            Path = "/"
        };

        if (includeExpiry)
        {
            options.MaxAge = TimeSpan.FromMinutes(10);
        }

        return options;
    }

    private static string GenerateUrlToken(int byteLength)
    {
        var bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return WebEncoders.Base64UrlEncode(hash);
    }
}
