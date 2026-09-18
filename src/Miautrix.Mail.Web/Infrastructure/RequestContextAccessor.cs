namespace Miautrix.Mail.Web.Infrastructure;

/// <summary>
/// Resolves the acting tenant and user from the <c>X-Tenant-Id</c> and
/// <c>X-User-Id</c> request headers. The full authentication layer is out of scope
/// for this surface; these headers are the trust boundary the transport layer uses
/// until session/JWT auth lands on top of the same accessor.
/// </summary>
public interface IRequestContextAccessor
{
    Guid CurrentTenantId { get; }
    Guid CurrentUserId { get; }
}

public sealed class HeaderRequestContextAccessor : IRequestContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HeaderRequestContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid CurrentTenantId => ReadGuid("X-Tenant-Id");

    public Guid CurrentUserId => ReadGuid("X-User-Id");

    private Guid ReadGuid(string header)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            throw new InvalidOperationException("No HTTP context is available.");
        }

        var value = context.Request.Headers[header].FirstOrDefault();
        if (!Guid.TryParse(value, out var guid))
        {
            throw new InvalidOperationException($"The '{header}' header must carry a valid tenant/user id.");
        }

        return guid;
    }
}
