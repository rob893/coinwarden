using System.Collections.Generic;

namespace Coinwarden.API.Models.Requests;

public sealed record EditRoleRequest
{
    public IReadOnlyList<string> RoleNames { get; init; } = [];
}