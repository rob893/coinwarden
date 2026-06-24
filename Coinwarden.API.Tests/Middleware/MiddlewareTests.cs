using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Coinwarden.API.Constants;
using Coinwarden.API.Middleware;
using Coinwarden.API.Models.Settings;
using Coinwarden.API.Services.Core;

namespace Coinwarden.API.Tests.Middleware;

/// <summary>
/// Tests for <see cref="CorrelationIdMiddleware"/> and <see cref="GlobalExceptionHandlerMiddleware"/>.
/// </summary>
public sealed class MiddlewareTests
{
    [Fact]
    public async Task CorrelationIdMiddleware_NoIncomingId_GeneratesAndSetsResponseHeader()
    {
        var responseFeature = new CapturingResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(responseFeature);

        var correlationIdService = new Mock<ICorrelationIdService>();
        string? capturedId = null;
        correlationIdService.SetupSet(s => s.CorrelationId = It.IsAny<string>())
            .Callback<string>(value => capturedId = value);
        correlationIdService.Setup(s => s.CorrelationId).Returns(() => capturedId ?? string.Empty);

        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context, correlationIdService.Object);
        await responseFeature.FireOnStartingAsync();

        Assert.True(nextCalled);
        Assert.False(string.IsNullOrWhiteSpace(capturedId));
        Assert.True(context.Request.Headers.ContainsKey(AppHeaderNames.CorrelationId));
        Assert.Equal(capturedId, context.Response.Headers[AppHeaderNames.CorrelationId]);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_IncomingId_IsPreserved()
    {
        var responseFeature = new CapturingResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        context.Request.Headers[AppHeaderNames.CorrelationId] = "incoming-id";

        var correlationIdService = new Mock<ICorrelationIdService>();
        string? capturedId = null;
        correlationIdService.SetupSet(s => s.CorrelationId = It.IsAny<string>())
            .Callback<string>(value => capturedId = value);

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context, correlationIdService.Object);
        await responseFeature.FireOnStartingAsync();

