using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Coinwarden.API.Constants;

namespace Coinwarden.API.ApplicationStartup.ApplicationBuilderExtensions;

/// <summary>
/// Provides CORS pipeline configuration for the API.
/// </summary>
public static class CorsApplicationBuilderExtensions
{
    private static readonly string[] allowedRequestHeaderValues =
    [
        "Authorization",
        "Content-Type",
        AppHeaderNames.CsrfToken,
        AppHeaderNames.CorrelationId
    ];

    /// <summary>
    /// Gets the request headers accepted by credentialed cross-origin requests.
    /// </summary>
    public static IReadOnlyList<string> AllowedRequestHeaders => allowedRequestHeaderValues;

    /// <summary>
    /// Adds the configured CORS policy to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder to configure.</param>
    /// <param name="config">The application configuration containing allowed CORS origins and exposed headers.</param>
    /// <returns>The configured application builder.</returns>
    public static IApplicationBuilder UseAndConfigureCors(this IApplicationBuilder app, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(config);

        // Fail closed: when no origins are configured allow NO cross-origin requests.
        // Never combine a wildcard ("*") origin with AllowCredentials().
        var allowedOrigins = config.GetSection(ConfigurationKeys.CorsAllowedOrigins).Get<string[]>() ?? [];

        app.UseCors(header =>
            header.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .WithHeaders(allowedRequestHeaderValues)
                .AllowCredentials()
                .WithExposedHeaders(config.GetSection(ConfigurationKeys.CorsExposedHeaders).Get<string[]>() ?? [AppHeaderNames.TokenExpired, AppHeaderNames.CorrelationId]));

        return app;
    }
}