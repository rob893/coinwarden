using System;
using Coinwarden.API.Constants;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;

namespace Coinwarden.API.ApplicationStartup.ApplicationBuilderExtensions;

public static class EndpointsApplicationBuilderExtensions
{
    public static WebApplication UseAndConfigureEndpoints(this WebApplication app, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(config);

        app.MapHealthChecks(ApplicationSettings.HealthCheckEndpoint, new HealthCheckOptions()
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapHealthChecks(ApplicationSettings.LivenessHealthCheckEndpoint, new HealthCheckOptions()
        {
            Predicate = (check) => !check.Tags.Contains(HealthCheckTags.Dependency),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapControllers();

        return app;
    }
}