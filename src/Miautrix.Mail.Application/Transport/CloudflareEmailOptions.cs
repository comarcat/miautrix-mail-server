using System;

namespace Miautrix.Mail.Application.Transport;

/// <summary>
/// Server-wide Cloudflare credentials, parsed once at boot.
/// <para>
/// Per-domain values (<c>CloudflareZoneId</c>, <c>CloudflareWorkerUrl</c>) deliberately live on the
/// domain row rather than here — that is what makes Cloudflare a per-domain choice. Only the API
/// token is global, because one token covers every zone the operator owns.
/// </para>
/// </summary>
public sealed class CloudflareEmailOptions
{
    public string? ApiToken { get; init; }

    public string ApiBase { get; init; } = "https://api.cloudflare.com/client/v4";

    public bool IsConfigured => !string.IsNullOrEmpty(ApiToken);

    /// <summary>
    /// Reads the two Cloudflare variables from the environment.
    /// <para>
    /// The token is deliberately allowed to be absent here rather than crashing the host: the
    /// option may simply not be in use yet, and the dispatcher reports the mismatch loudly if a
    /// domain is switched to Cloudflare while no token exists. A defaulted empty string is never
    /// written into a request — <see cref="IsConfigured"/> gates every send.
    /// </para>
    /// </summary>
    public static CloudflareEmailOptions FromEnvironment()
    {
        var token = Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN");
        var apiBase = Environment.GetEnvironmentVariable("CLOUDFLARE_API_BASE");

        return new CloudflareEmailOptions
        {
            ApiToken = string.IsNullOrWhiteSpace(token) ? null : token.Trim(),
            ApiBase = string.IsNullOrWhiteSpace(apiBase)
                ? "https://api.cloudflare.com/client/v4"
                : apiBase.Trim().TrimEnd('/')
        };
    }
}
