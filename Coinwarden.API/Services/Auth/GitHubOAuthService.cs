using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Coinwarden.API.Models.GitHub;
using Coinwarden.API.Models.Settings;
using Microsoft.Extensions.Options;

namespace Coinwarden.API.Services.Auth;

/// <summary>
/// Service for validating GitHub OAuth tokens and retrieving user information
/// </summary>
public sealed class GitHubOAuthService : IGitHubOAuthService
{
    private const string AppName = "Coinwarden";

    private static readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory httpClientFactory;

    private readonly AuthenticationSettings authSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubOAuthService"/> class
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory for making API calls</param>
    /// <param name="authSettings">Authentication settings</param>
    public GitHubOAuthService(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthenticationSettings> authSettings)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.authSettings = authSettings?.Value ?? throw new ArgumentNullException(nameof(authSettings));
    }

    public async Task<string> ExchangeCodeForGithubAccessTokenAsync(string code, string codeVerifier, CancellationToken cancellationToken)
    {
        using var client = this.httpClientFactory.CreateClient();

        using var encodedContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", this.authSettings.GitHubOAuthClientId },
            { "client_secret", this.authSettings.GitHubOAuthClientSecret },
            { "code", code },
            { "redirect_uri", this.authSettings.GitHubOAuthRedirectUri.ToString() },
            { "code_verifier", codeVerifier }
        });
        var response = await client.PostAsync(new Uri("https://github.com/login/oauth/access_token"), encodedContent, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var query = HttpUtility.ParseQueryString(content);

        return query["access_token"] ?? throw new InvalidOperationException("GitHub access token not found in response.");
    }

    public async Task<GitHubUser> GetGitHubUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var client = this.httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(AppName);

        var response = await client.GetStringAsync(new Uri("https://api.github.com/user"), cancellationToken);
        return JsonSerializer.Deserialize<GitHubUser>(response, jsonSerializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub user information.");
    }

    public async Task<List<GitHubEmail>> GetGitHubEmailsAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var client = this.httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(AppName);

        var response = await client.GetAsync(new Uri("https://api.github.com/user/emails"), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GitHub email request failed with status code {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<List<GitHubEmail>>(content, jsonSerializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub email response.");
    }
}