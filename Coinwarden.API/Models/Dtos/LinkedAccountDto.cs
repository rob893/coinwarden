using System;
using Coinwarden.API.Models.Entities;

namespace Coinwarden.API.Models.Dtos;

public sealed record LinkedAccountDto : IIdentifiable<string>, IOwnedByUser<int>
{
    public required string Id { get; init; }

    public required LinkedAccountType LinkedAccountType { get; init; }

    public required int UserId { get; init; }

    public static LinkedAccountDto FromEntity(LinkedAccount linkedAccount)
    {
        ArgumentNullException.ThrowIfNull(linkedAccount);

        return new LinkedAccountDto
        {
            Id = linkedAccount.Id,
            LinkedAccountType = linkedAccount.LinkedAccountType,
            UserId = linkedAccount.UserId
        };
    }
}