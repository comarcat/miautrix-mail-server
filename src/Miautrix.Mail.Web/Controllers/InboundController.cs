using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Protocols.Smtp;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Miautrix.Mail.Web.Controllers;

/// <summary>
/// Accepts raw MIME posted by a Cloudflare Email Worker and hands it to the same
/// <see cref="ISmtpInboundHandler"/> that serves port-25 mail, so parsing, threading and storage
/// are identical regardless of how the message entered the system.
/// </summary>
[ApiController]
[Route("api/v1/inbound")]
public sealed class InboundController : ControllerBase
{
    private const string TokenHeader = "X-Miautrix-Inbound-Token";
    private const string EnvelopeFromHeader = "X-Miautrix-Envelope-From";
    private const string EnvelopeToHeader = "X-Miautrix-Envelope-To";

    private readonly InboundWebhookOptions _options;
    private readonly ISmtpDomainValidator _domainValidator;
    private readonly ISmtpInboundHandler _inboundHandler;
    private readonly ISecurityEventSink _eventSink;
    private readonly ILogger<InboundController> _logger;

    public InboundController(
        InboundWebhookOptions options,
        ISmtpDomainValidator domainValidator,
        ISmtpInboundHandler inboundHandler,
        ISecurityEventSink eventSink,
        ILogger<InboundController> logger)
    {
        _options = options;
        _domainValidator = domainValidator;
        _inboundHandler = inboundHandler;
        _eventSink = eventSink;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/v1/inbound/cloudflare
    /// </summary>
    /// <param name="cancellationToken">Request-scoped cancellation token.</param>
    /// <returns>200 when the message is queued, 401 for bad auth, 403 for relay denied.</returns>
    [HttpPost("cloudflare")]
    [ProducesResponseType(typeof(ApiResponse<InboundQueuedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IResult> Cloudflare(CancellationToken cancellationToken)
    {
        // ── Auth ──────────────────────────────────────────────────────────

        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Inbound webhook rejected request: MIAUTRIX_INBOUND_TOKEN is not configured.");
            _eventSink.RecordEvent(
                Guid.Empty,
                null,
                SecurityEventCodes.AuthInboundWebhookRejected,
                "Inbound webhook rejected: token not configured.",
                ClientIp());
            return ApiResults.Error(HttpContext, StatusCodes.Status503ServiceUnavailable,
                "inbound_unconfigured", "Inbound webhook is not configured on this server.");
        }

        var suppliedToken = Request.Headers[TokenHeader].ToString();

        if (!_options.Accepts(suppliedToken))
        {
            _logger.LogWarning("Inbound webhook rejected request: invalid token from {Ip}.", ClientIp());
            _eventSink.RecordEvent(
                Guid.Empty,
                null,
                SecurityEventCodes.AuthInboundWebhookRejected,
                "Inbound webhook rejected: invalid token.",
                ClientIp());
            return ApiResults.Error(HttpContext, StatusCodes.Status401Unauthorized,
                "inbound_auth_failed", "Unauthorized.");
        }

        // ── Envelope headers ─────────────────────────────────────────────

        var envelopeFrom = Request.Headers[EnvelopeFromHeader].ToString().Trim();
        var envelopeTo = Request.Headers[EnvelopeToHeader].ToString().Trim();

        if (string.IsNullOrEmpty(envelopeTo))
        {
            return ApiResults.Error(HttpContext, StatusCodes.Status400BadRequest,
                "inbound_missing_envelope", "X-Miautrix-Envelope-To header is required.");
        }

        // ── Read raw MIME body ───────────────────────────────────────────

        string rawMime;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true, bufferSize: 81920))
        {
            rawMime = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrEmpty(rawMime))
        {
            return ApiResults.Error(HttpContext, StatusCodes.Status400BadRequest,
                "inbound_empty_body", "Request body must not be empty.");
        }

        // ── Open-relay guard → queue ─────────────────────────────────────

        if (!_domainValidator.IsDomainLocal(envelopeTo, out var tenantId))
        {
            _logger.LogWarning(
                "Inbound webhook relay denied: recipient '{To}' from '{From}'.",
                Redacted(envelopeTo),
                Redacted(envelopeFrom));
            return ApiResults.Error(
                HttpContext, StatusCodes.Status403Forbidden,
                "inbound_relay_denied", "Relay access denied.");
        }

        var (response, queueItem) = await _inboundHandler.HandleDataAsync(
            tenantId,
            string.IsNullOrEmpty(envelopeFrom) ? "<>" : envelopeFrom,
            envelopeTo,
            rawMime,
            subject: null,
            cancellationToken);

        if (!response.IsSuccess)
        {
            return ApiResults.Error(
                HttpContext,
                response.Code == 550 ? StatusCodes.Status403Forbidden : StatusCodes.Status502BadGateway,
                "inbound_processing_failed",
                response.Message);
        }

        _logger.LogInformation(
            "Inbound webhook queued message: {From} → {To}, {Bytes} bytes, queueId {Id}.",
            Redacted(envelopeFrom),
            Redacted(envelopeTo),
            Encoding.UTF8.GetByteCount(rawMime),
            queueItem?.Id);

        return Results.Json(
            new ApiResponse<InboundQueuedResponse>(new InboundQueuedResponse(response.ToSmtpString(), queueItem?.Id)),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    private string? ClientIp() => Request.HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// Mask an email address so the domain is visible but the local part is not.
    /// Never logs full email addresses or message bodies.
    /// </summary>
    private static string Redacted(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "(empty)";
        var at = value.LastIndexOf('@');
        if (at < 1) return "***";
        return "***" + value[at..];
    }
}

/// <summary>Body returned by a successful inbound webhook delivery.</summary>
public sealed record InboundQueuedResponse(string SmtpResponse, Guid? QueueItemId);
