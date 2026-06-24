using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Coinwarden.API.Constants;
using Coinwarden.API.Models.Settings;

namespace Coinwarden.API.Middleware
{
    /// <summary>
    /// This middleware rewrites the request path base from the <c>X-Forwarded-Prefix</c> header, but
    /// only when the supplied value is explicitly allowlisted. Because the header is client-supplied,
    /// honouring arbitrary values would let any client poison generated URLs (Location headers, links).
    /// </summary>
    public sealed class PathBaseRewriterMiddleware
    {
        private readonly RequestDelegate next;

        private readonly ForwardedHeadersSettings settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathBaseRewriterMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="settings">Forwarded-headers settings containing the allowlist of trusted path prefixes.</param>
        public PathBaseRewriterMiddleware(RequestDelegate next, IOptions<ForwardedHeadersSettings> settings)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Applies the forwarded path prefix to <see cref="HttpRequest.PathBase"/> when, and only when,
        /// the client-supplied value exactly matches an allowlisted prefix.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A task that completes when the request has been processed.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Request.Headers.TryGetValue(AppHeaderNames.ForwardedPrefix, out var value))
            {
                var prefix = value.FirstOrDefault();

                if (!string.IsNullOrEmpty(prefix) &&
                    this.settings.AllowedForwardedPrefixes.Contains(prefix, StringComparer.Ordinal))
                {
                    context.Request.PathBase = prefix;
                }
            }

            await this.next(context);
        }
    }
}