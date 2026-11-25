using System.Net;

namespace Miautrix.Mail.Application.Transport;

public enum InboundMailPath { Smtp25, CloudflareWebhook }
public enum AuthenticationMode { Off, Monitor, Enforce }
public sealed record InboundAuthenticationInput(IPAddress? ClientIp, string? Helo, string? MailFrom, string RawMessage, InboundMailPath Path);
public sealed record SpfAuthenticationResult(string Result, string? Domain, string? Reason = null);
public sealed record DkimAuthenticationResult(string? Domain, string? Selector, string Result, string? Reason = null);
public sealed record DmarcAuthenticationResult(string Result, string? Domain, string? Policy, string? Disposition, IReadOnlyList<string> AlignedIdentifiers);
public sealed record InboundAuthenticationResult(SpfAuthenticationResult Spf, IReadOnlyList<DkimAuthenticationResult> Dkim, DmarcAuthenticationResult Dmarc, string AuthenticationResults);

public interface IInboundAuthenticationService
{
    Task<InboundAuthenticationResult> EvaluateAsync(InboundAuthenticationInput input, CancellationToken cancellationToken = default);
}

public sealed class InboundAuthenticationOptions
{
    public AuthenticationMode Mode { get; init; } = AuthenticationMode.Monitor;
    public string AuthservId { get; init; } = "localhost";

    public static InboundAuthenticationOptions FromEnvironment()
    {
        var modeText = Environment.GetEnvironmentVariable("MIAUTRIX_INBOUND_AUTH_MODE") ?? "monitor";
        var mode = modeText.ToLowerInvariant() switch
        {
            "off" => AuthenticationMode.Off,
            "monitor" => AuthenticationMode.Monitor,
            "enforce" => AuthenticationMode.Enforce,
            _ => throw new InvalidOperationException("MIAUTRIX_INBOUND_AUTH_MODE must be off, monitor, or enforce.")
        };
        var hostname = Environment.GetEnvironmentVariable("MIAUTRIX_HOSTNAME");
        if (string.IsNullOrWhiteSpace(hostname)) throw new InvalidOperationException("MIAUTRIX_HOSTNAME is required for inbound authentication.");
        return new InboundAuthenticationOptions { Mode = mode, AuthservId = hostname.Trim() };
    }
}
