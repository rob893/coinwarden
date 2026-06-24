using System.Threading;
using System.Threading.Tasks;

namespace Coinwarden.API.Data;

/// <summary>
/// Interface for the database seeder.
/// </summary>
public interface IDatabaseSeeder
{
    /// <summary>
    /// Seeds the database with optional data operations.
    /// </summary>
    /// <param name="seedData">Whether to seed initial data.</param>
    /// <param name="clearCurrentData">Whether to clear existing data first.</param>
    /// <param name="applyMigrations">Whether to apply pending migrations.</param>
    /// <param name="dropDatabase">Whether to drop and recreate the schema first.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SeedDatabaseAsync(bool seedData, bool clearCurrentData, bool applyMigrations, bool dropDatabase, CancellationToken cancellationToken = default);
}
