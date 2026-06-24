using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Coinwarden.API.Constants;
using Coinwarden.API.Data.Repositories;
using Coinwarden.API.Extensions;
using Coinwarden.API.Models.Auth;
using Coinwarden.API.Models.Dtos;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.Requests.Auth;
using Coinwarden.API.Models.Responses.Auth;
using Coinwarden.API.Models.Settings;
using Coinwarden.API.Services.Auth;
using Coinwarden.API.Services.Core;
using Coinwarden.API.Services.Domain;
using static Coinwarden.API.Utilities.UtilityFunctions;

namespace Coinwarden.API.Controllers.V1;

[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1")]
[ApiController]
public sealed class AuthController : ServiceControllerBase
{
    /// <summary>
    /// A precomputed valid Identity password hash used solely to equalize timing on the
    /// "user not found" login path, preventing username/email enumeration via timing analysis.
    /// The default <see cref="PasswordHasher{TUser}"/> ignores the user argument during verification,
    /// so the value only needs to be a valid hash that burns comparable PBKDF2 work.
    /// </summary>
    private static readonly string dummyPasswordHash =
        new PasswordHasher<User>().HashPassword(new User(), "Timing-Equalization-Dummy-Password-1!");

    /// <summary>
    /// Identity error codes that confirm account existence. These are collapsed into a generic
    /// message to avoid username/email enumeration during registration.
    /// </summary>
    private static readonly string[] dnumerationErrorCodes =
    [
        nameof(IdentityErrorDescriber.DuplicateUserName),
        nameof(IdentityErrorDescriber.DuplicateEmail)
    ];

    /// <summary>
    /// Generic registration failure message returned for enumeration-revealing errors so that
    /// neither the username nor email field existence is disclosed.
    /// </summary>
    private static readonly string[] genericRegistrationFailure = ["Registration could not be completed."];

    private readonly IUserRepository userRepository;

    private readonly IJwtTokenService jwtTokenService;

    private readonly IUserService userService;

    private readonly IGitHubOAuthService gitHubOAuthService;

    private readonly IGoogleOAuthService googleOAuthService;

    private readonly IExternalLoginService externalLoginService;

    private readonly AuthenticationSettings authSettings;

    private readonly IDataProtector oauthFlowProtector;

    private readonly ILogger<AuthController> logger;

    public AuthController(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IUserService userService,
        IGitHubOAuthService gitHubOAuthService,
        IGoogleOAuthService googleOAuthService,
        IExternalLoginService externalLoginService,
        IOptions<AuthenticationSettings> authSettings,
        IDataProtectionProvider dataProtectionProvider,
        ICorrelationIdService correlationIdService,
        ILogger<AuthController> logger)
            : base(correlationIdService)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        this.jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
        this.gitHubOAuthService = gitHubOAuthService ?? throw new ArgumentNullException(nameof(gitHubOAuthService));
        this.googleOAuthService = googleOAuthService ?? throw new ArgumentNullException(nameof(googleOAuthService));
        this.externalLoginService = externalLoginService ?? throw new ArgumentNullException(nameof(externalLoginService));
        this.authSettings = authSettings?.Value ?? throw new ArgumentNullException(nameof(authSettings));
        this.oauthFlowProtector = dataProtectionProvider.CreateProtector(OAuthConstants.DataProtectionPurpose);
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers a new user.
    /// </summary>
    /// <param name="registerUserRequest">The register request object.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The user object and tokens.</returns>
    /// <response code="201">The user object and tokens.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="500">If an unexpected server error occurred.</response>
    /// <response code="504">If the server took too long to respond.</response>
    [AllowAnonymous]
    [HttpPost("register", Name = nameof(RegisterAsync))]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<LoginResponse>> RegisterAsync([FromBody] RegisterUserRequest registerUserRequest, CancellationToken cancellationToken)
    {
        if (registerUserRequest == null)
        {
            return this.BadRequest();
        }

        var user = registerUserRequest.ToEntity();

        var result = await this.userRepository.CreateUserWithPasswordAsync(user, registerUserRequest.Password, cancellationToken);

        if (!result.Succeeded)
        {
            // Deliberate UX/security tradeoff: when the failure confirms an existing account
            // (duplicate username/email), return a generic message that does not disclose which
            // field exists, preventing account enumeration. Non-enumerating failures (e.g. password
            // policy) keep their descriptions so users still receive actionable validation feedback.
            if (result.Errors.Any(error => dnumerationErrorCodes.Contains(error.Code, StringComparer.Ordinal)))
            {
                return this.BadRequest(genericRegistrationFailure);
            }

            return this.BadRequest([.. result.Errors.Select(e => e.Description)]);
        }

        var token = await this.GenerateAndSaveAccessAndRefreshTokensAsync(user, registerUserRequest.DeviceId, cancellationToken);

        await this.userService.SendEmailConfirmationAsync(user, cancellationToken);

        var userToReturn = UserDto.FromEntity(user);

        return this.CreatedAtRoute(
            nameof(UsersController.GetUserAsync),
            new
            {
                controller = GetControllerName<UsersController>(),
                id = user.Id
            },
            new LoginResponse
            {
                Token = token,
                User = userToReturn
            });
    }

    /// <summary>
    /// Logs the user in.
    /// </summary>
    /// <param name="loginRequest">The login request object.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The user object and tokens.</returns>
    /// <response code="200">The user object and tokens.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If provided login information is invalid.</response>
    /// <response code="500">If an unexpected server error occurred.</response>
    /// <response code="504">If the server took too long to respond.</response>
    [AllowAnonymous]
    [HttpPost("login", Name = nameof(LoginAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> LoginAsync([FromBody] LoginRequest loginRequest, CancellationToken cancellationToken)
    {
        if (loginRequest == null)
        {
            return this.BadRequest();
        }

        var user = await this.userRepository.GetByUsernameOrEmailAsync(loginRequest.UserName, [user => user.RefreshTokens], cancellationToken);

        if (user == null)
        {
            // Perform a dummy password verification to equalize timing with the found-user path,
            // preventing username/email enumeration via login timing analysis. The result is ignored.
            this.userRepository.UserManager.PasswordHasher.VerifyHashedPassword(new User(), dummyPasswordHash, loginRequest.Password);

            return this.Unauthorized("Invalid username or password.");
        }

        var result = await this.userRepository.CheckPasswordAsync(user, loginRequest.Password, cancellationToken);

        if (!result)
        {
            return this.Unauthorized("Invalid username or password.");
        }

        var token = await this.GenerateAndSaveAccessAndRefreshTokensAsync(user, loginRequest.DeviceId, cancellationToken);

        var userToReturn = UserDto.FromEntity(user);

        return this.Ok(
            new LoginResponse
            {
                Token = token,
                User = userToReturn
            });
    }

    /// <summary>
    /// Logs the user in using Google OAuth authorization code (similar to GitHub flow).
    /// </summary>
    /// <param name="loginRequest">The login request object.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The user object and tokens.</returns>
    /// <response code="200">If the user was logged in.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If provided login information is invalid.</response>
    /// <response code="500">If an unexpected server error occurred.</response>
    /// <response code="504">If the server took too long to respond.</response>
    [AllowAnonymous]
    [HttpPost("login/google", Name = nameof(LoginGoogleAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> LoginGoogleAsync([FromBody] OAuthCodeLoginRequest loginRequest, CancellationToken cancellationToken)
    {
        try
        {
            if (loginRequest == null)
            {
                return this.BadRequest("Request body cannot be null.");
            }

            if (!this.TryReadOAuthFlowCookie(OAuthConstants.GoogleProvider, out var flow))
            {
                return this.Unauthorized("Invalid or missing OAuth flow. Please restart the login process.");
            }

            var idToken = await this.googleOAuthService.ExchangeCodeForGoogleIdTokenAsync(loginRequest.Code, flow.CodeVerifier, cancellationToken);

            if (string.IsNullOrWhiteSpace(idToken))
            {
                return this.Unauthorized("Invalid Google authorization code.");
            }

            var validatedToken = await this.googleOAuthService.ValidateIdTokenAsync(idToken, cancellationToken);

            var identity = new ExternalLoginIdentity
            {
                ProviderSubjectId = validatedToken.Subject,
                ProviderType = LinkedAccountType.Google,
                Email = validatedToken.Email,
                EmailVerified = validatedToken.EmailVerified,
                SuggestedUserName = validatedToken.Email?.Split('@').FirstOrDefault() ?? validatedToken.Subject
            };

            var result = await this.externalLoginService.ResolveOrProvisionUserAsync(identity, cancellationToken);

            if (!result.IsSuccess)
            {
                return this.HandleServiceFailureResult(result);
            }

            var user = result.ValueOrThrow;

            var token = await this.GenerateAndSaveAccessAndRefreshTokensAsync(user, loginRequest.DeviceId, cancellationToken);
            var userToReturn = UserDto.FromEntity(user);

            return this.Ok(
                new LoginResponse
                {
                    Token = token,
                    User = userToReturn
                });
        }
        catch (HttpRequestException)
        {
            return this.Unauthorized("Unable to validate Google authorization code.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogError(ex, "Unexpected error during Google login.");
            return this.InternalServerError("Unable to login with Google.");
        }
        finally
        {
            this.DeleteOAuthFlowCookie();
        }
    }

    /// <summary>
    /// Logs the user in using GitHub OAuth credentials.
    /// </summary>
    /// <param name="loginRequest">The login request object.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The user object and tokens.</returns>
    /// <response code="200">If the user was logged in.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If provided login information is invalid.</response>
    /// <response code="500">If an unexpected server error occurred.</response>
    /// <response code="504">If the server took too long to respond.</response>
    [AllowAnonymous]
    [HttpPost("login/github", Name = nameof(LoginGitHubAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> LoginGitHubAsync([FromBody] OAuthCodeLoginRequest loginRequest, CancellationToken cancellationToken)
    {
        try
        {
            if (loginRequest == null)
            {
                return this.BadRequest("Request body cannot be null.");
            }

            if (!this.TryReadOAuthFlowCookie(OAuthConstants.GitHubProvider, out var flow))
            {
                return this.Unauthorized("Invalid or missing OAuth flow. Please restart the login process.");
            }

            var githubToken = await this.gitHubOAuthService.ExchangeCodeForGithubAccessTokenAsync(loginRequest.Code, flow.CodeVerifier, cancellationToken);

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                return this.Unauthorized("Invalid GitHub code.");
            }

            var gitHubUserInfo = await this.gitHubOAuthService.GetGitHubUserAsync(githubToken, cancellationToken);

            if (gitHubUserInfo == null || string.IsNullOrWhiteSpace(gitHubUserInfo.Login))
            {
                return this.Unauthorized("Unable to retrieve GitHub user information.");
            }

            var gitHubEmail = string.IsNullOrWhiteSpace(gitHubUserInfo.Email)
                ? (await this.gitHubOAuthService.GetGitHubEmailsAsync(githubToken, cancellationToken)).FirstOrDefault(e => e.Primary && e.Verified)?.Email
                : gitHubUserInfo.Email;

            var identity = new ExternalLoginIdentity
            {
                ProviderSubjectId = gitHubUserInfo.Id.ToString(CultureInfo.InvariantCulture),
                ProviderType = LinkedAccountType.GitHub,
                Email = gitHubEmail,
                EmailVerified = !string.IsNullOrEmpty(gitHubEmail),
                SuggestedUserName = gitHubUserInfo.Login
            };

            var result = await this.externalLoginService.ResolveOrProvisionUserAsync(identity, cancellationToken);

            if (!result.IsSuccess)
            {
                return this.HandleServiceFailureResult(result);
            }

            var user = result.ValueOrThrow;

            var token = await this.GenerateAndSaveAccessAndRefreshTokensAsync(user, loginRequest.DeviceId, cancellationToken);
            var userToReturn = UserDto.FromEntity(user);

            return this.Ok(
                new LoginResponse
                {
                    Token = token,
                    User = userToReturn
                });
        }
        catch (HttpRequestException)
        {
            return this.Unauthorized("Unable to validate GitHub token.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogError(ex, "Unexpected error during GitHub login.");
            return this.InternalServerError("Unable to login with GitHub.");
        }
        finally
        {
            this.DeleteOAuthFlowCookie();
        }
    }

    /// <summary>
    /// Begins the backend-initiated GitHub OAuth flow by issuing CSRF state + PKCE, persisting them in
    /// a tamper-proof cookie, and redirecting the browser to GitHub's authorize endpoint.
    /// </summary>
    /// <returns>A redirect to GitHub's authorize endpoint.</returns>
    /// <response code="302">Redirects to the GitHub authorize endpoint.</response>
    [AllowAnonymous]
    [HttpGet("github/start", Name = nameof(GitHubStart))]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GitHubStart()
    {
        var authorizeUrl = this.BeginOAuthFlow(
            OAuthConstants.GitHubProvider,
            OAuthConstants.GitHubAuthorizeUrl,
            this.authSettings.GitHubOAuthClientId,
            this.authSettings.GitHubOAuthRedirectUri,
            OAuthConstants.GitHubScope,
            additionalParameters: null);

        return this.Redirect(authorizeUrl);
    }

    /// <summary>
    /// Begins the backend-initiated Google OAuth flow by issuing CSRF state + PKCE, persisting them in
    /// a tamper-proof cookie, and redirecting the browser to Google's authorize endpoint.
    /// </summary>
    /// <returns>A redirect to Google's authorize endpoint.</returns>
    /// <response code="302">Redirects to the Google authorize endpoint.</response>
    [AllowAnonymous]
    [HttpGet("google/start", Name = nameof(GoogleStart))]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GoogleStart()
    {
        var authorizeUrl = this.BeginOAuthFlow(
            OAuthConstants.GoogleProvider,
            OAuthConstants.GoogleAuthorizeUrl,
            this.authSettings.GoogleOAuthClientId,
            this.authSettings.GoogleOAuthRedirectUri,
            OAuthConstants.GoogleScope,
            additionalParameters: new[]
            {
                ("response_type", "code"),
                ("access_type", "offline"),
                ("prompt", "consent")
            });

        return this.Redirect(authorizeUrl);
    }

    /// <summary>
    /// Handles the GitHub OAuth callback by validating the cookie-bound state and redirecting to the
    /// UI with the authorization code.
    /// </summary>
    /// <param name="code">The authorization code from GitHub.</param>
    /// <param name="state">The state parameter, validated against the cookie-bound flow state.</param>
    /// <returns>A redirect to the UI with the authorization code or an error.</returns>
    /// <response code="302">Redirects to the UI with the authorization code or error.</response>
    [AllowAnonymous]
    [HttpGet("github/callback", Name = nameof(GitHubCallback))]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GitHubCallback([FromQuery] string code, [FromQuery] string state)
    {
        return this.HandleOAuthCallback(OAuthConstants.GitHubProvider, code, state);
    }

    /// <summary>
    /// Handles the Google OAuth callback by validating the cookie-bound state and redirecting to the
    /// UI with the authorization code.
    /// </summary>
    /// <param name="code">The authorization code from Google.</param>
    /// <param name="state">The state parameter, validated against the cookie-bound flow state.</param>
    /// <returns>A redirect to the UI with the authorization code or an error.</returns>
    /// <response code="302">Redirects to the UI with the authorization code or error.</response>
    [AllowAnonymous]
    [HttpGet("google/callback", Name = nameof(GoogleCallback))]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GoogleCallback([FromQuery] string code, [FromQuery] string state)
    {
        return this.HandleOAuthCallback(OAuthConstants.GoogleProvider, code, state);
    }

    /// <summary>
    /// Logs the user out by revoking all refresh tokens and clearing the refresh token cookie.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>No content.</returns>
    /// <response code="204">No content.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If provided login information is invalid.</response>
    /// <response code="500">If an unexpected server error occurred.</response>
    /// <response code="504">If the server took too long to respond.</response>
    [HttpPost("logout", Name = nameof(LogoutAsync))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        if (!this.User.TryGetUserId(out var userId) || userId == null)
        {
            return this.Unauthorized("You must be logged in to log out.");
        }

        var user = await this.userRepository.GetByIdAsync(userId.Value, [u => u.RefreshTokens], track: true, cancellationToken);

        if (user == null)
        {
            return this.NotFound("User not found.");
        }

        await this.jwtTokenService.RevokeAllRefreshTokensForUserAsync(user, cancellationToken);
        this.Response.Cookies.Delete(CookieKeys.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            IsEssential = true,
            Domain = this.authSettings.CookieDomain
        });

        return this.NoContent();
    }

    /// <summary>
    /// Refreshes a user's access token. Refresh token is stored in a cookie.
    /// </summary>
    /// <param name="refreshTokenRequest">The refresh token request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A new set of tokens.</returns>
    /// <response code="200">If the token was refreshed.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If provided token pair was invalid.</response>
    /// <response code="500">If an unexpected server error occurred.</response>
    /// <response code="504">If the server took too long to respond.</response>
    [AllowAnonymous]
    [HttpPost("refreshToken", Name = nameof(RefreshTokenAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequest refreshTokenRequest, CancellationToken cancellationToken)
    {
        if (refreshTokenRequest == null)
        {
            return this.BadRequest();
        }

        // CSRF protection using Double Submit Cookie pattern. This is the only endpoint that requires CSRF token as it is the only one that uses cookies.
        // This isn't really needed as the device id is effectively a CSRF token, but it's a good practice to have it.
        var csrfHeader = this.Request.Headers[AppHeaderNames.CsrfToken].ToString();
        var csrfCookie = this.Request.Cookies[CookieKeys.CsrfToken];

        if (string.IsNullOrEmpty(csrfHeader) || !FixedTimeEquals(csrfHeader, csrfCookie))
        {
            return this.Unauthorized("CSRF token mismatch.");
        }

        if (!this.Request.Cookies.TryGetValue(CookieKeys.RefreshToken, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            return this.Unauthorized("Missing refresh token cookie");
        }

        var (isTokenEligibleForRefresh, user) = await this.jwtTokenService.IsTokenEligibleForRefreshAsync(
            refreshToken,
            refreshTokenRequest.DeviceId,
            cancellationToken);

        if (!isTokenEligibleForRefresh || user == null)
        {
            return this.Unauthorized("Invalid token.");
        }

        var token = await this.GenerateAndSaveAccessAndRefreshTokensAsync(user, refreshTokenRequest.DeviceId, cancellationToken, updateLastLogin: false);

        return this.Ok(
            new RefreshTokenResponse
            {
                Token = token
            });
    }



    private async Task<string> GenerateAndSaveAccessAndRefreshTokensAsync(User user, string deviceId, CancellationToken cancellationToken, bool updateLastLogin = true)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        if (updateLastLogin)
        {
            user.LastLogin = DateTimeOffset.UtcNow;
        }

        var token = this.jwtTokenService.GenerateJwtTokenForUser(user);
        var refreshToken = await this.jwtTokenService.GenerateAndSaveRefreshTokenForUserAsync(user, deviceId, cancellationToken);

        // Set the refresh token cookie
        this.Response.Cookies.Append(CookieKeys.RefreshToken, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Always require HTTPS
            SameSite = SameSiteMode.None, // Required for cross-origin requests
            Expires = DateTimeOffset.UtcNow.AddMinutes(this.authSettings.RefreshTokenExpirationTimeInMinutes),
            IsEssential = true, // Mark as essential for GDPR compliance
            Domain = this.authSettings.CookieDomain // Don't set domain to restrict to exact host
        });

        // Generate and set CSRF token cookie for Double Submit Cookie pattern.
        // Use a CSPRNG (32 random bytes, base64url encoded) rather than Guid to ensure unpredictability.
        var csrfTokenBytes = new byte[32];
        RandomNumberGenerator.Fill(csrfTokenBytes);
        var csrfToken = Convert.ToBase64String(csrfTokenBytes).ConvertToBase64UrlEncodedString();
        this.Response.Cookies.Append(CookieKeys.CsrfToken, csrfToken, new CookieOptions
        {
            HttpOnly = false, // Must be false so JavaScript can read it
            Secure = true, // Always require HTTPS
            SameSite = SameSiteMode.None, // Required for cross-origin requests
            Expires = DateTimeOffset.UtcNow.AddMinutes(this.authSettings.RefreshTokenExpirationTimeInMinutes),
            IsEssential = true, // Mark as essential for GDPR compliance
            Domain = this.authSettings.CookieDomain // Set domain to allow cross-origin requests
        });

        return token;
    }

    /// <summary>
    /// Begins a backend-initiated OAuth flow: generates a CSPRNG state and PKCE verifier/challenge,
    /// persists <c>{ provider, state, codeVerifier }</c> in the tamper-proof <c>oauth_flow</c> cookie,
    /// and returns the provider authorize URL the browser should be redirected to.
    /// </summary>
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

    /// <summary>
    /// Validates the cookie-bound OAuth state for a callback and returns the UI redirect URL.
    /// </summary>
    private RedirectResult HandleOAuthCallback(string provider, string code, string state)
    {
        if (!this.TryReadOAuthFlowCookie(provider, out var flow) ||
            string.IsNullOrEmpty(state) ||
            !FixedTimeEquals(state, flow.State))
        {
            return this.Redirect($"{this.authSettings.UIBaseUrl}#/auth/{provider}/callback?error=invalid_state");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return this.Redirect($"{this.authSettings.UIBaseUrl}#/auth/{provider}/callback?error=missing_code");
        }

        return this.Redirect($"{this.authSettings.UIBaseUrl}#/auth/{provider}/callback?code={Uri.EscapeDataString(code)}");
    }

    /// <summary>
    /// Attempts to read and unprotect the <c>oauth_flow</c> cookie and confirm it matches the expected provider.
    /// </summary>
    private bool TryReadOAuthFlowCookie(string expectedProvider, out OAuthFlowState flow)
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

    /// <summary>
    /// Deletes the <c>oauth_flow</c> cookie using the same options it was set with so the browser clears it.
    /// </summary>
    private void DeleteOAuthFlowCookie()
    {
        this.Response.Cookies.Delete(CookieKeys.OAuthFlow, this.BuildOAuthFlowCookieOptions(includeExpiry: false));
    }

    /// <summary>
    /// Builds the cross-domain cookie options used for the <c>oauth_flow</c> cookie, matching the
    /// refresh-cookie settings so the browser accepts and later clears it.
    /// </summary>
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

    /// <summary>
    /// Generates a base64url-encoded (no padding) token from <paramref name="byteLength"/> CSPRNG bytes.
    /// </summary>
    private static string GenerateUrlToken(int byteLength)
    {
        var bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Computes the PKCE S256 code challenge for the supplied verifier.
    /// </summary>
    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return WebEncoders.Base64UrlEncode(hash);
    }

    /// <summary>
    /// Cookie payload binding an in-flight OAuth authorization to its provider, CSRF state and PKCE verifier.
    /// </summary>
    private sealed record OAuthFlowState
    {
        /// <summary>
        /// The OAuth provider this flow was initiated for.
        /// </summary>
        public string Provider { get; init; } = default!;

        /// <summary>
        /// The CSRF state value echoed back by the provider on the callback.
        /// </summary>
        public string State { get; init; } = default!;

        /// <summary>
        /// The PKCE code verifier used during token exchange.
        /// </summary>
        public string CodeVerifier { get; init; } = default!;
    }
}