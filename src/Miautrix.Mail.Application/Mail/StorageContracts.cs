namespace Miautrix.Mail.Storage;

public sealed record StorageWriteResult(string ContentHash, long SizeBytes, string StoragePath);

public interface IMailStorage
{
    Task<StorageWriteResult> StoreAsync(Stream contentStream, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string contentHash, CancellationToken cancellationToken = default);
    Task DeleteAsync(string contentHash, CancellationToken cancellationToken = default);
}
