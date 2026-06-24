using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using CommandLine;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Coinwarden.API.ApplicationStartup;
using Coinwarden.API.Constants;
using Coinwarden.API.Core;
using Coinwarden.API.Data;
using Coinwarden.API.Extensions;

namespace Coinwarden.API;

/// <summary>
/// Application entry point.
/// </summary>
public static class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var keyVaultUrl = builder.Configuration[ConfigurationKeys.KeyVaultUrl] ?? throw new InvalidOperationException("KeyVaultUrl not found in configuration.");

        if (builder.Configuration.GetEnvironment() != EnvironmentNames.Development)
        {
            builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential(), new PrefixKeyVaultSecretManager(["Coinwarden", "All"]));
        }

        builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

        var startup = new Startup(builder.Configuration);
        startup.ConfigureServices(builder.Services);

        var app = builder.Build();
        startup.Configure(app, app.Environment);

        using (var scope = app.Services.CreateScope())
        {
            var serviceProvider = scope.ServiceProvider;
            var seeder = serviceProvider.GetRequiredService<IDatabaseSeeder>();

            if (args.Contains(CommandLineOptions.SeedArgument, StringComparer.OrdinalIgnoreCase))
            {
                await Parser.Default.ParseArguments<CommandLineOptions>(args)
                    .WithParsedAsync(async o =>
                    {
                        var logger = serviceProvider.GetRequiredService<ILogger<DatabaseSeeder>>();
                        var seederPassword = app.Configuration.GetValue<string>("SeederPassword") ?? throw new InvalidOperationException("Seeder password not found in configuration.");

                        if (o.Password != null && o.Password == seederPassword)
                        {
                            var migrate = args.Contains(CommandLineOptions.MigrateArgument, StringComparer.OrdinalIgnoreCase);
                            var clearData = args.Contains(CommandLineOptions.ClearDataArgument, StringComparer.OrdinalIgnoreCase);
                            var seedData = args.Contains(CommandLineOptions.SeedDataArgument, StringComparer.OrdinalIgnoreCase);
                            var dropDatabase = args.Contains(CommandLineOptions.DropArgument, StringComparer.OrdinalIgnoreCase);

                            logger.LogInformation("Seeding database:\nDrop database: {DropDatabase}\nApply Migrations: {Migrate}\nClear old data: {ClearData}\nSeed new data: {SeedData}", dropDatabase, migrate, clearData, seedData);
                            logger.LogWarning("Are you sure you want to apply these actions to the database in that order? Only 'yes' will continue.");

                            var answer = Console.ReadLine();

                            if (answer == "yes")
                            {
                                await seeder.SeedDatabaseAsync(seedData, clearData, migrate, dropDatabase, CancellationToken.None);
                            }
                            else
                            {
                                logger.LogWarning("Aborting database seed process...");
                            }
                        }
                        else
                        {
                            logger.LogWarning("Invalid seeder password");
                        }
                    });
            }
        }

        await app.RunAsync();
    }
}
