
using System;
using Microsoft.AspNetCore.Builder;
using Coinwarden.API.Middleware;

namespace Coinwarden.API.ApplicationStartup.ApplicationBuilderExtensions;

public static class MiddlewareApplicationBuilderExtensions
{
    public static IApplicationBuilder UseCorrelationIdMiddleware(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<CorrelationIdMiddleware>();

        return app;
    }

    public static IApplicationBuilder UseGlobalExceptionHandlerMiddleware(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler(builder => builder.UseMiddleware<GlobalExceptionHandlerMiddleware>());

        return app;
    }

    public static IApplicationBuilder UseSecurityHeadersMiddleware(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<SecurityHeadersMiddleware>();

        return app;
    }
}