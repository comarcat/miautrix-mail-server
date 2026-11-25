using System.Globalization;
using Miautrix.Mail.Application.Transport;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Queue;
using Miautrix.Mail.Protocols.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Miautrix.Mail.Worker;

/// <summary>
/// Reads <c>Pending</c>/<c>Failed</c> rows out of the SMTP queue and hands them to the transport
/// that serves the recipient domain's <see cref="Domain.TransportMode"/>.
/// <para>
/// Only domains on a transport that is actually registered are picked up. A domain left on
/// <see cref="DomainTransportModes.Local"/> has no transport registered, so its queued mail stays
/// <c>Pending</c> exactly as it did before this dispatcher existed — enabling Cloudflare for one
/// domain must not change what happens to any other domain's mail.
/// </para>
/// </summary>
public sealed class OutboundQueueDispatcher : BackgroundService
{
    private const int BatchSize = 50;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyDictionary<string, IOutboundMailTransport> _transports;
    private readonly CloudflareEmailOptions _cloudflareOptions;
    private readonly IDkimService _dkimService;
    private readonly QueueRetryOptions _retryOptions;
    private readonly ILogger<OutboundQueueDispatcher> _logger;

    public OutboundQueueDispatcher(
        IServiceScopeFactory scopeFactory,
        IEnumerable<IOutboundMailTransport> transports,
        CloudflareEmailOptions cloudflareOptions,
        IDkimService dkimService,
        QueueRetryOptions retryOptions,
        ILogger<OutboundQueueDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _cloudflareOptions = cloudflareOptions;
        _dkimService = dkimService;
        _retryOptions = retryOptions;
        _transports = transports.ToDictionary(t => t.Mode, StringComparer.OrdinalIgnoreCase);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_transports.Count == 0)
        {
            _logger.LogInformation(
                "OutboundQueueDispatcher: no outbound transport is registered. Queued mail stays " +
                "Pending; configure a domain for Cloudflare transport to enable delivery.");
            return;
        }

        await WarnIfTransportMissingAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failure here is a failure of the pass, not of the process. Log and poll again
                // rather than taking the mail listeners down with it.
                _logger.LogError(ex, "Outbound dispatch pass failed; retrying after the poll interval.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// A domain switched to a transport that cannot actually deliver would silently stop sending
    /// mail. Say so loudly at startup instead.
    /// <para>
    /// This never takes the process down: a misconfigured outbound path must not stop inbound mail
    /// arriving on port 25.
    /// </para>
    /// </summary>
    private async Task WarnIfTransportMissingAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var modesInUse = await db.Domains.AsNoTracking()
            .Select(d => d.TransportMode)
            .Distinct()
            .ToListAsync(ct);

        foreach (var mode in modesInUse)
        {
            if (string.Equals(mode, DomainTransportModes.Local, StringComparison.OrdinalIgnoreCase) ||
                _transports.ContainsKey(mode))
            {
                continue;
            }

            _logger.LogError(
                "Domains are configured with transport mode '{Mode}' but no transport for it is " +
                "registered. Outbound mail for those domains will stay queued and never be " +
                "delivered. Registered transports: {Registered}.",
                mode,
                string.Join(", ", _transports.Keys));
        }

        // The transport is registered but cannot authenticate. Without this the failure would only
        // surface one message at a time, as each queued item exhausts its retries.
        if (_transports.ContainsKey(DomainTransportModes.Cloudflare) &&
            modesInUse.Any(m => string.Equals(m, DomainTransportModes.Cloudflare, StringComparison.OrdinalIgnoreCase)) &&
            !_cloudflareOptions.IsConfigured)
        {
            _logger.LogError(
                "Domains are configured for Cloudflare transport but CLOUDFLARE_API_TOKEN is not " +
                "set. Outbound mail for those domains will fail every delivery attempt.");
        }
    }

    /// <summary>
    /// One dispatch pass. Internal rather than private so the integration tests can drive a single
    /// pass against the real database instead of waiting on the poll interval.
    /// </summary>
    internal async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var queueManager = scope.ServiceProvider.GetRequiredService<ISmtpQueueManager>();

