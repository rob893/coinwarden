
using System;
using System.Reflection;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Coinwarden.API.Extensions;

namespace Coinwarden.API.ApplicationStartup.ServiceCollectionExtensions;

/// <summary>
/// Options for configuring App Insights services.
/// </summary>
public sealed record AppInsightsOptions
{
    /// <summary>
    /// Whether to use <see cref="DefaultAzureCredential" /> for authenticating with Azure Monitor.
    /// If false, no authentication is used (meaning the app insights instance must allow unauthenticated ingestion).
    /// </summary>
    public bool UseDefaultAzureCredential { get; init; } = true;

    /// <summary>
    /// Action to configure OpenTelemetry logger options.
    /// </summary>
    public Action<OpenTelemetryLoggerOptions>? ConfigureLoggerOptions { get; init; }

    /// <summary>
    /// Action to configure OpenTelemetry meter provider.
    /// </summary>
    public Action<MeterProviderBuilder>? ConfigureMeterProvider { get; init; }

    /// <summary>
    /// Action to configure Azure Monitor options.
    /// </summary>
    public Action<AzureMonitorOptions>? ConfigureAzureMonitorOptions { get; init; }

    /// <summary>
    /// Action to configure OpenTelemetry tracer provider.
    /// </summary>
    public Action<TracerProviderBuilder>? ConfigureTracerProvider { get; init; }

    /// <summary>
    /// Action to configure AspNetCore trace instrumentation options.
    /// </summary>
    public Action<AspNetCoreTraceInstrumentationOptions>? ConfigureAspNetCoreTraceInstrumentationOptions { get; init; }
}

/// <summary>
/// Extension helpers for wiring OpenTelemetry and Azure Monitor instrumentation.
/// </summary>
public static class AppInsightsServiceCollectionExtensions
{
    private const string DefaultServiceName = "Coinwarden";

    private const string AppInsightsConnectionStringKey = "ApplicationInsights:ConnectionString";

    private const string AppInsightsConnectionStringKeyEnvVar = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    /// <summary>
    /// Configures OpenTelemetry and Azure Monitor logging for the application.
    /// </summary>
    /// <param name="services">Dependency injection service collection.</param>
    /// <param name="configuration">Application configuration set.</param>
    /// <param name="appInsightsOptions">Optional App Insights configuration.</param>
    /// <returns>The provided service collection.</returns>
    public static IServiceCollection AddAppInsightsServices(this IServiceCollection services, IConfiguration configuration, AppInsightsOptions? appInsightsOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var appInsightsConnectionString = configuration[AppInsightsConnectionStringKeyEnvVar]
            ?? configuration[AppInsightsConnectionStringKey]
            ?? Environment.GetEnvironmentVariable(AppInsightsConnectionStringKeyEnvVar);
        var entryAssembly = Assembly.GetEntryAssembly();
        var serviceName = entryAssembly?.GetName().Name ?? DefaultServiceName;

        if (string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            using var factory = LoggerFactory.Create(lb => lb.AddConsole());
            var logger = factory.CreateLogger(serviceName);
            logger.LogWarning("No configuration set for {ConfigKey}. Skipping OpenTelemetry registration.", AppInsightsConnectionStringKey);

            return services;
        }

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;

                if (appInsightsOptions?.ConfigureLoggerOptions != null)
                {
                    appInsightsOptions.ConfigureLoggerOptions(options);
                }
            });
        });

        var serviceVersion = entryAssembly?.GetName().Version?.ToString();
        var environmentName = configuration.GetEnvironment();

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: $"{serviceName} - {environmentName}", // note this is a fallback for when the app is not runnning in azure (like local dev)
                serviceVersion: serviceVersion))
            .WithMetrics(metrics =>
            {
                if (appInsightsOptions?.ConfigureMeterProvider != null)
                {
                    appInsightsOptions.ConfigureMeterProvider(metrics);
                }
            })
            .WithTracing(tracing =>
            {
                if (appInsightsOptions?.ConfigureTracerProvider != null)
                {
                    appInsightsOptions.ConfigureTracerProvider(tracing);
                }
            })
            .UseAzureMonitor(options =>
            {
                options.ConnectionString = appInsightsConnectionString;
                options.SamplingRatio = 1.0f;

                if (appInsightsOptions?.UseDefaultAzureCredential == true)
                {
                    options.Credential = new DefaultAzureCredential();
                }

                if (appInsightsOptions?.ConfigureAzureMonitorOptions != null)
                {
                    appInsightsOptions.ConfigureAzureMonitorOptions(options);
                }
            });

        services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
        {
            options.Filter = context =>
            {
                // Filter out ping and health endpoints to reduce noise in App Insights
                var path = context.Request.Path.Value ?? string.Empty;

                if (HttpMethods.IsGet(context.Request.Method) && (string.IsNullOrEmpty(path) || path == "/"))
                {
                    return false;
                }

                if (path.Equals("/health/liveness", StringComparison.OrdinalIgnoreCase) || path.Equals("/health", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            };

            // Resolve route template parameters in span names so App Insights
            // shows actual paths instead of raw route templates.
            options.EnrichWithHttpResponse = (activity, response) =>
            {
                var routeValues = response.HttpContext.Request.RouteValues;
                if (routeValues.Count == 0)
                {
                    return;
                }

                var displayName = activity.DisplayName;
                foreach (var (key, value) in routeValues)
                {
                    if (value is null)
                    {
                        continue;
                    }

                    var resolved = value.ToString()!;

                    // Replace {key:constraint} patterns (e.g., {version:apiVersion} → 2)
                    var token = $"{{{key}:";
                    int idx;
                    while ((idx = displayName.IndexOf(token, StringComparison.OrdinalIgnoreCase)) >= 0)
                    {
                        var end = displayName.AsSpan(idx).IndexOf('}');
                        if (end < 0)
                        {
                            break;
                        }

                        end += idx;

                        displayName = string.Concat(displayName.AsSpan(0, idx), resolved, displayName.AsSpan(end + 1));
                    }

                    // Replace simple {key} patterns
                    displayName = displayName.Replace($"{{{key}}}", resolved, StringComparison.OrdinalIgnoreCase);
                }

                if (displayName != activity.DisplayName)
                {
                    activity.DisplayName = displayName;

                    // Also update http.route so App Insights shows the resolved path as the operation name
                    var spaceIdx = displayName.AsSpan().IndexOf(' ');
                    if (spaceIdx >= 0)
                    {
                        activity.SetTag("http.route", displayName[(spaceIdx + 1)..]);
                    }
                }
            };

            if (appInsightsOptions?.ConfigureAspNetCoreTraceInstrumentationOptions != null)
            {
                appInsightsOptions.ConfigureAspNetCoreTraceInstrumentationOptions(options);
            }
        });

        return services;
    }
}