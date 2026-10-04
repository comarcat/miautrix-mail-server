using Miautrix.Mail.Application.Transport;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Queue;
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
    private readonly ILogger<OutboundQueueDispatcher> _logger;

    public OutboundQueueDispatcher(
        IServiceScopeFactory scopeFactory,
        IEnumerable<IOutboundMailTransport> transports,
        CloudflareEmailOptions cloudflareOptions,
        ILogger<OutboundQueueDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _cloudflareOptions = cloudflareOptions;
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

        // Identify all tenants that have at least one Cloudflare-enabled domain.
        // We only process these tenants because we currently have no transport for 'local' mode.
        var cfTenants = await db.Domains.AsNoTracking()
            .Where(d => d.TransportMode == DomainTransportModes.Cloudflare)
            .Select(d => d.TenantId)
            .Distinct()
            .ToListAsync(ct);

        if (cfTenants.Count == 0)
        {
            return;
        }

        if (!_transports.TryGetValue(DomainTransportModes.Cloudflare, out var transport))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        int totalProcessed = 0;

        foreach (var tenantId in cfTenants)
        {
            if (totalProcessed >= BatchSize) break;

            // Load all domain configs for this tenant to resolve relays.
            var tenantDomains = await db.Domains.AsNoTracking()
                .Where(d => d.TenantId == tenantId)
                .ToListAsync(ct);

            var primary = tenantDomains.FirstOrDefault(d => d.IsPrimary && d.TransportMode == DomainTransportModes.Cloudflare);
            var domainMap = tenantDomains
                .Where(d => d.TransportMode == DomainTransportModes.Cloudflare)
                .ToDictionary(d => d.Name.ToLowerInvariant(), d => (d.CloudflareWorkerUrl, d.CloudflareZoneId));


            var rows = await db.SmtpQueue
                .Where(q => q.TenantId == tenantId && (q.Status == "Pending" || q.Status == "Failed") && q.NextAttemptAt <= now)
                .OrderBy(q => q.NextAttemptAt)
                .Take(BatchSize - totalProcessed)
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                var host = row.Recipient.Split('@').Last().ToLowerInvariant();

                // 1. Try recipient-specific relay config.
                if (domainMap.TryGetValue(host, out var config))
                {
                    await DeliverAsync(queueManager, transport, row, config.Item1, config.Item2, ct);
                }
                // else: recipient domain is not Cloudflare-enabled => leave Pending/Failed for retries.

                totalProcessed++;
                if (totalProcessed >= BatchSize) break;
            }
        }
    }

    private async Task DeliverAsync(
        ISmtpQueueManager queueManager,
        IOutboundMailTransport transport,
        SmtpQueueItem item,
        string? workerUrl,
        string? zoneId,
        CancellationToken ct)
    {
        try
        {
            var message = new OutboundMessage(
                item.TenantId, item.Sender, item.Recipient, item.RawMessage, workerUrl, zoneId);
            var result = await transport.SendAsync(message, ct);

            await queueManager.RecordDeliveryAttemptAsync(
                item.Id, result.IsSuccess, result.ErrorMessage, result.ResponseCode, ct);
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
}
