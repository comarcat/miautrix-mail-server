using System.Runtime.CompilerServices;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.IntegrationTests;

internal static class TestDatabaseBootstrap
{
    [ModuleInitializer]
    internal static void ApplyMigrations()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;

        using var context = new AppDbContext(options);
        context.Database.Migrate();
    }

    private static string GetConnectionString()
    {
        var conn = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException("MIAUTRIX_DB_CONNECTION environment variable is required.");
        }

        return conn;
    }
}
