using Microsoft.AspNetCore.Identity;

namespace Coinwarden.API.Models.Entities;

public sealed class UserRole : IdentityUserRole<int>
{
    public User User { get; set; } = default!;

    public Role Role { get; set; } = default!;
}