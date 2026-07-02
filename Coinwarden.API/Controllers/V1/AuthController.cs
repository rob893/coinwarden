using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Constants;
using Coinwarden.API.Data.Repositories;
using Coinwarden.API.Extensions;
using Coinwarden.API.Models.Dtos;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.Requests.Auth;
using Coinwarden.API.Models.Responses.Auth;
using Coinwarden.API.Services.Auth;
using Coinwarden.API.Services.Core;
using Coinwarden.API.Services.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    private static readonly string[] enumerationErrorCodes =
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

    private readonly IAuthTokenCookieService authTokenCookieService;

    private readonly IOAuthFlowCookieService oauthFlowCookieService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="userRepository">The user repository.</param>
    /// <param name="jwtTokenService">The JWT token service.</param>
    /// <param name="userService">The user domain service.</param>
    /// <param name="authTokenCookieService">The auth token and cookie issuer.</param>
    /// <param name="oauthFlowCookieService">The OAuth flow cookie service used to clear stale flow cookies.</param>
    /// <param name="correlationIdService">The correlation ID service.</param>
    public AuthController(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IUserService userService,
        IAuthTokenCookieService authTokenCookieService,
        IOAuthFlowCookieService oauthFlowCookieService,
        ICorrelationIdService correlationIdService)
            : base(correlationIdService)
    {
        this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        this.jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
        this.authTokenCookieService = authTokenCookieService ?? throw new ArgumentNullException(nameof(authTokenCookieService));
        this.oauthFlowCookieService = oauthFlowCookieService ?? throw new ArgumentNullException(nameof(oauthFlowCookieService));
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
            if (result.Errors.Any(error => enumerationErrorCodes.Contains(error.Code, StringComparer.Ordinal)))
            {
                return this.BadRequest(genericRegistrationFailure);
            }

            return this.BadRequest([.. result.Errors.Select(e => e.Description)]);
        }

        var token = await this.authTokenCookieService.GenerateAndSaveAccessAndRefreshTokensAsync(user, registerUserRequest.DeviceId, cancellationToken);

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

        var token = await this.authTokenCookieService.GenerateAndSaveAccessAndRefreshTokensAsync(user, loginRequest.DeviceId, cancellationToken);

        var userToReturn = UserDto.FromEntity(user);

        return this.Ok(
            new LoginResponse
            {
                Token = token,
                User = userToReturn
            });
    }

    /// <summary>
    /// Logs the user out by revoking all refresh tokens and clearing auth cookies.
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
        this.authTokenCookieService.DeleteAuthCookies();
        this.oauthFlowCookieService.DeleteOAuthFlowCookie();

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

        // CSRF protection using the Double Submit Cookie pattern. This is the only endpoint that
        // requires a CSRF token as it is the only one that authenticates via cookies.
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

        var token = await this.authTokenCookieService.GenerateAndSaveAccessAndRefreshTokensAsync(user, refreshTokenRequest.DeviceId, cancellationToken, updateLastLogin: false);

        return this.Ok(
            new RefreshTokenResponse
            {
                Token = token
            });
    }
}
