using System;
using System.Collections.Generic;
using Coinwarden.API.ApplicationStartup.ServiceCollectionExtensions;
using Coinwarden.API.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Coinwarden.API.Tests.ApplicationStartup.ServiceCollectionExtensions;

/// <summary>
/// Tests for <see cref="AuthenticationServiceCollectionExtensions"/>.
/// </summary>
public sealed class AuthenticationServiceCollectionExtensionsTests
{
    private const string ApiSecret = "this-is-a-sufficiently-long-api-secret-of-at-least-64-characters!!";

    [Fact]
    public void AddAuthenticationServices_ProductionWithoutResolvableGoogleAudience_Throws()
    {
        var config = BuildConfig(EnvironmentNames.Production, googleClientId: string.Empty);

        var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddAuthenticationServices(config));

        Assert.Contains("GoogleOAuthAudiences", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAuthenticationServices_DevelopmentWithoutResolvableGoogleAudience_DoesNotThrow()
    {
        var config = BuildConfig(EnvironmentNames.Development, googleClientId: string.Empty);

        var exception = Record.Exception(() => new ServiceCollection().AddAuthenticationServices(config));

        Assert.Null(exception);
    }

    [Fact]
    public void AddAuthenticationServices_ProductionWithGoogleClientIdFallback_DoesNotThrow()
    {
        var config = BuildConfig(EnvironmentNames.Production, googleClientId: "google-client-id");

        var exception = Record.Exception(() => new ServiceCollection().AddAuthenticationServices(config));

        Assert.Null(exception);
    }

    private static IConfiguration BuildConfig(string environment, string googleClientId)
    {
        var values = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = environment,
            ["Authentication:APISecret"] = ApiSecret,
            ["Authentication:TokenAudience"] = "coinwarden",
            ["Authentication:TokenIssuer"] = "coinwarden",
            ["Authentication:GoogleOAuthClientId"] = googleClientId
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
