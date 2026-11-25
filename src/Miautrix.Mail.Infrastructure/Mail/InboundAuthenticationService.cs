using System.Net;
using System.Text;
using MimeKit;
using MimeKit.Cryptography;
using Miautrix.Mail.Application.Transport;

namespace Miautrix.Mail.Infrastructure.Mail;

/// <summary>Inbound authentication implementation; all DNS access is through the injectable resolver.</summary>
public sealed class InboundAuthenticationService(IDnsResolver dns, InboundAuthenticationOptions options) : IInboundAuthenticationService
{
    private readonly SpfEvaluator _spfEvaluator = new(dns);
    public async Task<InboundAuthenticationResult> EvaluateAsync(InboundAuthenticationInput input, CancellationToken cancellationToken = default)
    {
        if (options.Mode == AuthenticationMode.Off)
            return new(new("none", null), [], new("none", null, null, "none", []), $"{options.AuthservId}; none");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(10));
        var spf = await _spfEvaluator.EvaluateAsync(input.ClientIp, input.Helo, input.MailFrom, budget.Token);
        var dkim = await EvaluateDkim(input.RawMessage, budget.Token);
        var message = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(input.RawMessage)));
        var fromDomains = message.From.Mailboxes.Select(m => m.Domain.ToLowerInvariant()).Distinct().ToArray();
        DmarcAuthenticationResult dmarc;
        if (fromDomains.Length != 1)
        {
            dmarc = new("permerror", null, null, "none", []);
        }
        else
        {
            var domain = fromDomains[0];
            var records = await dns.GetTxtAsync("_dmarc." + domain, budget.Token);
            var record = records.SingleOrDefault(value => value.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase));
            if (record is null) dmarc = new("none", domain, null, "none", []);
            else
            {
                var tags = record.Split(';').Select(x => x.Trim()).Where(x => x.Contains('=')).Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);
                var policy = tags.GetValueOrDefault("p", "none").ToLowerInvariant();
                var aligned = dkim.Where(x => x.Result == "pass" && x.Domain is not null && Align(domain, x.Domain, tags.GetValueOrDefault("adkim", "r"))).Select(x => x.Domain!).ToArray();
                if (aligned.Length > 0) dmarc = new("pass", domain, policy, "none", aligned);
                else
                {
                    var pct = int.TryParse(tags.GetValueOrDefault("pct", "100"), out var parsedPct) ? Math.Clamp(parsedPct, 0, 100) : 100;
                    var disposition = policy == "reject" && Random.Shared.Next(100) < pct ? "reject" : policy == "quarantine" && Random.Shared.Next(100) < pct ? "quarantine" : "none";
                    dmarc = new("fail", domain, policy, disposition, []);
                }
            }
        }
        var auth = $"{options.AuthservId}; spf={spf.Result} smtp.mailfrom={(spf.Domain ?? "unknown")}; " +
                   (dkim.Count == 0 ? "dkim=none" : string.Join("; ", dkim.Select(x => $"dkim={x.Result} header.d={x.Domain ?? "unknown"}"))) +
                   $"; dmarc={dmarc.Result} header.from={dmarc.Domain ?? "unknown"}";
        return new(spf, dkim, dmarc, auth);
    }

    private async Task<IReadOnlyList<DkimAuthenticationResult>> EvaluateDkim(string raw, CancellationToken ct)
    {
        var message = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(raw)));
        var locator = new DnsDkimLocator(dns);
        var verifier = new DkimVerifier(locator) { MinimumRsaKeyLength = 1024 };
        var result = new List<DkimAuthenticationResult>();
        foreach (var header in message.Headers.Where(x => x.Id == HeaderId.DkimSignature))
        {
            var tags = header.Value.Split(';').Select(x => x.Trim()).Where(x => x.Contains('=')).Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);
            var domain = tags.GetValueOrDefault("d"); var selector = tags.GetValueOrDefault("s");
            if (tags.GetValueOrDefault("a", "").Equals("rsa-sha1", StringComparison.OrdinalIgnoreCase)) { result.Add(new(domain, selector, "fail", "rsa-sha1 prohibited")); continue; }
            if (long.TryParse(tags.GetValueOrDefault("x"), out var expires) && expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            { result.Add(new(domain, selector, "fail", "signature expired (x=)")); continue; }
            try
            {
                var passed = await verifier.VerifyAsync(message, header, ct);
                result.Add(new(domain, selector, passed ? "pass" : "fail", passed && tags.ContainsKey("l") ? "l= body length limits signed body coverage" : null));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { result.Add(new(domain, selector, "temperror", "DNS lookup timeout")); }
            catch (Exception ex) { result.Add(new(domain, selector, ex is FormatException ? "permerror" : "fail", ex.GetType().Name + ": " + ex.Message)); }
        }
        return result;
    }

    private static bool Align(string from, string signer, string mode) => mode.Equals("s", StringComparison.OrdinalIgnoreCase) ? from.Equals(signer, StringComparison.OrdinalIgnoreCase) : from.Equals(signer, StringComparison.OrdinalIgnoreCase) || from.EndsWith("." + signer, StringComparison.OrdinalIgnoreCase) || signer.EndsWith("." + from, StringComparison.OrdinalIgnoreCase);
    private sealed class DnsDkimLocator(IDnsResolver dns) : DkimPublicKeyLocatorBase
    {
        public override Org.BouncyCastle.Crypto.AsymmetricKeyParameter LocatePublicKey(string methods, string domain, string selector, CancellationToken cancellationToken = default)
        {
            var records = dns.GetTxtAsync($"{selector}._domainkey.{domain}", cancellationToken).GetAwaiter().GetResult();
            if (records.Count != 1) throw new FormatException("Missing or ambiguous DKIM key record.");
            return GetPublicKey(records[0]);
        }

        public override async Task<Org.BouncyCastle.Crypto.AsymmetricKeyParameter> LocatePublicKeyAsync(string methods, string domain, string selector, CancellationToken cancellationToken = default)
        {
            var records = await dns.GetTxtAsync($"{selector}._domainkey.{domain}", cancellationToken);
            if (records.Count != 1) throw new FormatException("Missing or ambiguous DKIM key record.");
            return GetPublicKey(records[0]);
        }
    }
}
