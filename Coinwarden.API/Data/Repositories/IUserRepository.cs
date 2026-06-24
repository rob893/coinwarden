using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Core;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.QueryParameters;
using Microsoft.AspNetCore.Identity;

namespace Coinwarden.API.Data.Repositories;

/// <summary>
/// Repository interface for user data access.
/// </summary>
public interface IUserRepository : IRepository<User, CursorPaginationQueryParameters>
{
    /// <summary>Gets the ASP.NET Identity UserManager.</summary>
    UserManager<User> UserManager { get; }

    /// <summary>Creates a user without a password (for social login flows).</summary>
    Task<IdentityResult> CreateUserWithoutPasswordAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Creates a user with the given password.</summary>
    Task<IdentityResult> CreateUserWithPasswordAsync(User user, string password, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by username using the default includes.</summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by username with additional includes.</summary>
    Task<User?> GetByUsernameAsync(string username, Expression<Func<User, object>>[] includes, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by email with additional includes.</summary>
    Task<User?> GetByEmailAsync(string email, Expression<Func<User, object>>[] includes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a user by either username or email in a single query, applying the default includes plus
    /// any additional includes. The input is normalized and matched against both the normalized username
    /// and normalized email, using the existing indexes on those columns.
    /// </summary>
    /// <param name="usernameOrEmail">The username or email to search for.</param>
    /// <param name="includes">Additional navigation properties to eager-load.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The matching tracked user, or <c>null</c> if none is found.</returns>
    Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, Expression<Func<User, object>>[] includes, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by a linked OAuth account.</summary>
    Task<User?> GetByLinkedAccountAsync(string id, LinkedAccountType accountType, Expression<Func<User, object>>[] includes, CancellationToken cancellationToken = default);

    /// <summary>Verifies a user's password via the SignInManager.</summary>
    Task<bool> CheckPasswordAsync(User user, string password, CancellationToken cancellationToken = default);

    /// <summary>Returns a cursor-paginated list of roles.</summary>
    Task<CursorPaginatedList<Role, int>> GetRolesAsync(CursorPaginationQueryParameters searchParams, CancellationToken cancellationToken = default);

    /// <summary>Returns all roles.</summary>
    Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all refresh tokens for a device ID.</summary>
    Task<IReadOnlyList<RefreshToken>> GetRefreshTokensForDeviceAsync(string deviceId, bool track = true, CancellationToken cancellationToken = default);

    /// <summary>Deletes all expired refresh tokens for the given user using a targeted bulk delete.</summary>
    /// <param name="userId">The ID of the user whose expired refresh tokens should be removed.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task DeleteExpiredRefreshTokensAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a user tracked with the data required for token refresh: roles for JWT claims and only the
    /// specified device's refresh tokens for rotation.
    /// </summary>
    /// <param name="userId">The ID of the user to load.</param>
    /// <param name="deviceId">The device ID whose refresh tokens should be included.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The tracked user, or <c>null</c> if no user with the given ID exists.</returns>
    Task<User?> GetUserForTokenRefreshAsync(int userId, string deviceId, CancellationToken cancellationToken = default);
}
