using System;
using Miautrix.Mail.Persistence;
using Xunit;

namespace Miautrix.Mail.UnitTests;

public sealed class DatabaseConnectionFailClosedTests
{
    [Fact]
    public void AppDbContextFactory_When_MIAUTRIX_DB_CONNECTION_is_unset_Throws_InvalidOperationException()
    {
        var original = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("MIAUTRIX_DB_CONNECTION", null);
            var factory = new AppDbContextFactory();
            var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
            Assert.Contains("MIAUTRIX_DB_CONNECTION", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MIAUTRIX_DB_CONNECTION", original);
        }
    }

    [Fact]
    public void AppDbContextFactory_When_MIAUTRIX_DB_CONNECTION_is_whitespace_Throws_InvalidOperationException()
    {
        var original = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("MIAUTRIX_DB_CONNECTION", "   ");
            var factory = new AppDbContextFactory();
            var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
            Assert.Contains("MIAUTRIX_DB_CONNECTION", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MIAUTRIX_DB_CONNECTION", original);
        }
    }
}
