namespace Coinwarden.API.Services.Auth;

/// <summary>
/// Manages OAuth state/PKCE flow cookies and provider callback redirects.
/// </summary>
public interface IOAuthFlowCookieService
{
    /// <summary>
    /// Starts a GitHub OAuth authorization-code flow.
    /// </summary>
    /// <returns>The GitHub authorization URL.</returns>
    string BeginGitHubFlow();

    /// <summary>
    /// Starts a Google OAuth authorization-code flow.
    /// </summary>
    /// <returns>The Google authorization URL.</returns>
    string BeginGoogleFlow();

    /// <summary>
    /// Builds the UI redirect URL for an OAuth callback after validating cookie-bound state.
    /// </summary>
    /// <param name="provider">The expected OAuth provider.</param>
    /// <param name="code">The authorization code returned by the provider.</param>
    /// <param name="state">The state returned by the provider.</param>
    /// <returns>The UI callback URL including either a code or error query parameter.</returns>
    string BuildCallbackRedirectUrl(string provider, string code, string state);

    /// <summary>
    /// Attempts to read the protected OAuth flow cookie for the expected provider.
    /// </summary>
    /// <param name="expectedProvider">The provider that must match the cookie payload.</param>
    /// <param name="flow">The parsed OAuth flow state when valid.</param>
    /// <returns><see langword="true"/> when the cookie is present, protected, and matches the provider.</returns>
    bool TryReadOAuthFlowCookie(string expectedProvider, out OAuthFlowState flow);

    /// <summary>
    /// Deletes the OAuth flow cookie using the same cross-origin cookie options used to create it.
    /// </summary>
    void DeleteOAuthFlowCookie();
}
