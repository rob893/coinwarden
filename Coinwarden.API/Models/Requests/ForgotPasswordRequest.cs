using System.ComponentModel.DataAnnotations;

namespace Coinwarden.API.Models.Requests;

public sealed record ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = default!;
}