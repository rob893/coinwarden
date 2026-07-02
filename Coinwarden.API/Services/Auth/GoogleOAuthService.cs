using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Models.Settings;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace Coinwarden.API.Services.Auth;

/// <summary>
/// Service for handling Google OAuth tokens and retrieving user information
/// </summary>
public sealed class GoogleOAuthService : IGoogleOAuthService
{
    private static readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory httpClientFactory;

    private readonly AuthenticationSettings authSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleOAuthService"/> class
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory for making API calls</param>
    /// <param name="authSettings">Authentication settings</param>
    public GoogleOAuthService(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthenticationSettings> authSettings)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.authSettings = authSettings?.Value ?? throw new ArgumentNullException(nameof(authSettings));
    }

    public async Task<string> ExchangeCodeForGoogleIdTokenAsync(string code, string codeVerifier, CancellationToken cancellationToken)
    {
        using var client = this.httpClientFactory.CreateClient();

        using var encodedContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", this.authSettings.GoogleOAuthClientId },
            { "client_secret", this.authSettings.GoogleOAuthClientSecret },
            { "code", code },
            { "grant_type", "authorization_code" },
            { "redirect_uri", this.authSettings.GoogleOAuthRedirectUri.ToString() },
            { "code_verifier", codeVerifier }
        });

        var response = await client.PostAsync(new Uri("https://oauth2.googleapis.com/token"), encodedContent, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Google token exchange failed with status code {response.StatusCode}: {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(content, jsonSerializerOptions);

        return tokenResponse?.IdToken ?? throw new InvalidOperationException("Google ID token not found in response.");
    }

    public async Task<GoogleJsonWebSignature.Payload> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken)
    {
        // GoogleJsonWebSignature.ValidateAsync has no CancellationToken overload, so the token
        // cannot be honored by the underlying API. The parameter is kept for interface consistency.
        return await GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = ResolveGoogleOAuthAudiences(this.authSettings)
            });
    }

    /// <summary>
    /// Resolves the Google ID-token audiences to validate against, defaulting to the configured
    /// client id when no explicit audiences are set so the <c>aud</c> claim is always pinned.
    /// </summary>
    /// <param name="authSettings">The authentication settings.</param>
    /// <returns>The audiences to validate the Google ID token against.</returns>
    internal static IReadOnlyList<string> ResolveGoogleOAuthAudiences(AuthenticationSettings authSettings)
    {
        ArgumentNullException.ThrowIfNull(authSettings);

        if (authSettings.GoogleOAuthAudiences.Count > 0)
        {
            return authSettings.GoogleOAuthAudiences;
        }

        return string.IsNullOrWhiteSpace(authSettings.GoogleOAuthClientId)
            ? []
            : [authSettings.GoogleOAuthClientId];
    }

    private sealed record GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = default!;

        [JsonPropertyName("id_token")]
        public string IdToken { get; init; } = default!;

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = default!;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("scope")]
        public string Scope { get; init; } = default!;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }
    }
}