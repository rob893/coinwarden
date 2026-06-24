
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Coinwarden.API.Extensions;

namespace Coinwarden.API.Utilities;

/// <summary>
/// Builds the universal set of per-request logging-scope dimensions used by
/// <c>LoggingEnrichmentMiddleware</c> and <c>GlobalExceptionHandlerMiddleware</c>.
/// Centralizing the shape here ensures both middlewares produce the same
/// <c>customDimensions</c> on traces and exceptions.
/// </summary>
public static class RequestLoggingScope
{
    public const string CorrelationIdKey = "CorrelationId";

    public const string UserIdKey = "UserId";

    public const string UnauthenticatedUserId = "<unauthenticated>";

    /// <summary>
    /// Builds the basic logging-scope dictionary for the current request.
    /// </summary>
    /// <param name="context">The HTTP context. Required for caller identity claims.</param>
    /// <param name="correlationId">The correlation id for the current request.</param>
    /// <returns>A mutable dictionary suitable for <c>ILogger.BeginScope</c>.</returns>
    public static Dictionary<string, object?> Build(HttpContext context, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(correlationId);

        var user = context.User;

        return new Dictionary<string, object?>
        {
            [CorrelationIdKey] = correlationId,
            [UserIdKey] = user.TryGetUserId(out var userId) ? userId : UnauthenticatedUserId
        };
    }
}
