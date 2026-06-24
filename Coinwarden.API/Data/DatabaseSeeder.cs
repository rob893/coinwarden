using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Constants;
using Coinwarden.API.Extensions;
using Coinwarden.API.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coinwarden.API.Data;

/// <summary>
/// Seeds the database with initial roles and an admin user.
/// </summary>
public sealed class DatabaseSeeder : IDatabaseSeeder
{
    private readonly DataContext context;

    private readonly UserManager<User> userManager;

    private readonly RoleManager<Role> roleManager;

    private readonly ILogger<DatabaseSeeder> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseSeeder"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="userManager">The Identity user manager.</param>
    /// <param name="roleManager">The Identity role manager.</param>
    /// <param name="logger">The logger.</param>
    public DatabaseSeeder(
        DataContext context,
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        ILogger<DatabaseSeeder> logger)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        this.roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task SeedDatabaseAsync(bool seedData, bool clearCurrentData, bool applyMigrations, bool dropDatabase, CancellationToken cancellationToken = default)
    {
        if (dropDatabase)
        {
            await ResetSchemaAsync(this.context.Database.GetDbConnection(), cancellationToken);
        }

        if (applyMigrations)
        {
            await this.context.Database.MigrateAsync(cancellationToken);
        }

        if (clearCurrentData)
        {
            await this.ClearAllDataAsync(cancellationToken);
        }

        if (seedData)
        {
            await this.SeedRolesAsync(cancellationToken);
            await this.SeedAdminUserAsync(cancellationToken);

            await FixSequenceAsync(this.context, "AspNetRoles", "Id", cancellationToken);
            await FixSequenceAsync(this.context, "AspNetUsers", "Id", cancellationToken);
        }
    }

    private static async Task FixSequenceAsync(DbContext dbContext, string table, string idColumn, CancellationToken cancellationToken = default)
    {
        var sql = $@"
        SELECT setval(
            pg_get_serial_sequence('""{table}""', '{idColumn}'),
            COALESCE((SELECT MAX(""{idColumn}"") FROM ""{table}""), 0) + 1,
            false
        );";

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task ResetSchemaAsync(DbConnection connection, CancellationToken ct)
    {
        await connection.OpenAsync(ct);

        try
        {
            await using var cmd1 = connection.CreateCommand();
            cmd1.CommandText = "DROP SCHEMA IF EXISTS public CASCADE;";
            await cmd1.ExecuteNonQueryAsync(ct);

            await using var cmd2 = connection.CreateCommand();
            cmd2.CommandText = "CREATE SCHEMA public;";
            await cmd2.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private async Task ClearAllDataAsync(CancellationToken cancellationToken = default)
    {
        this.context.Notes.Clear();
        this.context.RefreshTokens.Clear();
        this.context.LinkedAccounts.Clear();
        this.context.Users.Clear();
        this.context.Roles.Clear();

        await this.context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken = default)
    {
        if (await this.context.Roles.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var roleName in new[] { UserRoleName.Admin, UserRoleName.User })
        {
            await this.roleManager.CreateAsync(new Role { Name = roleName });
        }
    }

    private async Task SeedAdminUserAsync(CancellationToken cancellationToken = default)
    {
        if (await this.context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var adminUser = new User
        {
            Id = ApplicationSettings.SystemUserId,
            UserName = "admin",
            Email = "admin@coinwarden.local",
            EmailConfirmed = true,
            Created = DateTimeOffset.UtcNow,
            LastPasswordChange = DateTimeOffset.UtcNow,
            LastEmailChange = DateTimeOffset.UtcNow,
            LastUsernameChange = DateTimeOffset.UtcNow
        };

        this.logger.LogInformation("Seeding admin user...");
        var result = await this.userManager.CreateAsync(adminUser);

        if (!result.Succeeded)
        {
            this.logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors));
            return;
        }

        await this.userManager.AddToRoleAsync(adminUser, UserRoleName.User);
        await this.userManager.AddToRoleAsync(adminUser, UserRoleName.Admin);
    }
}
