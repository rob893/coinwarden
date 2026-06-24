using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Coinwarden.API.Models.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Coinwarden.API.Middleware;

public sealed class OpenApiBasicAuthMiddleware
{
    private readonly RequestDelegate next;

    public OpenApiBasicAuthMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<OpenApiSettings> openApiSettings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(openApiSettings);

        var settings = openApiSettings.Value;
        var authSettings = settings.AuthSettings;

        // Make sure we are hitting the swagger/openapi/scalar paths
        if (context.Request.Path.StartsWithSegments("/swagger", StringComparison.Ordinal) ||
            context.Request.Path.StartsWithSegments("/openapi", StringComparison.Ordinal) ||
            context.Request.Path.StartsWithSegments("/scalar", StringComparison.Ordinal))
        {
            if (!settings.Enabled)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            if (!authSettings.RequireAuth)
            {
                await this.next.Invoke(context);
                return;
            }

            if (context.Request.Headers.TryGetValue("Authorization", out var authHeader) && authHeader.ToString().StartsWith("Basic ", StringComparison.Ordinal))
            {
                // Get the encoded username and password
                // Take everything after the "Basic " scheme prefix; an empty/whitespace
                // remainder is handled by TryParseBasicCredentials (-> 401), never a crash.
                var encodedUsernamePassword = authHeader.ToString()["Basic ".Length..].Trim();

                if (TryParseBasicCredentials(encodedUsernamePassword, out var username, out var password) &&
                    IsAuthorized(username, password, authSettings))
                {
                    await this.next.Invoke(context);
                    return;
                }
            }

            // Return authentication type (causes browser to show login dialog)
            context.Response.Headers["WWW-Authenticate"] = "Basic";

            // Return unauthorized
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        }
        else
        {
            await this.next.Invoke(context);
        }
    }

    /// <summary>
    /// Decodes and splits a Base64-encoded "username:password" Basic auth value.
    /// </summary>
    /// <param name="encodedUsernamePassword">The Base64-encoded credentials, may be null.</param>
    /// <param name="username">The parsed username when successful.</param>
    /// <param name="password">The parsed password when successful.</param>
    /// <returns><see langword="true"/> if the value was valid Base64 containing a colon separator; otherwise <see langword="false"/>.</returns>
    private static bool TryParseBasicCredentials(string? encodedUsernamePassword, out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;

        if (string.IsNullOrEmpty(encodedUsernamePassword))
        {
            return false;
        }

        string decodedUsernamePassword;
        try
        {
            decodedUsernamePassword = Encoding.UTF8.GetString(Convert.FromBase64String(encodedUsernamePassword));
        }
        catch (FormatException)
        {
            return false;
        }

        // A malformed header without a colon separator must yield 401, not a 500.
        var separatorIndex = decodedUsernamePassword.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return false;
        }

        username = decodedUsernamePassword[..separatorIndex];
        password = decodedUsernamePassword[(separatorIndex + 1)..];
        return true;
    }

    private static bool IsAuthorized(string username, string password, OpenApiAuthSettings authSettings)
    {
        // Fail closed when no credentials are configured so that RequireAuth=true with
        // missing Username/Password returns 401 instead of allowing empty credentials.
        if (string.IsNullOrEmpty(authSettings.Username) || string.IsNullOrEmpty(authSettings.Password))
        {
            return false;
        }

        // Compare both username and password in constant time without short-circuiting
        // to avoid leaking credential information through timing side channels.
        var usernameMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(username),
            Encoding.UTF8.GetBytes(authSettings.Username));
        var passwordMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(password),
            Encoding.UTF8.GetBytes(authSettings.Password));

        return usernameMatches & passwordMatches;
    }
}