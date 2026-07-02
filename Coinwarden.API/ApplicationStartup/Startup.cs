using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coinwarden.API.ApplicationStartup.ApplicationBuilderExtensions;
using Coinwarden.API.ApplicationStartup.ServiceCollectionExtensions;
using Coinwarden.API.Constants;
using Coinwarden.API.Extensions;
using Coinwarden.API.Middleware;
using Coinwarden.API.Models.Settings;

namespace Coinwarden.API.ApplicationStartup;

public sealed class Startup
{
    private readonly IConfiguration configuration;

    public Startup(IConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (this.configuration.GetEnvironment() != EnvironmentNames.Development)
        {
            services.AddAppInsightsServices(this.configuration);
        }

        services.AddControllerServices()
            .AddHealthCheckServices()
            .AddMemoryCache()
            .AddIdentityServices()
            .AddRateLimiterServices(this.configuration)
            .AddCoreServices(this.configuration)
            .AddEmailServices(this.configuration)
            .AddAuthenticationServices(this.configuration)
            .AddDatabaseServices(this.configuration)
            .AddRepositoryServices()
            .AddDomainServices()
            .AddOpenApiServices(this.configuration)
            .AddCors()
            .AddHttpClient();
    }

    public void Configure(WebApplication app, IWebHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(env);

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseGlobalExceptionHandlerMiddleware()
            .UseRouting()
            .UseHsts()
            .UseHttpsRedirection()
            .UseSecurityHeadersMiddleware()
            .UseCorrelationIdMiddleware()
            .UseForwardedHeaders(BuildForwardedHeadersOptions(this.configuration))
            .UseMiddleware<PathBaseRewriterMiddleware>()
            .UseAndConfigureCors(this.configuration)
            .UseAuthentication()
            .UseAuthorization()
            .UseMiddleware<LoggingScopeMiddleware>() // Ensure this is after UseAuthentication and UseAuthorization to capture user information.
            .UseRateLimiter(); // Ensure this is after UseAuthentication and UseAuthorization to apply rate limiting based on user identity.

        app.UseAndConfigureOpenApi(this.configuration)
            .UseAndConfigureEndpoints(this.configuration);
    }

    // ForwardedHeaders middleware rewrites Connection.RemoteIpAddress, Request.Scheme, and
    // Request.Host based on X-Forwarded-* headers, but only when the immediate connection comes
    // from a trusted proxy. We pin the trust list to loopback by default (the API sits behind an
    // on-host nginx in production) and allow ops to extend it via configuration without redeploys.
    // Without this, malicious clients could spoof their source IP for the rate limiter and any
    // other middleware that reads RemoteIpAddress.
    private static ForwardedHeadersOptions BuildForwardedHeadersOptions(IConfiguration configuration)
    {
        var settings = configuration.GetSection(ConfigurationKeys.ForwardedHeaders).Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.All,
            ForwardLimit = settings.ForwardLimit
        };

        // Replace the framework defaults with an explicit, documented trust list. Loopback is
        // always included so the on-host nginx reverse proxy keeps working.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("127.0.0.0"), 8));
        options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.IPv6Loopback, 128));

        foreach (var proxy in settings.KnownProxies)
        {
            if (IPAddress.TryParse(proxy, out var address))
            {
                options.KnownProxies.Add(address);
            }
        }

        foreach (var network in settings.KnownNetworks)
        {
            if (System.Net.IPNetwork.TryParse(network, out var ipNetwork))
            {
                options.KnownIPNetworks.Add(ipNetwork);
            }
        }

        return options;
    }
}