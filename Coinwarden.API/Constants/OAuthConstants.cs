namespace Coinwarden.API.Constants;

/// <summary>
/// Constants for the backend-initiated OAuth authorization-code flows (state + PKCE).
/// </summary>
public static class OAuthConstants
{
    /// <summary>
    /// Logical provider name for GitHub, used in cookie payloads, callback routes and redirect URLs.
    /// </summary>
    public const string GitHubProvider = "github";

    /// <summary>
    /// Logical provider name for Google, used in cookie payloads, callback routes and redirect URLs.
    /// </summary>
    public const string GoogleProvider = "google";

    /// <summary>
    /// GitHub OAuth authorize endpoint.
    /// </summary>
    public const string GitHubAuthorizeUrl = "https://github.com/login/oauth/authorize";

    /// <summary>
    /// OAuth scopes requested from GitHub.
    /// </summary>
    public const string GitHubScope = "read:user user:email";

    /// <summary>
    /// Google OAuth authorize endpoint.
    /// </summary>
    public const string GoogleAuthorizeUrl = "https://accounts.google.com/o/oauth2/v2/auth";

    /// <summary>
    /// OAuth scopes requested from Google.
    /// </summary>
    public const string GoogleScope = "openid email profile";

    /// <summary>
    /// PKCE code challenge method used by both providers.
    /// </summary>
    public const string CodeChallengeMethod = "S256";

    /// <summary>
    /// Data Protection purpose string used to protect the OAuth flow cookie payload.
    /// </summary>
    public const string DataProtectionPurpose = "Coinwarden.OAuthFlow";
}
