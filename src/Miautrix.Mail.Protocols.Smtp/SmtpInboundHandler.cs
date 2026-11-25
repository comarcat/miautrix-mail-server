using Miautrix.Mail.Domain;
using Miautrix.Mail.Queue;
using Miautrix.Mail.Application.Transport;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Miautrix.Mail.Protocols.Smtp;

public interface ISmtpInboundHandler
{
    SmtpResponse HandleRcptTo(string recipientAddress, out Guid tenantId);

    Task<(SmtpResponse Response, SmtpQueueItem? QueueItem)> HandleDataAsync(
        Guid tenantId,
        string sender,
        string recipient,
        string rawMessage,
        string? subject = null,
        CancellationToken cancellationToken = default,
        InboundMailPath path = InboundMailPath.Smtp25,
        IPAddress? clientIp = null,
        string? helo = null,
        string? cloudflareAuthenticationResults = null,
        InboundAuthenticationResult? precomputedAuthentication = null);
}

public sealed class SmtpInboundHandler : ISmtpInboundHandler
{
    private readonly ISmtpDomainValidator _domainValidator;
    private readonly ISmtpQueueManager _queueManager;
    private readonly IInboundAuthenticationService? _authentication;
    private readonly InboundAuthenticationOptions? _authenticationOptions;
    private readonly ILogger<SmtpInboundHandler>? _logger;

    public SmtpInboundHandler(
        ISmtpDomainValidator domainValidator,
        ISmtpQueueManager queueManager,
        IInboundAuthenticationService? authentication = null,
        InboundAuthenticationOptions? authenticationOptions = null,
        ILogger<SmtpInboundHandler>? logger = null)
    {
        _domainValidator = domainValidator;
        _queueManager = queueManager;
        _authentication = authentication;
        _authenticationOptions = authenticationOptions;
        _logger = logger;
    }

    public SmtpResponse HandleRcptTo(string recipientAddress, out Guid tenantId)
    {
        if (!_domainValidator.IsDomainLocal(recipientAddress, out tenantId))
        {
            // Open-relay protection: reject recipient outside tenant domains
            return SmtpResponse.RelayAccessDenied;
        }

        return SmtpResponse.Ok;
    }

    public async Task<(SmtpResponse Response, SmtpQueueItem? QueueItem)> HandleDataAsync(
        Guid tenantId,
        string sender,
        string recipient,
        string rawMessage,
        string? subject = null,
        CancellationToken cancellationToken = default,
        InboundMailPath path = InboundMailPath.Smtp25,
        IPAddress? clientIp = null,
        string? helo = null,
        string? cloudflareAuthenticationResults = null,
        InboundAuthenticationResult? precomputedAuthentication = null)
    {
        if (!_domainValidator.IsDomainLocal(recipient, out var resolvedTenantId) || resolvedTenantId != tenantId)
        {
            return (SmtpResponse.RelayAccessDenied, null);
        }

        if (_authentication is not null && _authenticationOptions?.Mode != AuthenticationMode.Off)
        {
            InboundAuthenticationResult auth;
            if (precomputedAuthentication is not null)
            {
                auth = precomputedAuthentication;
            }
            else
            {
                try
                {
                    auth = await _authentication.EvaluateAsync(new InboundAuthenticationInput(clientIp, helo, sender, rawMessage, path, cloudflareAuthenticationResults), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch
                {
                    var authserv = _authenticationOptions?.AuthservId ?? "unknown";
                    auth = new(new("temperror", null), [new(null, null, "temperror", "verification service error")],
                        new("temperror", null, null, "none", []), $"{authserv}; spf=temperror; dkim=temperror; dmarc=temperror");
                }
            }

            _logger?.LogInformation("Inbound authentication path={Path} SPF={SpfResult} SPFDomain={SpfDomain} DKIM={DkimResults} DMARC={DmarcResult} FromDomain={FromDomain}",
                path, auth.Spf.Result, auth.Spf.Domain ?? "unknown",
                string.Join(",", auth.Dkim.Select(signature => $"{signature.Domain ?? "unknown"}:{signature.Result}")),
                auth.Dmarc.Result, auth.Dmarc.Domain ?? "unknown");

            var temporaryError = auth.Spf.Result == "temperror" || auth.Dmarc.Result == "temperror" || auth.Dkim.Any(item => item.Result == "temperror");
            if (_authenticationOptions?.Mode == AuthenticationMode.Enforce && !temporaryError &&
                auth.Dmarc.Result == "fail" && auth.Dmarc.Disposition == "reject")
                return (new SmtpResponse(550, "5.7.1", "Message rejected by DMARC policy"), null);

            var separator = rawMessage.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            var headerEnd = separator >= 0 ? separator : rawMessage.IndexOf("\n\n", StringComparison.Ordinal);
            var headerLength = separator >= 0 ? 4 : 2;
            var headerBlock = headerEnd >= 0 ? rawMessage[..headerEnd] : rawMessage;
            var lines = headerBlock.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var retainedLines = new List<string>();
            var skipField = false;
            foreach (var line in lines)
            {
                if (line.StartsWith(" ", StringComparison.Ordinal) || line.StartsWith("\t", StringComparison.Ordinal))
                {
                    if (!skipField) retainedLines.Add(line);
                    continue;
                }
                skipField = false;
                if (line.StartsWith("Authentication-Results:", StringComparison.OrdinalIgnoreCase))
                {
                    var authserv = line[(line.IndexOf(':') + 1)..].Trim().Split(';', 2)[0].Trim();
                    skipField = authserv.Equals(_authenticationOptions?.AuthservId, StringComparison.OrdinalIgnoreCase);
                    if (skipField) continue;
                }
                retainedLines.Add(line);
            }
            var retained = string.Join("\r\n", retainedLines);
            rawMessage = "Authentication-Results: " + auth.AuthenticationResults + "\r\n" + retained + "\r\n\r\n" + (headerEnd >= 0 ? rawMessage[(headerEnd + headerLength)..] : string.Empty);
        }

        var queueItem = await _queueManager.EnqueueAsync(
            tenantId,
            sender,
            recipient,
            rawMessage,
            QueueDirection.Inbound,
            subject,
            cancellationToken);

        return (SmtpResponse.Queued, queueItem);
    }
}
