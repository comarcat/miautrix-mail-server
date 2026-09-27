using System;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.ProtocolTests;

internal static class TestDatabaseBootstrap
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void ApplyMigrations()
    {
        // Protocol tests run directly against the shared Postgres database.
        // Keep this in sync with IntegrationTests so new migrations (like
        // malware_detected_action) exist before seeding.
        var connectionString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")
            ?? "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.Migrate();
    }
}
