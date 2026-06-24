using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth;

namespace Coinwarden.API.Services.Auth;

/// <summary>
/// Service for handling Google OAuth tokens and retrieving user information
/// </summary>
public interface IGoogleOAuthService
{
    Task<string> ExchangeCodeForGoogleIdTokenAsync(string code, string codeVerifier, CancellationToken cancellationToken);

    Task<GoogleJsonWebSignature.Payload> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken);
}