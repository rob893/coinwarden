using Coinwarden.API.Models.Entities;

namespace Coinwarden.API.Models.Auth;

/// <summary>
/// Provider-normalized representation of a verified external (OAuth) identity, used to resolve or
/// provision a local user regardless of the originating provider.
/// </summary>
public sealed record ExternalLoginIdentity
{
    /// <summary>The provider-specific subject identifier (e.g. the Google subject or GitHub user id).</summary>
    public required string ProviderSubjectId { get; init; }

    /// <summary>The type of linked account / OAuth provider this identity originates from.</summary>
    public required LinkedAccountType ProviderType { get; init; }

    /// <summary>The email reported by the provider, if any.</summary>
    public string? Email { get; init; }

    /// <summary>Whether the provider considers the email verified.</summary>
    public bool EmailVerified { get; init; }

    /// <summary>The username to assign when provisioning a new user.</summary>
    public required string SuggestedUserName { get; init; }
}
