using System.Net;

namespace Miautrix.Mail.Web.Infrastructure;

/// <summary>Builds a discovery rate-limit key without trusting forwarded headers from public peers.</summary>
public static class DiscoveryClientKey
{
    public static string From(HttpContext context)
    {
        var peer = context.Connection.RemoteIpAddress;
        if (peer is null) return "unknown";

        // Nginx is the only configured reverse proxy today and connects over loopback.
        // Ignore X-Forwarded-For from every other peer to prevent clients forging buckets.
        if (IPAddress.IsLoopback(peer))
        {
            var chain = context.Request.Headers["X-Forwarded-For"].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = chain.Length - 1; i >= 0; i--)
            {
                if (IPAddress.TryParse(chain[i], out var address) && !IPAddress.IsLoopback(address))
                    return address.ToString();
            }
        }

        return peer.ToString();
    }
}
