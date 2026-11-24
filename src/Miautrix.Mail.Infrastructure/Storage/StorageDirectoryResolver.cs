namespace Miautrix.Mail.Infrastructure.Storage;

/// <summary>Resolves the shared blob-store path while refusing deploy-local defaults outside Development.</summary>
public static class StorageDirectoryResolver
{
    public static string Resolve(string? configuredPath, bool isDevelopment, string applicationBaseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath);

        if (!isDevelopment)
            throw new InvalidOperationException("MIAUTRIX_STORAGE_DIR is required outside Development; refusing to store mail inside the application directory.");

        return Path.Combine(applicationBaseDirectory, "mail_data");
    }
}
