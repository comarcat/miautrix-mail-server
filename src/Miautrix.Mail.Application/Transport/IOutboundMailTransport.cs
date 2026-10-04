using System;
using System.Threading;
using System.Threading.Tasks;
using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.Application.Transport;

/// <summary>
/// An outbound message awaiting delivery, already validated and stored in the queue.
/// <para>
/// The recipient's transport coordinates travel with the message rather than being looked up again
/// inside the transport. The dispatcher has already read the domain row to decide which transport
/// to use, so passing the values along keeps each transport stateless and free of a database
/// dependency.
/// </para>
/// </summary>
public sealed record OutboundMessage(
    Guid TenantId,
    string Sender,
    string Recipient,
    string RawMessage,
    string? WorkerUrl = null,
    string? ZoneId = null);

/// <summary>
/// The outcome of one delivery attempt, shaped to feed
/// <c>ISmtpQueueManager.RecordDeliveryAttemptAsync</c> directly so retry counting, backoff and the
/// dead-letter transition stay in one place.
/// </summary>
public sealed record TransportSendResult(bool IsSuccess, int? ResponseCode, string? ErrorMessage)
{
    public static TransportSendResult Ok(int? responseCode = null) => new(true, responseCode, null);

    /// <summary>
    /// <paramref name="error"/> is persisted to <c>smtp_queue.last_error</c> and shown in the queue
    /// screen, so it must never carry message content, credentials or tokens.
    /// </summary>
    public static TransportSendResult Fail(string error, int? responseCode = null) => new(false, responseCode, error);
}

/// <summary>
/// Delivers queued mail for one <see cref="DomainEntity.TransportMode"/>. Implementations are
/// registered as a set and selected by <see cref="Mode"/>, which keeps the dispatcher free of
/// provider names.
/// </summary>
public interface IOutboundMailTransport
{
    /// <summary>The <see cref="DomainEntity.TransportMode"/> value this transport serves.</summary>
    string Mode { get; }

    Task<TransportSendResult> SendAsync(OutboundMessage message, CancellationToken ct = default);
}

/// <summary>Outcome of checking whether a domain's Cloudflare Worker is actually reachable.</summary>
public sealed record CloudflareProbeResult(bool IsReachable, string Status);

/// <summary>
/// Cloudflare-specific operations. Kept separate from <see cref="IOutboundMailTransport"/> because
/// verification runs in the web app, which never dispatches queued mail.
/// </summary>
public interface ICloudflareTransport
{
    /// <summary>
    /// Proves the domain's Worker answers. Replaces the DKIM/SPF/DMARC TXT lookups that the local
    /// transport relies on: when Cloudflare owns the zone, publishing those records is Cloudflare's
    /// job, so the claim worth verifying is that the Worker we hand mail to is up.
    /// </summary>
    Task<CloudflareProbeResult> ProbeAsync(DomainEntity domain, CancellationToken ct = default);
}
