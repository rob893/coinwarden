using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Core;
using Coinwarden.API.Models.Auth;
using Coinwarden.API.Models.Entities;

namespace Coinwarden.API.Services.Auth;

/// <summary>
/// Resolves or provisions a local user from a verified external (OAuth) identity. Encapsulates the
/// shared "find by linked account → else find by email and link → else create" flow used by the
/// social login endpoints.
/// </summary>
public interface IExternalLoginService
{
    /// <summary>
    /// Resolves the local user associated with the given external identity, provisioning a new user
    /// or linking the identity to an existing user when necessary.
    /// </summary>
    /// <param name="identity">The provider-normalized external identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A successful result containing the resolved or newly provisioned user, or a failure result
    /// describing why the user could not be resolved or provisioned.
    /// </returns>
    Task<Result<User>> ResolveOrProvisionUserAsync(ExternalLoginIdentity identity, CancellationToken cancellationToken);
}
