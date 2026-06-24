using System;

namespace Coinwarden.API.Models.Responses;

/// <summary>
/// Response model for the Hello endpoint.
/// </summary>
public sealed record HelloResponse
{
    /// <summary>Gets a greeting message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the API version that generated the response.</summary>
    public required string Version { get; init; }

    /// <summary>Gets the authenticated user's name.</summary>
    public required string UserName { get; init; }

    /// <summary>Gets an optional timestamp (used in v2+).</summary>
    public DateTimeOffset? Timestamp { get; init; }
}
