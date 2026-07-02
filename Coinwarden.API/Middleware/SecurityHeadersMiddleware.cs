using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Coinwarden.API.Middleware;

/// <summary>
/// Adds browser security response headers that cannot be delivered by the SPA meta CSP.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private const string ContentSecurityPolicy = "frame-ancestors 'none'";
    private const string XFrameOptions = "DENY";

    private readonly RequestDelegate next;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityHeadersMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the request pipeline.</param>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        this.next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>
    /// Adds clickjacking protection headers before the response starts.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the next middleware has run.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            httpContext.Response.Headers[HeaderNames.XFrameOptions] = XFrameOptions;
            httpContext.Response.Headers[HeaderNames.ContentSecurityPolicy] = ContentSecurityPolicy;
            return Task.CompletedTask;
        }, context);

        await this.next(context);
    }
}