        // Identify tenants using a registered non-local outbound transport.
        var cfTenants = await db.Domains.AsNoTracking()
            .Where(d => d.TransportMode != DomainTransportModes.Local && _transports.Keys.Contains(d.TransportMode))
            .Select(d => d.TenantId)
            .Distinct()
            .ToListAsync(ct);

        if (cfTenants.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var expiryCutoff = now - _retryOptions.Lifetime;
        var expired = await db.SmtpQueue
            .Where(q => q.Direction == QueueDirection.Outbound && (q.Status == "Pending" || q.Status == "Failed") && q.CreatedAt <= expiryCutoff)
            .ToListAsync(ct);
        foreach (var item in expired)
        {
            item.Status = "DeadLetter";
            item.LastError = "Message expired after the configured outbound retry lifetime.";
            item.UpdatedAt = now;
            await EnqueueDsnIfLocalAsync(db, queueManager, item, expired: true, ct);
        }
        if (expired.Count > 0) await db.SaveChangesAsync(ct);
        int totalProcessed = 0;

        foreach (var tenantId in cfTenants)
        {
            if (totalProcessed >= BatchSize) break;

            // Load all domain configs for this tenant to resolve relays.
            var tenantDomains = await db.Domains.AsNoTracking()
                .Where(d => d.TenantId == tenantId)
                .ToListAsync(ct);

            var primary = tenantDomains.FirstOrDefault(d => d.IsPrimary && d.TransportMode == DomainTransportModes.Cloudflare);
            var domainMap = tenantDomains.ToDictionary(d => d.Name.ToLowerInvariant());
            var dkimKeys = await db.DkimKeys.AsNoTracking()
                .Where(k => k.TenantId == tenantId)
                .ToListAsync(ct);


            var rows = await db.SmtpQueue
                .Where(q => q.TenantId == tenantId && q.Direction == QueueDirection.Outbound && (q.Status == "Pending" || q.Status == "Failed") && q.NextAttemptAt <= now && q.CreatedAt > expiryCutoff)
                .OrderBy(q => q.NextAttemptAt)
                .Take(BatchSize - totalProcessed)
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                var senderDomain = row.Sender.Split('@').Last().TrimEnd('>').ToLowerInvariant();
                if (domainMap.TryGetValue(senderDomain, out var sendingDomain) &&
                    _transports.TryGetValue(sendingDomain.TransportMode, out var transport))
                {
                    var key = dkimKeys.FirstOrDefault(k =>
                        string.Equals(k.DomainName, sendingDomain.Name, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(k.Selector, sendingDomain.DkimSelector, StringComparison.Ordinal));
                    await DeliverAsync(db, queueManager, transport, row, sendingDomain.CloudflareWorkerUrl, sendingDomain.CloudflareZoneId,
                        sendingDomain.Name, sendingDomain.DkimSelector, key?.PrivateKeyPem, ct);
                }
                else if (primary != null)
                {
                    if (_transports.TryGetValue(primary.TransportMode, out var primaryTransport))
                    {
                        var key = dkimKeys.FirstOrDefault(k =>
                            string.Equals(k.DomainName, primary.Name, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(k.Selector, primary.DkimSelector, StringComparison.Ordinal));
                        await DeliverAsync(db, queueManager, primaryTransport, row, primary.CloudflareWorkerUrl, primary.CloudflareZoneId,
                            primary.Name, primary.DkimSelector, key?.PrivateKeyPem, ct);
                    }
                }

                totalProcessed++;
                if (totalProcessed >= BatchSize) break;
            }
        }
    }

    private async Task DeliverAsync(
        AppDbContext db,
        ISmtpQueueManager queueManager,
        IOutboundMailTransport transport,
        SmtpQueueItem item,
        string? workerUrl,
        string? zoneId,
        string domain,
        string? selector,
        string? privateKey,
        CancellationToken ct)
    {
        try
        {
            var rawMessage = !string.IsNullOrWhiteSpace(privateKey) && !string.IsNullOrWhiteSpace(selector)
                ? _dkimService.SignMessage(item.RawMessage, domain, selector, privateKey)
                : item.RawMessage;
            var message = new OutboundMessage(
                item.TenantId, item.Sender, item.Recipient, rawMessage, workerUrl, zoneId);
            var result = await transport.SendAsync(message, ct);
            await using var transaction = !result.IsSuccess && result.IsPermanentFailure
                ? await db.Database.BeginTransactionAsync(ct)
                : null;
            await queueManager.RecordDeliveryAttemptAsync(
                item.Id, result.IsSuccess, result.ErrorMessage, result.ResponseCode, ct, result.IsPermanentFailure);
            if (!result.IsSuccess && (result.IsPermanentFailure || item.Status == "DeadLetter"))
            {
                await EnqueueDsnIfLocalAsync(db, queueManager, item, expired: !result.IsPermanentFailure, ct);
            }
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down: leave the row Pending so the next process picks it up.
            throw;
        }
        catch (Exception ex)
        {
            // The transport threw instead of returning a result. The attempt must still be
            // recorded, otherwise the row is retried forever and never reaches the dead-letter
            // state. The message itself is never written to the log or to last_error.
            _logger.LogError(ex, "Delivery attempt for queue item {QueueItemId} threw.", item.Id);

            await queueManager.RecordDeliveryAttemptAsync(
                item.Id, success: false, "Transport threw before returning a result.", responseCode: null, ct);
        }
    }

    private async Task EnqueueDsnIfLocalAsync(
        AppDbContext db,
        ISmtpQueueManager queueManager,
        SmtpQueueItem original,
        bool expired,
        CancellationToken ct)
    {
        var sender = original.Sender.Trim().Trim('<', '>');
        if (sender.Length == 0 || !sender.Contains('@')) return;

        var at = sender.LastIndexOf('@');
        var senderDomain = sender[(at + 1)..];
        var localDomain = await db.Domains.AsNoTracking().FirstOrDefaultAsync(
            d => d.TenantId == original.TenantId && d.Name.ToLower() == senderDomain.ToLower(), ct);
        if (localDomain is null) return;
        var isLocalMailbox = await db.Mailboxes.AsNoTracking().AnyAsync(m =>
            m.TenantId == original.TenantId && m.DomainId == localDomain.Id &&
            m.Address.ToLower() == sender.ToLower() && m.IsActive, ct);
        if (!isLocalMailbox) return;

        var status = expired ? "4.4.7" : "5.0.0";
        var code = expired ? "451" : "550";
        var raw = BuildDeliveryStatusNotification(sender, original.Recipient, status, code,
            Environment.GetEnvironmentVariable("OUTBOUND_EHLO_NAME") ?? "localhost", original.CreatedAt);
        // Empty envelope sender is the RFC null reverse-path. InboundQueueDispatcher will deliver
        // this locally; external delivery is never invoked for this inbound queue row.
        await queueManager.EnqueueAsync(original.TenantId, string.Empty, sender, raw, QueueDirection.Inbound,
            "Delivery Status Notification (Failure)", ct);
    }

    internal static string BuildDeliveryStatusNotification(
        string recipient,
        string failedRecipient,
        string status,
        string smtpCode,
        string reportingMta,
        DateTimeOffset arrival)
    {
        static string Safe(string value) => value.Replace("\r", " ").Replace("\n", " ");
        var boundary = "=_miautrix_dsn_" + Guid.NewGuid().ToString("N");
        return $"From: Mail Delivery Subsystem <MAILER-DAEMON@{Safe(reportingMta)}>\r\n" +
               $"To: {Safe(recipient)}\r\n" +
               "Subject: Delivery Status Notification (Failure)\r\n" +
               "MIME-Version: 1.0\r\n" +
               $"Content-Type: multipart/report; report-type=delivery-status; boundary=\"{boundary}\"\r\n\r\n" +
               $"--{boundary}\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n" +
               $"Delivery to {Safe(failedRecipient)} failed. { (status == "4.4.7" ? "The message expired while retrying." : "The recipient server permanently rejected the message.") }\r\n" +
               $"--{boundary}\r\nContent-Type: message/delivery-status\r\n\r\n" +
               $"Reporting-MTA: dns; {Safe(reportingMta)}\r\n" +
               $"Arrival-Date: {arrival.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss +0000", CultureInfo.InvariantCulture)}\r\n\r\n" +
               $"Final-Recipient: rfc822; {Safe(failedRecipient)}\r\n" +
               "Action: failed\r\n" +
               $"Status: {status}\r\n" +
               $"Diagnostic-Code: smtp; {smtpCode} Delivery failed\r\n" +
               $"--{boundary}--\r\n";
    }
}
