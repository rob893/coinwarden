using System.ComponentModel.DataAnnotations;

namespace Coinwarden.API.Models.Requests.Auth;

public sealed record RefreshTokenRequest
{
    [Required]
    public string DeviceId { get; init; } = default!;
}