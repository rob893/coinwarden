using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Models.Entities;

namespace Coinwarden.API.Services.Auth;

/// <summary>
/// Issues access tokens and manages the authentication cookies paired with refresh tokens.
/// </summary>
public interface IAuthTokenCookieService
{
    /// <summary>
    /// Generates an access token, saves a refresh token, and writes refresh/CSRF cookies.
    /// </summary>
    /// <param name="user">The authenticated user receiving tokens.</param>
    /// <param name="deviceId">The client device identifier associated with the refresh token.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="updateLastLogin">Whether to update the user's last-login timestamp.</param>
    /// <returns>The generated access token.</returns>
    Task<string> GenerateAndSaveAccessAndRefreshTokensAsync(User user, string deviceId, CancellationToken cancellationToken, bool updateLastLogin = true);

    /// <summary>
    /// Deletes the refresh-token and CSRF cookies using the configured cross-origin cookie options.
    /// </summary>
    void DeleteAuthCookies();
}
