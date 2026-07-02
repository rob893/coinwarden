using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Coinwarden.API.Constants;
using Coinwarden.API.Core;
using Coinwarden.API.Extensions;
using Coinwarden.API.Models.Settings;
using Coinwarden.API.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Coinwarden.API.ApplicationStartup.ServiceCollectionExtensions;

public static class AuthenticationServiceCollectionExtensions
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthTokenCookieService, AuthTokenCookieService>();
        services.AddScoped<IOAuthFlowCookieService, OAuthFlowCookieService>();
        services.AddScoped<IGitHubOAuthService, GitHubOAuthService>();
        services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHttpContextAccessor();

        services.Configure<AuthenticationSettings>(config.GetSection(ConfigurationKeys.Authentication));

        var authSettings = config.GetSection(ConfigurationKeys.Authentication)?.Get<AuthenticationSettings>() ??
            throw new InvalidOperationException($"Missing {ConfigurationKeys.Authentication} section in configuration.");

        // HMAC-SHA512 requires a key of at least 512 bits (64 bytes). Validate the signing secret here
        // so a too-short secret fails fast at startup on the validation path too — not only when a token
        // is first issued (JwtTokenService) — preventing an under-strength, forgeable key from deploying.
        if (string.IsNullOrEmpty(authSettings.APISecret) || Encoding.UTF8.GetByteCount(authSettings.APISecret) < 64)
        {
            throw new InvalidOperationException(
                $"{ConfigurationKeys.Authentication}:{nameof(AuthenticationSettings.APISecret)} must be at least 64 bytes (512 bits) for HMAC-SHA512 token signing.");
        }

        if (config.GetEnvironment() != EnvironmentNames.Development &&
            GoogleOAuthService.ResolveGoogleOAuthAudiences(authSettings).Count == 0)
        {
            throw new InvalidOperationException(
                $"{ConfigurationKeys.Authentication}:{nameof(AuthenticationSettings.GoogleOAuthAudiences)} must contain at least one audience or {nameof(AuthenticationSettings.GoogleOAuthClientId)} must be configured outside Development.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Set token validation options. These will be used when validating all tokens.
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authSettings.APISecret)),
                    RequireSignedTokens = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ValidAudience = authSettings.TokenAudience,
                    ValidIssuers = [authSettings.TokenIssuer]
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        var errorMessage = string.IsNullOrWhiteSpace(context.ErrorDescription) ? context.Error : $"{context.Error}. {context.ErrorDescription}.";

                        var problem = new ProblemDetailsWithErrors(errorMessage ?? "Invalid token", StatusCodes.Status401Unauthorized, context.Request);

                        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, jsonOptions));
                    },
                    OnForbidden = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";

                        var problem = new ProblemDetailsWithErrors("Forbidden", StatusCodes.Status403Forbidden, context.Request);

                        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, jsonOptions));
                    },
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception is SecurityTokenExpiredException)
                        {
                            context.Response.Headers[AppHeaderNames.TokenExpired] = "true";
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicyName.RequireAdminRole, policy => policy.RequireRole(UserRoleName.Admin));
            options.AddPolicy(AuthorizationPolicyName.RequireUserRole, policy => policy.RequireRole(UserRoleName.User));
        });

        return services;
    }
}