using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Constants;
using Coinwarden.API.Models.Auth;
using Coinwarden.API.Models.Dtos;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.Requests.Auth;
using Coinwarden.API.Models.Responses.Auth;
using Coinwarden.API.Services.Auth;
using Coinwarden.API.Services.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Coinwarden.API.Controllers.V1;

[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1")]
[ApiController]
public sealed class OAuthController : ServiceControllerBase
{
    private readonly IGitHubOAuthService gitHubOAuthService;

    private readonly IGoogleOAuthService googleOAuthService;

    private readonly IExternalLoginService externalLoginService;

    private readonly IAuthTokenCookieService authTokenCookieService;

    private readonly IOAuthFlowCookieService oauthFlowCookieService;

    private readonly ILogger<OAuthController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthController"/> class.
    /// </summary>
    /// <param name="gitHubOAuthService">The GitHub OAuth service.</param>
    /// <param name="googleOAuthService">The Google OAuth service.</param>
    /// <param name="externalLoginService">The external-login provisioning service.</param>
    /// <param name="authTokenCookieService">The auth token and cookie issuer.</param>
    /// <param name="oauthFlowCookieService">The OAuth flow cookie service.</param>
    /// <param name="correlationIdService">The correlation ID service.</param>
    /// <param name="logger">The logger.</param>
    public OAuthController(
        IGitHubOAuthService gitHubOAuthService,
        IGoogleOAuthService googleOAuthService,
        IExternalLoginService externalLoginService,
        IAuthTokenCookieService authTokenCookieService,
        IOAuthFlowCookieService oauthFlowCookieService,
        ICorrelationIdService correlationIdService,
        ILogger<OAuthController> logger)
            : base(correlationIdService)
    {
        this.gitHubOAuthService = gitHubOAuthService ?? throw new ArgumentNullException(nameof(gitHubOAuthService));
        this.googleOAuthService = googleOAuthService ?? throw new ArgumentNullException(nameof(googleOAuthService));
        this.externalLoginService = externalLoginService ?? throw new ArgumentNullException(nameof(externalLoginService));
        this.authTokenCookieService = authTokenCookieService ?? throw new ArgumentNullException(nameof(authTokenCookieService));
        this.oauthFlowCookieService = oauthFlowCookieService ?? throw new ArgumentNullException(nameof(oauthFlowCookieService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Logs the user in using Google OAuth authorization code.
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

            if (!this.oauthFlowCookieService.TryReadOAuthFlowCookie(OAuthConstants.GoogleProvider, out var flow))
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
            var token = await this.authTokenCookieService.GenerateAndSaveAccessAndRefreshTokensAsync(user, loginRequest.DeviceId, cancellationToken);
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
            this.oauthFlowCookieService.DeleteOAuthFlowCookie();
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

            if (!this.oauthFlowCookieService.TryReadOAuthFlowCookie(OAuthConstants.GitHubProvider, out var flow))
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
            var token = await this.authTokenCookieService.GenerateAndSaveAccessAndRefreshTokensAsync(user, loginRequest.DeviceId, cancellationToken);
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
            this.oauthFlowCookieService.DeleteOAuthFlowCookie();
        }
    }

    /// <summary>
    /// Begins the backend-initiated GitHub OAuth flow.
    /// </summary>
    /// <returns>A redirect to GitHub's authorize endpoint.</returns>
    /// <response code="302">Redirects to the GitHub authorize endpoint.</response>
    [AllowAnonymous]
    [HttpGet("github/start", Name = nameof(GitHubStart))]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GitHubStart()
    {
        return this.Redirect(this.oauthFlowCookieService.BeginGitHubFlow());
    }

    /// <summary>
    /// Begins the backend-initiated Google OAuth flow.
    /// </summary>
    /// <returns>A redirect to Google's authorize endpoint.</returns>
    /// <response code="302">Redirects to the Google authorize endpoint.</response>
    [AllowAnonymous]
    [HttpGet("google/start", Name = nameof(GoogleStart))]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GoogleStart()
    {
        return this.Redirect(this.oauthFlowCookieService.BeginGoogleFlow());
    }

    /// <summary>
    /// Handles the GitHub OAuth callback by validating cookie-bound state and redirecting to the UI.
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
        return this.Redirect(this.oauthFlowCookieService.BuildCallbackRedirectUrl(OAuthConstants.GitHubProvider, code, state));
    }

    /// <summary>
    /// Handles the Google OAuth callback by validating cookie-bound state and redirecting to the UI.
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
        return this.Redirect(this.oauthFlowCookieService.BuildCallbackRedirectUrl(OAuthConstants.GoogleProvider, code, state));
    }
}
