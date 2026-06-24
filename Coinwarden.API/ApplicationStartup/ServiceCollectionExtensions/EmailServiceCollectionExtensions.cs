using System;
using Coinwarden.API.Constants;
using Coinwarden.API.Models.Settings;
using Coinwarden.API.Services.Email;
using Coinwarden.API.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Coinwarden.API.ApplicationStartup.ServiceCollectionExtensions;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddEmailServices(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.Configure<EmailSettings>(config.GetSection(ConfigurationKeys.Email));

        services.AddSingleton<IAcsEmailClientFactory, AcsEmailClientFactory>()
            .AddScoped<IEmailService, AcsEmailService>()
            .AddSingleton<IEmailTemplateService, EmailTemplateService>();

        return services;
    }
}