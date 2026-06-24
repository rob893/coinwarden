using System.ComponentModel.DataAnnotations;

namespace Coinwarden.API.Models.Requests;

public sealed record ResetPasswordRequest
{
    [Required]
    public string Password { get; init; } = default!;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = default!;

    [Required]
    public string Token { get; init; } = default!;
}