using Coinwarden.API.Constants;
using Coinwarden.API.Models;
using Coinwarden.API.Models.Entities;

namespace Coinwarden.API.Services.Auth;

/// <summary>
/// Service providing information about the currently authenticated user.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Gets a value indicating whether the user is logged in.</summary>
    bool IsUserLoggedIn { get; }

    /// <summary>Gets the current user's ID.</summary>
    int UserId { get; }

    /// <summary>Gets the current user's username.</summary>
    string UserName { get; }

    /// <summary>Gets a value indicating whether the user's email is verified.</summary>
    bool EmailVerified { get; }

    /// <summary>Checks whether the current user is in the given role.</summary>
    bool IsInRole(string role);

    /// <summary>Gets a value indicating whether the user has the Admin role.</summary>
    bool IsAdmin => this.IsInRole(UserRoleName.Admin);

    /// <summary>Returns true if the current user is authorized to access the given resource.</summary>
    bool IsUserAuthorizedForResource(IOwnedByUser<int> resource, bool isAdminAuthorized = true);

    /// <summary>Overrides the current user identity (used by the database seeder).</summary>
    void SetOverrideUser(User user);

    /// <summary>Clears any override user identity.</summary>
    void ClearOverrideUser();
}