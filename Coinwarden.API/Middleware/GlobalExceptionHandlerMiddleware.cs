using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Coinwarden.API.Core;
using Coinwarden.API.Extensions;
using Coinwarden.API.Services.Core;
using Coinwarden.API.Utilities;

namespace Coinwarden.API.Middleware;

public sealed class GlobalExceptionHandlerMiddleware
{
    // Generic, status-keyed response bodies. Intentionally free of exception type names, table
    // names, dependency identifiers, or any other server-side detail. The correlation ID surfaced
    // by ProblemDetailsWithErrors is the support handle for matching the response to logs.
    private static readonly Dictionary<int, string> safeStatusMessages = new()
    {
        { StatusCodes.Status500InternalServerError, "An unexpected error occurred while processing the request. Please reference the correlationId when contacting support." },
        { StatusCodes.Status504GatewayTimeout, "The request timed out. Please reference the correlationId when contacting support." }
    };

    private const string FallbackSafeMessage = "An error occurred while processing the request. Please reference the correlationId when contacting support.";

    private readonly ILogger<GlobalExceptionHandlerMiddleware> logger;

    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public GlobalExceptionHandlerMiddleware(RequestDelegate _, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdService correlationIdService)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(correlationIdService);

        var error = context.Features.Get<IExceptionHandlerFeature>();

        if (error == null)
        {
            return;
        }

        var thrownException = error.Error;
        var statusCode = thrownException switch
        {
            TimeoutException => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status500InternalServerError
        };

        // Prefer the value populated by CorrelationIdMiddleware (single source of truth,
        // shared with LoggingEnrichmentMiddleware). Fall back to the request header (or
        // generating a new id) if the exception was thrown before CorrelationIdMiddleware
        // had a chance to populate the service.
        var correlationId = !string.IsNullOrEmpty(correlationIdService.CorrelationId)
            ? correlationIdService.CorrelationId
            : context.Request.Headers.GetOrGenerateCorrelationId();

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        // Guard against the case where the response has already started writing
        // (e.g., a partial body was flushed before the exception was thrown). In that
        // situation, mutating StatusCode/ContentType throws InvalidOperationException
        // and we cannot reset the response. We still log below; the client will receive
        // whatever was already on the wire.
        if (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = statusCode;
        }

        // Apply the same per-request logging-scope dimensions used by LoggingEnrichmentMiddleware
        // so the exception/trace rows emitted here carry the basic customDimensions
        // (CorrelationId, UserAlias, CallerType, ApplicationId). The enrichment middleware's
        // BeginScope has already been disposed by the time this handler runs (the exception
        // unwound the pipeline), so we re-establish the scope here from the still-live HttpContext.
        var loggingScope = RequestLoggingScope.Build(context, correlationId);

        using (this.logger.BeginScope(loggingScope))
        {
            // Log the full exception (including stack trace and inner-exception chain) for the
            // operator. The exception object is intentionally NOT copied into the response body
            // because messages from EF Core, Postgres, identity providers, etc. can disclose
            // schema, dependency, and infrastructure details that aid attackers.
            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                this.logger.LogError(thrownException, "Unhandled exception");
            }
            else
            {
                this.logger.LogWarning(thrownException, "Unhandled exception");
            }
        }

        if (!safeStatusMessages.TryGetValue(statusCode, out var safeMessage))
        {
            safeMessage = FallbackSafeMessage;
        }

        var problemDetails = new ProblemDetailsWithErrors(safeMessage, statusCode, context.Request);
        var jsonResponse = JsonSerializer.Serialize(problemDetails, this.jsonOptions);

        if (!context.Response.HasStarted)
        {
            await context.Response.WriteAsync(jsonResponse, context.RequestAborted);
        }
    }
}