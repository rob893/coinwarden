using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Scalar.AspNetCore;
using Coinwarden.API.Constants;
using Coinwarden.API.Extensions;
using Coinwarden.API.Middleware;
using Coinwarden.API.Models.Settings;

namespace Coinwarden.API.ApplicationStartup.ApplicationBuilderExtensions;

public static class OpenApiApplicationBuilderExtensions
{
    public static WebApplication UseAndConfigureOpenApi(this WebApplication app, IConfiguration config, Action<ScalarOptions>? configureScalar = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(config);

        var settings = config.GetSection(ConfigurationKeys.OpenApi).Get<OpenApiSettings>();

        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.Enabled)
        {
            return app;
        }

        var openApiSettings = config.GetSection(ConfigurationKeys.OpenApi).Get<OpenApiSettings>();
        var entryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        var assemblyVersion = entryAssembly?.GetName().Version ?? new Version(0, 0, 0, 0);
        var buildVersion = FileVersionInfo.GetVersionInfo(entryAssembly?.Location ?? Assembly.GetExecutingAssembly().Location).ProductVersion ?? "Unknown build";
        var productName = FileVersionInfo.GetVersionInfo(entryAssembly?.Location ?? Assembly.GetExecutingAssembly().Location).ProductName ?? "Unknown product";
        var environment = config.GetEnvironment();

        // Apply basic auth middleware for protecting the OpenAPI/Scalar endpoints
        app.UseMiddleware<OpenApiBasicAuthMiddleware>();

        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle($"{productName} - {environment} ({assemblyVersion} - Build {buildVersion})")
                .WithTheme(ScalarTheme.BluePlanet)
                .WithOperationTitleSource(OperationTitleSource.Path)
                .AddPreferredSecuritySchemes("Bearer");

            if (configureScalar is not null)
            {
                configureScalar(options);
            }
        });

        return app;
    }
}