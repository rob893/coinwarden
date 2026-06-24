using System.ComponentModel.DataAnnotations;

namespace Coinwarden.API.Models.Requests.Auth;

public sealed record LoginRequest
{
    [Required]
    public string UserName { get; init; } = default!;

    [Required]
    public string Password { get; init; } = default!;

    [Required]
    public string DeviceId { get; init; } = default!;
}