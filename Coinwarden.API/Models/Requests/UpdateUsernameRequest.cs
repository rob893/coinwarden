using System.ComponentModel.DataAnnotations;

namespace Coinwarden.API.Models.Requests;

public sealed record UpdateUsernameRequest
{
    [Required]
    [MinLength(1)]
    public string NewUsername { get; init; } = default!;
}