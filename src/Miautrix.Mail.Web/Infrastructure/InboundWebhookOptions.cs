using System.Security.Cryptography;
using System.Text;

namespace Miautrix.Mail.Web.Infrastructure;

/// <summary>
/// Shared-secret configuration for the Cloudflare inbound webhook.
/// <para>
/// A null or empty <see cref="Token"/> is a deliberate "closed" state: the endpoint rejects every
/// request rather than turning an unconfigured deployment into an open relay. The token itself is
/// never logged, never returned, and never compared with <c>==</c>.
/// </para>
/// </summary>
public sealed class InboundWebhookOptions
{
    public string? Token { get; init; }

    public bool IsConfigured => !string.IsNullOrEmpty(Token);

    public bool Accepts(string? supplied)
    {
        if (!IsConfigured || string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Token!);
        var actual = Encoding.UTF8.GetBytes(supplied);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
