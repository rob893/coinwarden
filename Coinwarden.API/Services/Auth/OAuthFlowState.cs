namespace Coinwarden.API.Services.Auth;

/// <summary>
/// Cookie payload binding an in-flight OAuth authorization to its provider, CSRF state, and PKCE verifier.
/// </summary>
public sealed record OAuthFlowState
{
    /// <summary>
    /// Gets the OAuth provider this flow was initiated for.
    /// </summary>
    public string Provider { get; init; } = default!;

    /// <summary>
    /// Gets the CSRF state value echoed back by the provider on the callback.
    /// </summary>
    public string State { get; init; } = default!;

    /// <summary>
    /// Gets the PKCE code verifier used during token exchange.
    /// </summary>
    public string CodeVerifier { get; init; } = default!;
}
