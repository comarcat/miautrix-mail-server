namespace Miautrix.Mail.Web.Infrastructure;

public interface IRequestContextAccessor
{
    Guid CurrentTenantId { get; }
    Guid CurrentUserId { get; }
}

public sealed class HeaderRequestContextAccessor : IRequestContextAccessor
{
    private readonly IHttpContextAccessor _http;
    public HeaderRequestContextAccessor(IHttpContextAccessor http) => _http = http;
    public Guid CurrentTenantId => Get("tenant_id", "X-Tenant-Id");
    public Guid CurrentUserId => Get("user_id", "X-User-Id");
    private Guid Get(string key, string header)
    {
        var context = _http.HttpContext ?? throw new InvalidOperationException("No authenticated request context.");
        if (!context.Items.TryGetValue(key, out var value) || value is not Guid id || id == Guid.Empty)
            throw new InvalidOperationException("No authenticated request context.");
        if (context.Request.Headers.TryGetValue(header, out var supplied) &&
            (!Guid.TryParse(supplied, out var parsed) || parsed != id))
            throw new Miautrix.Mail.Security.ResourceNotFoundException("Resource not found.");
        return id;
    }
}
