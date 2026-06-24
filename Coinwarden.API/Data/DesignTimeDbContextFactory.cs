using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Coinwarden.API.Constants;
using Coinwarden.API.Models.Settings;

namespace Coinwarden.API.Data;

/// <summary>
/// Design-time factory for creating <see cref="DataContext"/> instances.
/// Used by EF Core tools (migrations, scaffolding) when no host is available.
/// </summary>
/// <remarks>
/// EF tools use this factory exclusively, bypassing the application host and
/// <c>AddDatabaseServices</c>. It loads the connection string from the same configuration
/// sources the app uses at run time (<c>appsettings.json</c> → environment-specific →
/// <c>appsettings.Local.json</c> → environment variables) so migrations run against your
/// configured database rather than a hardcoded one. Key Vault is intentionally not loaded here.
/// </remarks>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DataContext>
{
    /// <summary>
    /// Connection string used only when no <c>Postgres:DefaultConnection</c> is configured,
    /// keeping a zero-config local default working.
    /// </summary>
    private const string FallbackConnectionString = "Host=localhost;Database=coinwarden;Username=postgres;Password=postgres";

    /// <inheritdoc />
    public DataContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? EnvironmentNames.Development;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration
            .GetSection(ConfigurationKeys.Postgres)
            .Get<PostgresSettings>()?.DefaultConnection;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = FallbackConnectionString;
        }

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DataContext(options);
    }
}
