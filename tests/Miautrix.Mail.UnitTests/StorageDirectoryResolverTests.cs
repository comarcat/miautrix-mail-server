using Miautrix.Mail.Infrastructure.Storage;

namespace Miautrix.Mail.UnitTests;

public sealed class StorageDirectoryResolverTests
{
    [Fact]
    public void Missing_path_fails_outside_development()
    {
        var error = Assert.Throws<InvalidOperationException>(() => StorageDirectoryResolver.Resolve(null, false, "/app"));
        Assert.Contains("MIAUTRIX_STORAGE_DIR", error.Message);
    }

    [Fact]
    public void Missing_path_uses_application_fallback_in_development()
    {
        Assert.Equal(Path.Combine("/app", "mail_data"), StorageDirectoryResolver.Resolve(" ", true, "/app"));
    }

    [Fact]
    public void Configured_path_is_normalized()
    {
        Assert.Equal(Path.GetFullPath("data/mail"), StorageDirectoryResolver.Resolve("data/mail", false, "/app"));
    }
}
