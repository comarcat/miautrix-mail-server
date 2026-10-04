using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Miautrix.Mail.Web.Infrastructure;

/// <summary>
/// In-memory idempotency replay cache. Scoped by (tenant, key-hash) so a repeated
/// <c>Idempotency-Key</c> within a tenant replays the captured response instead of
/// re-executing the mutating operation. Keys are stored hashed, never in plaintext.
/// </summary>
public interface IIdempotencyStore
{
    bool TryGet(string tenantKey, string idempotencyKey, out CapturedResponse? response);
    void Store(string tenantKey, string idempotencyKey, CapturedResponse response);
}

public sealed record CapturedResponse(int StatusCode, string ContentType, byte[] Body);

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, CapturedResponse> _store = new();

    public bool TryGet(string tenantKey, string idempotencyKey, out CapturedResponse? response)
        => _store.TryGetValue(Hash(tenantKey, idempotencyKey), out response);

    public void Store(string tenantKey, string idempotencyKey, CapturedResponse response)
        => _store[Hash(tenantKey, idempotencyKey)] = response;

    private static string Hash(string tenantKey, string idempotencyKey)
    {
        var raw = $"{tenantKey}\n{idempotencyKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}
