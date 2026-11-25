using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Miautrix.Mail.IntegrationTests;

/// <summary>Provides isolated, migrated PostgreSQL databases for tests that depend on PostgreSQL behavior.</summary>
public sealed class IsolatedPostgresFixture : IAsyncLifetime
{
    private readonly string _serverConnection = Environment.GetEnvironmentVariable("MIAUTRIX_TEST_SERVER_CONNECTION")
        ?? throw new InvalidOperationException("MIAUTRIX_TEST_SERVER_CONNECTION environment variable is required.");
    private readonly List<string> _databases = [];

    public string PrimaryConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        PrimaryConnectionString = await CreateDatabaseAsync();
    }

    public async Task<string> CreateDatabaseAsync()
    {
        var database = $"miautrix_test_fixtures_{Guid.NewGuid():N}";
        var server = new NpgsqlConnectionStringBuilder(_serverConnection);
        if ((server.Database ?? string.Empty).Contains("-pro", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Test server connection must not target a production database.");
        }

        await using (var connection = new NpgsqlConnection(_serverConnection))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{database}\"";
            await command.ExecuteNonQueryAsync();
        }

        _databases.Add(database);
        var connectionString = new NpgsqlConnectionStringBuilder(_serverConnection) { Database = database }.ConnectionString;
        await using var db = CreateContext(connectionString);
        await db.Database.MigrateAsync();
        return connectionString;
    }

    public static AppDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_serverConnection);
        await connection.OpenAsync();
        foreach (var database in _databases)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)";
            await command.ExecuteNonQueryAsync();
        }
    }
}
