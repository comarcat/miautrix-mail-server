using System.Diagnostics;

namespace Miautrix.Mail.Web.Infrastructure;

/// <summary>
/// Propagates an <c>X-Request-Id</c> header when the client supplies one, otherwise
/// adopts the ambient trace identifier. The value is echoed back on the response so
/// a client can correlate its own logs with server-side structured logs.
/// </summary>
public sealed class RequestIdMiddleware
{
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers["X-Request-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(incoming))
        {
            context.TraceIdentifier = incoming;
        }
        else
        {
            context.TraceIdentifier = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        }

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("X-Request-Id"))
            {
                context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
