using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Miautrix.Mail.Security;

namespace Miautrix.Mail.Application.Mail;

public interface IMessageUidAllocator
{
    Task<uint> AllocateAsync(Guid tenantId, Guid folderId, uint count = 1, CancellationToken cancellationToken = default);
}

/// <summary>Allocates UID blocks atomically in the caller's database transaction.</summary>
public sealed class MessageUidAllocator(AppDbContext db) : IMessageUidAllocator
{
    public async Task<uint> AllocateAsync(Guid tenantId, Guid folderId, uint count = 1, CancellationToken cancellationToken = default)
    {
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(count));
        // UPDATE ... RETURNING is not composable: materialise it as-is (no Single/First translation over it).
        var returned = await db.Database.SqlQuery<long>(
                $"UPDATE folders SET uid_next = uid_next + {count} WHERE id = {folderId} AND tenant_id = {tenantId} RETURNING uid_next - {count} AS \"Value\"")
            .ToListAsync(cancellationToken);
        var firstUid = returned.Count == 1 ? returned[0] : 0;
        if (firstUid <= 0 || firstUid > uint.MaxValue || firstUid + count - 1 > uint.MaxValue)
            throw new ResourceNotFoundException("Folder was not found.");
        return (uint)firstUid;
    }
}