        Assert.Equal("incoming-id", capturedId);
        Assert.Equal("incoming-id", context.Response.Headers[AppHeaderNames.CorrelationId]);
    }

    [Fact]
    public async Task GlobalExceptionHandler_GenericException_WritesProblemDetailsWith500()
    {
        var context = BuildContextWithException(new InvalidOperationException("boom"));
        var middleware = new GlobalExceptionHandlerMiddleware(_ => Task.CompletedTask, NullLogger<GlobalExceptionHandlerMiddleware>.Instance);

        await middleware.InvokeAsync(context, BuildCorrelationService());

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Contains("correlationId", await ReadBody(context));
    }

    [Fact]
    public async Task GlobalExceptionHandler_TimeoutException_MapsTo504()
    {
        var context = BuildContextWithException(new TimeoutException("slow"));
        var middleware = new GlobalExceptionHandlerMiddleware(_ => Task.CompletedTask, NullLogger<GlobalExceptionHandlerMiddleware>.Instance);

        await middleware.InvokeAsync(context, BuildCorrelationService());

        Assert.Equal(StatusCodes.Status504GatewayTimeout, context.Response.StatusCode);
    }

    [Fact]
    public async Task GlobalExceptionHandler_NoExceptionFeature_DoesNothing()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionHandlerMiddleware(_ => Task.CompletedTask, NullLogger<GlobalExceptionHandlerMiddleware>.Instance);

        await middleware.InvokeAsync(context, BuildCorrelationService());

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task OpenApiBasicAuth_MalformedHeaderWithoutColon_Returns401NotCrash()
    {
        var context = BuildOpenApiContext("Zm9vYmFy"); // base64("foobar") - no colon separator

        var nextCalled = false;
        var middleware = new OpenApiBasicAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, BuildOpenApiOptions());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task OpenApiBasicAuth_InvalidBase64_Returns401NotCrash()
    {
        var context = BuildOpenApiContext("not-valid-base64!!!");

        var nextCalled = false;
        var middleware = new OpenApiBasicAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, BuildOpenApiOptions());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task OpenApiBasicAuth_EmptyTokenAfterScheme_Returns401NotCrash()
    {
        var context = BuildOpenApiContext(string.Empty); // header is exactly "Basic " (no token)

        var nextCalled = false;
        var middleware = new OpenApiBasicAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, BuildOpenApiOptions());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task OpenApiBasicAuth_ValidCredentials_CallsNext()
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin:secret"));
        var context = BuildOpenApiContext(encoded);

        var nextCalled = false;
        var middleware = new OpenApiBasicAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, BuildOpenApiOptions());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task OpenApiBasicAuth_WrongPassword_Returns401()
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin:wrong"));
        var context = BuildOpenApiContext(encoded);

        var nextCalled = false;
        var middleware = new OpenApiBasicAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, BuildOpenApiOptions());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    private static DefaultHttpContext BuildOpenApiContext(string base64Credentials)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/swagger";
        context.Request.Headers["Authorization"] = $"Basic {base64Credentials}";
        return context;
    }

    private static Microsoft.Extensions.Options.IOptions<Coinwarden.API.Models.Settings.OpenApiSettings> BuildOpenApiOptions()
    {
        return Microsoft.Extensions.Options.Options.Create(new Coinwarden.API.Models.Settings.OpenApiSettings
        {
            Enabled = true,
            AuthSettings = new Coinwarden.API.Models.Settings.OpenApiAuthSettings
            {
                RequireAuth = true,
                Username = "admin",
                Password = "secret"
            }
        });
    }

    [Fact]
    public async Task PathBaseRewriterMiddleware_HeaderInAllowlist_SetsPathBase()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[AppHeaderNames.ForwardedPrefix] = "/api";

        var middleware = BuildPathBaseMiddleware("/api");

        await middleware.InvokeAsync(context);

        Assert.Equal("/api", context.Request.PathBase);
    }

    [Fact]
    public async Task PathBaseRewriterMiddleware_HeaderNotInAllowlist_LeavesPathBaseDefault()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[AppHeaderNames.ForwardedPrefix] = "/evil";

        var middleware = BuildPathBaseMiddleware("/api");

        await middleware.InvokeAsync(context);

        Assert.True(context.Request.PathBase.Value is null or "");
    }

    [Fact]
    public async Task PathBaseRewriterMiddleware_EmptyAllowlist_IgnoresHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[AppHeaderNames.ForwardedPrefix] = "/api";

        var middleware = BuildPathBaseMiddleware();

        await middleware.InvokeAsync(context);

        Assert.True(context.Request.PathBase.Value is null or "");
    }

    [Fact]
    public async Task PathBaseRewriterMiddleware_NoHeader_LeavesPathBaseUnchanged()
    {
        var context = new DefaultHttpContext();

        var middleware = BuildPathBaseMiddleware("/api");

        await middleware.InvokeAsync(context);

        Assert.True(context.Request.PathBase.Value is null or "");
    }

    private static PathBaseRewriterMiddleware BuildPathBaseMiddleware(params string[] allowedPrefixes)
    {
        var settings = Options.Create(new ForwardedHeadersSettings
        {
            AllowedForwardedPrefixes = allowedPrefixes
        });

        return new PathBaseRewriterMiddleware(_ => Task.CompletedTask, settings);
    }

    private static ICorrelationIdService BuildCorrelationService()
    {
        var mock = new Mock<ICorrelationIdService>();
        mock.Setup(s => s.CorrelationId).Returns("test-correlation-id");
        return mock.Object;
    }

    private static DefaultHttpContext BuildContextWithException(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature { Error = exception });
        return context;
    }

    private static async Task<string> ReadBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Minimal response feature that captures OnStarting callbacks so tests can fire them.
    /// </summary>
    private sealed class CapturingResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> callbacks = [];

        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state) => this.callbacks.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task FireOnStartingAsync()
        {
            this.HasStarted = true;
            foreach (var (callback, state) in this.callbacks)
            {
                await callback(state);
            }
        }
    }
}
