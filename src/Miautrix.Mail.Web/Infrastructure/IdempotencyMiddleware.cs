using Miautrix.Mail.Web.Contracts;

namespace Miautrix.Mail.Web.Infrastructure;

/// <summary>
/// Enforces <c>Idempotency-Key</c> on mutating verbs. A missing key on a mutating
/// request is a 400. A repeated key replays the previously captured response rather
/// than re-running the mutation, so a retried POST/DELETE cannot double-fire.
/// </summary>
public sealed class IdempotencyMiddleware
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE",
    };

    private readonly RequestDelegate _next;
    private readonly IIdempotencyStore _store;

    public IdempotencyMiddleware(RequestDelegate next, IIdempotencyStore store)
    {
        _next = next;
        _store = store;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!MutatingMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ApiResults.Error(
                context,
                StatusCodes.Status400BadRequest,
                "idempotency_key_required",
                "An Idempotency-Key header is required on mutating requests.").ExecuteAsync(context);
            return;
        }

        var tenantKey = context.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "unknown";

        if (_store.TryGet(tenantKey, idempotencyKey, out var cached) && cached is not null)
        {
            context.Response.StatusCode = cached.StatusCode;
            context.Response.ContentType = cached.ContentType;
            await context.Response.Body.WriteAsync(cached.Body);
            return;
        }

        // Buffer the downstream response so it can be captured for replay.
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            buffer.Position = 0;
            var body = buffer.ToArray();
            _store.Store(tenantKey, idempotencyKey, new CapturedResponse(
                context.Response.StatusCode,
                context.Response.ContentType ?? "application/json",
                body));

            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }
}
