using System;
using System.Collections.Generic;
using System.Linq;
using Coinwarden.API.Models.Entities;

namespace Coinwarden.API.Models.Dtos;

/// <summary>
/// Data transfer object representing a user.
/// </summary>
public sealed record UserDto : IIdentifiable<int>
{
    /// <summary>Gets the user ID.</summary>
    public required int Id { get; init; }

    /// <summary>Gets the username.</summary>
    public required string UserName { get; init; }

    /// <summary>Gets the email address.</summary>
    public required string Email { get; init; }

    /// <summary>Gets a value indicating whether the email is confirmed.</summary>
    public required bool EmailConfirmed { get; init; }

    /// <summary>Gets the creation date.</summary>
    public required DateTimeOffset Created { get; init; }

    /// <summary>Gets the roles assigned to the user.</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>Gets the linked OAuth accounts.</summary>
    public required IReadOnlyList<LinkedAccountDto> LinkedAccounts { get; init; }

    /// <summary>Gets the last login date.</summary>
    public required DateTimeOffset? LastLogin { get; init; }

    /// <summary>Gets the last password change date.</summary>
    public required DateTimeOffset LastPasswordChange { get; init; }

    /// <summary>Gets the last email change date.</summary>
    public required DateTimeOffset LastEmailChange { get; init; }

    /// <summary>Gets the last username change date.</summary>
    public required DateTimeOffset LastUsernameChange { get; init; }

    /// <summary>Gets the last email confirmation sent date.</summary>
    public required DateTimeOffset? LastEmailConfirmationSent { get; init; }

    /// <summary>
    /// Creates a <see cref="UserDto"/> from a <see cref="User"/> entity.
    /// </summary>
    /// <param name="user">The user entity.</param>
    /// <returns>A mapped <see cref="UserDto"/>.</returns>
    public static UserDto FromEntity(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            Created = user.Created,
            LastLogin = user.LastLogin,
            LastPasswordChange = user.LastPasswordChange,
            LastEmailChange = user.LastEmailChange,
            LastUsernameChange = user.LastUsernameChange,
            LastEmailConfirmationSent = user.LastEmailConfirmationSent,
            Roles = [.. user.UserRoles.Select(x => x.Role).Select(role => role.Name ?? string.Empty)],
            LinkedAccounts = [.. user.LinkedAccounts.Select(LinkedAccountDto.FromEntity)]
        };
    }
}