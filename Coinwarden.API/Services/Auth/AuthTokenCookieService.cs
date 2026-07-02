using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Constants;
using Coinwarden.API.Extensions;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Coinwarden.API.Services.Auth;

/// <summary>
/// Issues access/refresh tokens and writes the cookies required by browser-based refresh.
/// </summary>
public sealed class AuthTokenCookieService : IAuthTokenCookieService
{
    private readonly IJwtTokenService jwtTokenService;

    private readonly AuthenticationSettings authSettings;

    private readonly IHttpContextAccessor httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthTokenCookieService"/> class.
    /// </summary>
    /// <param name="jwtTokenService">The JWT and refresh-token service.</param>
    /// <param name="authSettings">Authentication cookie and expiration settings.</param>
    /// <param name="httpContextAccessor">Accessor for the current HTTP response.</param>
    public AuthTokenCookieService(
        IJwtTokenService jwtTokenService,
        IOptions<AuthenticationSettings> authSettings,
        IHttpContextAccessor httpContextAccessor)
    {
        this.jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        this.authSettings = authSettings?.Value ?? throw new ArgumentNullException(nameof(authSettings));
        this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    public async Task<string> GenerateAndSaveAccessAndRefreshTokensAsync(User user, string deviceId, CancellationToken cancellationToken, bool updateLastLogin = true)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        if (updateLastLogin)
        {
            user.LastLogin = DateTimeOffset.UtcNow;
        }

        var token = this.jwtTokenService.GenerateJwtTokenForUser(user);
        var refreshToken = await this.jwtTokenService.GenerateAndSaveRefreshTokenForUserAsync(user, deviceId, cancellationToken);

        this.Response.Cookies.Append(CookieKeys.RefreshToken, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddMinutes(this.authSettings.RefreshTokenExpirationTimeInMinutes),
            IsEssential = true,
            Domain = this.authSettings.CookieDomain
        });

        // Generate and set CSRF token cookie for the Double Submit Cookie pattern.
        // Use a CSPRNG (32 random bytes, base64url encoded) rather than Guid to ensure unpredictability.
        var csrfTokenBytes = new byte[32];
        RandomNumberGenerator.Fill(csrfTokenBytes);
        var csrfToken = Convert.ToBase64String(csrfTokenBytes).ConvertToBase64UrlEncodedString();
        this.Response.Cookies.Append(CookieKeys.CsrfToken, csrfToken, new CookieOptions
        {
            HttpOnly = false, // Must be false so JavaScript can read it
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddMinutes(this.authSettings.RefreshTokenExpirationTimeInMinutes),
            IsEssential = true,
            Domain = this.authSettings.CookieDomain
        });

        return token;
    }

    /// <inheritdoc />
    public void DeleteAuthCookies()
    {
        this.Response.Cookies.Delete(CookieKeys.RefreshToken, this.BuildAuthCookieOptions(httpOnly: true));
        this.Response.Cookies.Delete(CookieKeys.CsrfToken, this.BuildAuthCookieOptions(httpOnly: false));
    }

    private HttpResponse Response => this.httpContextAccessor.HttpContext?.Response
        ?? throw new InvalidOperationException("No current HTTP response.");

    private CookieOptions BuildAuthCookieOptions(bool httpOnly) => new()
    {
        HttpOnly = httpOnly,
        Secure = true,
        SameSite = SameSiteMode.None,
        IsEssential = true,
        Domain = this.authSettings.CookieDomain
    };
}
