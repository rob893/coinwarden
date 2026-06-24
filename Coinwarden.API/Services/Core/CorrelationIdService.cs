using System;
using System.Threading;

namespace Coinwarden.API.Services.Core;

public sealed class CorrelationIdService : ICorrelationIdService
{
    private static readonly AsyncLocal<string?> current = new();

    public string CorrelationId
    {
        get
        {
            var id = current.Value;
            if (id is null)
            {
                id = Guid.NewGuid().ToString();
                current.Value = id;
            }

            return id;
        }

        set => current.Value = value;
    }
}