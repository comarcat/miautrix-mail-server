using System.Security.Cryptography;
using System.Text;

namespace Miautrix.Mail.Web.Infrastructure;

/// <summary>
/// Resolves the acting tenant and user from the <c>X-Tenant-Id</c> and
/// <c>X-User-Id</c> request headers. When headers are omitted, falls back to
/// the default system tenant and default administrator ID.
/// </summary>
public interface IRequestContextAccessor
{
    Guid CurrentTenantId { get; }
    Guid CurrentUserId { get; }
}

public sealed class HeaderRequestContextAccessor : IRequestContextAccessor
{
    private static readonly Guid FallbackTenantId = DeterministicGuid(Guid.Empty, "tenant:default");
    private static readonly Guid FallbackUserId = DeterministicGuid(FallbackTenantId, "user:admin@miautrix.org");

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HeaderRequestContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid CurrentTenantId => ReadGuid("X-Tenant-Id", FallbackTenantId);

    public Guid CurrentUserId => ReadGuid("X-User-Id", FallbackUserId);

    private Guid ReadGuid(string header, Guid fallback)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return fallback;
        }

        var value = context.Request.Headers[header].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (Guid.TryParse(value, out var guid))
        {
            return guid;
        }

        throw new InvalidOperationException($"The '{header}' header must carry a valid tenant/user id.");
    }

    private static Guid DeterministicGuid(Guid namespaceId, string value)
    {
        var input = $"{namespaceId}:{value}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);

        // RFC 4122 variant
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(guidBytes);
    }
}
