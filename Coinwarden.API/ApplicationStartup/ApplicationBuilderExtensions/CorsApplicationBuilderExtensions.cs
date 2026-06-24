using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Coinwarden.API.Constants;

namespace Coinwarden.API.ApplicationStartup.ApplicationBuilderExtensions;

public static class CorsApplicationBuilderExtensions
{
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
                .AllowAnyHeader()
                .AllowCredentials()
                .WithExposedHeaders(config.GetSection(ConfigurationKeys.CorsExposedHeaders).Get<string[]>() ?? [AppHeaderNames.TokenExpired, AppHeaderNames.CorrelationId]));

        return app;
    }
}