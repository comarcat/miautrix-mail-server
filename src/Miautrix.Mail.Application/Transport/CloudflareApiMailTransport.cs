using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DomainEntity = Miautrix.Mail.Domain.Domain;
using Miautrix.Mail.Domain;

namespace Miautrix.Mail.Application.Transport;

/// <summary>
/// Hands mail to the tenant's Cloudflare Email Worker, which owns the MX record and calls
/// <c>env.EMAIL.send()</c>. Cloudflare is transport only: this class moves bytes and records the
/// outcome, and never parses, rewrites or stores a message.
/// <para>
/// <b>Wire format.</b> The outbound request shape below follows the flow the operator supplied
/// (<c>C# app → POST /send → Worker → env.EMAIL.send() → recipient</c>). The path, the bearer
/// header and the JSON field names are the only guessed values in this integration, and they are
/// confined to <see cref="SendAsync"/> and <see cref="ProbeAsync"/> so that reconciling them
/// against the sample Worker's <c>src/index.ts</c> is a change to this file alone.
/// </para>
/// </summary>
public sealed class CloudflareApiMailTransport : IOutboundMailTransport, ICloudflareTransport
{
    /// <summary>Route the Worker exposes for outbound hand-off.</summary>
    private const string SendPath = "/send";

    /// <summary>
    /// Upper bound on a single hand-off. A Worker that accepts a message and then stalls must not
    /// hold a dispatcher slot forever; the queue's retry policy owns what happens next.
    /// </summary>
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CloudflareEmailOptions _options;

    public CloudflareApiMailTransport(IHttpClientFactory httpClientFactory, CloudflareEmailOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public string Mode => DomainTransportModes.Cloudflare;

    public async Task<TransportSendResult> SendAsync(OutboundMessage message, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            return TransportSendResult.Fail(
                "Cloudflare transport is selected for this domain but CLOUDFLARE_API_TOKEN is not set on the server.");
        }

        if (string.IsNullOrWhiteSpace(message.WorkerUrl))
        {
            return TransportSendResult.Fail(
                "The recipient's domain is set to Cloudflare transport but has no Worker URL configured.");
        }

        if (!Uri.TryCreate(message.WorkerUrl.Trim(), UriKind.Absolute, out var workerUri))
        {
            return TransportSendResult.Fail(
                "The recipient's domain has a Cloudflare Worker URL that is not a valid absolute URL.");
        }

        var requestUri = new Uri(workerUri, SendPath);

        // Base64 rather than a JSON string literal: a raw MIME message contains CRLF, quotes and
        // arbitrary 8-bit bytes, and re-encoding it as a JSON string is the classic place to
        // corrupt an attachment.
        var payload = JsonSerializer.Serialize(new
        {
            from = message.Sender,
            to = message.Recipient,
            raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(message.RawMessage))
        });

        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(CloudflareApiMailTransport));
            client.Timeout = SendTimeout;

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (response.IsSuccessStatusCode)
            {
                return TransportSendResult.Ok((int)response.StatusCode);
            }

            // The Worker's body may echo the message, so only the status line is read. The queue
            // screen shows last_error, and that must never carry message content.
            return TransportSendResult.Fail(
                $"Cloudflare Worker rejected the message with HTTP {(int)response.StatusCode}.",
                (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The client's own timeout fired rather than host shutdown.
            return TransportSendResult.Fail(
                $"Cloudflare Worker did not respond within {SendTimeout.TotalSeconds:0} seconds.");
        }
        catch (HttpRequestException ex)
        {
            return TransportSendResult.Fail($"Could not reach the Cloudflare Worker: {ex.Message}");
        }
    }

    /// <summary>
    /// Proves the domain's Worker answers. A Cloudflare-mode domain owns no DKIM/SPF/DMARC TXT
    /// records of its own — Cloudflare publishes those — so reachability of the Worker is the
    /// claim that actually predicts whether mail will flow.
    /// </summary>
    public async Task<CloudflareProbeResult> ProbeAsync(DomainEntity domain, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(domain.CloudflareWorkerUrl))
        {
            return new CloudflareProbeResult(false, "No Worker URL is configured for this domain.");
        }

        if (!Uri.TryCreate(domain.CloudflareWorkerUrl.Trim(), UriKind.Absolute, out var workerUri))
        {
            return new CloudflareProbeResult(false, "The configured Worker URL is not a valid absolute URL.");
        }

        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(CloudflareApiMailTransport));
            client.Timeout = ProbeTimeout;

            using var request = new HttpRequestMessage(HttpMethod.Get, workerUri);

            if (_options.IsConfigured)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return new CloudflareProbeResult(
                    true,
                    $"Worker answered HTTP 200 at {workerUri.Host}.");
            }

            return new CloudflareProbeResult(
                false,
                $"Worker answered HTTP {(int)response.StatusCode} at {workerUri.Host}; expected HTTP 200.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new CloudflareProbeResult(
                false,
                $"Worker did not respond within {ProbeTimeout.TotalSeconds:0} seconds.");
        }
        catch (HttpRequestException ex)
        {
            return new CloudflareProbeResult(false, $"Worker is unreachable: {ex.Message}");
        }
    }
}
