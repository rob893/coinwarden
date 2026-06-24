using System;
using Coinwarden.API.Core.HealthChecks;
using Coinwarden.API.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Coinwarden.API.ApplicationStartup.ServiceCollectionExtensions;

public static class HealthCheckServiceCollectionExtensions
{
    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks()
            .AddDbContextCheck<DataContext>()
            .AddCheck<VersionHealthCheck>(
                name: "version",
                failureStatus: HealthStatus.Degraded,
                tags: ["version"]);

        return services;
    }
}