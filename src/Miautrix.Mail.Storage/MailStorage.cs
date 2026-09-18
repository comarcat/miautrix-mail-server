using System.Security.Cryptography;

namespace Miautrix.Mail.Storage;

public record StorageWriteResult(string ContentHash, long SizeBytes, string StoragePath);

public interface IMailStorage
{
    Task<StorageWriteResult> StoreAsync(Stream contentStream, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string contentHash, CancellationToken cancellationToken = default);
    Task DeleteAsync(string contentHash, CancellationToken cancellationToken = default);
}

public sealed class FileSystemMailStorage : IMailStorage
{
    private const int BufferSize = 81920; // 80 KB bounded streaming buffer
    private readonly string _baseDirectory;

    public FileSystemMailStorage(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? Path.Combine(AppContext.BaseDirectory, "mail_data");
        Directory.CreateDirectory(_baseDirectory);
    }

    public async Task<StorageWriteResult> StoreAsync(Stream contentStream, CancellationToken cancellationToken = default)
    {
        var tempFilePath = Path.Combine(_baseDirectory, $"temp_{Guid.NewGuid():N}.tmp");
        string hashHex;
        long totalBytes = 0;

        try
        {
            using (var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                await using (var fileStream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                {
                    var buffer = new byte[BufferSize];
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        sha256.AppendData(buffer, 0, bytesRead);
                        totalBytes += bytesRead;
                    }
                }

                var hashBytes = sha256.GetHashAndReset();
                hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            var targetDirectory = Path.Combine(_baseDirectory, hashHex[..2], hashHex[2..4]);
            Directory.CreateDirectory(targetDirectory);
            var targetFilePath = Path.Combine(targetDirectory, hashHex);

            // Deduplication: If already stored with same content hash, discard temp file
            if (File.Exists(targetFilePath))
            {
                File.Delete(tempFilePath);
            }
            else
            {
                File.Move(tempFilePath, targetFilePath, overwrite: true);
            }

            return new StorageWriteResult(hashHex, totalBytes, targetFilePath);
        }
        catch
        {
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentHash) || contentHash.Length < 4)
        {
            return Task.FromResult<Stream?>(null);
        }

        var normalizedHash = contentHash.Trim().ToLowerInvariant();
        var targetFilePath = Path.Combine(_baseDirectory, normalizedHash[..2], normalizedHash[2..4], normalizedHash);

        if (!File.Exists(targetFilePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream fileStream = new FileStream(targetFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        return Task.FromResult<Stream?>(fileStream);
    }

    public Task<bool> ExistsAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentHash) || contentHash.Length < 4)
        {
            return Task.FromResult(false);
        }

        var normalizedHash = contentHash.Trim().ToLowerInvariant();
        var targetFilePath = Path.Combine(_baseDirectory, normalizedHash[..2], normalizedHash[2..4], normalizedHash);
        return Task.FromResult(File.Exists(targetFilePath));
    }

    public Task DeleteAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentHash) || contentHash.Length < 4)
        {
            return Task.CompletedTask;
        }

        var normalizedHash = contentHash.Trim().ToLowerInvariant();
        var targetFilePath = Path.Combine(_baseDirectory, normalizedHash[..2], normalizedHash[2..4], normalizedHash);

        if (File.Exists(targetFilePath))
        {
            try { File.Delete(targetFilePath); } catch { }
        }

        return Task.CompletedTask;
    }
}
