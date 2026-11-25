using System.Net;
using System.Text;
using MimeKit;
using MimeKit.Cryptography;
using Miautrix.Mail.Application.Transport;

namespace Miautrix.Mail.Infrastructure.Mail;

/// <summary>Inbound authentication implementation; all DNS access is through the injectable resolver.</summary>
public sealed class InboundAuthenticationService(IDnsResolver dns, InboundAuthenticationOptions options) : IInboundAuthenticationService
{
    public async Task<InboundAuthenticationResult> EvaluateAsync(InboundAuthenticationInput input, CancellationToken cancellationToken = default)
    {
        if (options.Mode == AuthenticationMode.Off)
            return new(new("none", null), [], new("none", null, null, "none", []), $"{options.AuthservId}; none");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(10));
        var spf = await EvaluateSpf(input, budget.Token);
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

    private async Task<SpfAuthenticationResult> EvaluateSpf(InboundAuthenticationInput input, CancellationToken ct)
    {
        var ip = input.ClientIp;
        var sender = input.MailFrom;
        if (string.IsNullOrEmpty(sender) || sender == "<>") sender = input.Helo;
        if (string.IsNullOrWhiteSpace(sender)) return new("none", null);
        var domain = sender.Contains('@') ? sender[(sender.LastIndexOf('@') + 1)..] : sender;
        domain = domain.Trim('<', '>').ToLowerInvariant();
        if (ip is null) return new("none", domain, "client IP unavailable");
        var records = await dns.GetTxtAsync(domain, ct);
        var spfs = records.Where(x => x.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (spfs.Length == 0) return new("none", domain);
        if (spfs.Length != 1) return new("permerror", domain, "multiple SPF records");
        var terms = spfs[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
        var lookups = 0;
        var voids = 0;
        foreach (var term in terms)
        {
            var qualifier = term[0] is '+' or '-' or '~' or '?' ? term[0] : '+';
            var mechanism = qualifier == '+' && term[0] is not '+' ? term : term[1..];
            var matched = false;
            if (mechanism.Equals("all", StringComparison.OrdinalIgnoreCase)) matched = true;
            else if (mechanism.StartsWith("ip4:", StringComparison.OrdinalIgnoreCase) || mechanism.StartsWith("ip6:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = mechanism.Split(':', 2)[1].Split('/', 2);
                if (IPAddress.TryParse(parts[0], out var network))
                {
                    var prefix = parts.Length == 2 && int.TryParse(parts[1], out var bits) ? bits : (network.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
                    matched = IsInNetwork(ip, network, prefix);
                }
            }
            else if (mechanism.StartsWith("exists:", StringComparison.OrdinalIgnoreCase))
            {
                lookups++;
                matched = (await dns.GetAddressesAsync(mechanism[7..], ct)).Count > 0;
            }
            else if (mechanism.StartsWith("include:", StringComparison.OrdinalIgnoreCase))
            {
                lookups++;
                var included = await dns.GetTxtAsync(mechanism[8..], ct);
                matched = included.Any(x => x.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase) && x.Contains(ip.ToString(), StringComparison.Ordinal));
                if (included.Count == 0) voids++;
            }
            else if (mechanism.StartsWith("a", StringComparison.OrdinalIgnoreCase) || mechanism.StartsWith("mx", StringComparison.OrdinalIgnoreCase) || mechanism.StartsWith("ptr", StringComparison.OrdinalIgnoreCase))
            {
                lookups++;
                if (mechanism.StartsWith("a", StringComparison.OrdinalIgnoreCase)) matched = (await dns.GetAddressesAsync(domain, ct)).Contains(ip);
                else if (mechanism.StartsWith("mx", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var mx in await dns.GetMxAsync(domain, ct)) if ((await dns.GetAddressesAsync(mx.Host, ct)).Contains(ip)) { matched = true; break; }
                }
                else
                {
                    foreach (var host in await dns.GetPtrAsync(ip, ct))
                        if ((host.Equals(domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase)) && (await dns.GetAddressesAsync(host, ct)).Contains(ip)) { matched = true; break; }
                }
            }
            if (lookups > 10 || voids > 2) return new("permerror", domain, "SPF lookup limit exceeded");
            if (!matched) continue;
            return new(qualifier switch { '-' => "fail", '~' => "softfail", '?' => "neutral", _ => "pass" }, domain);
        }
        return new("neutral", domain);
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
            try { result.Add(new(domain, selector, await verifier.VerifyAsync(message, header, ct) ? "pass" : "fail")); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { result.Add(new(domain, selector, "temperror", "DNS lookup timeout")); }
            catch (Exception ex) { result.Add(new(domain, selector, ex is FormatException ? "permerror" : "fail", ex.GetType().Name)); }
        }
        return result;
    }

    private static bool Align(string from, string signer, string mode) => mode.Equals("s", StringComparison.OrdinalIgnoreCase) ? from.Equals(signer, StringComparison.OrdinalIgnoreCase) : from.Equals(signer, StringComparison.OrdinalIgnoreCase) || from.EndsWith("." + signer, StringComparison.OrdinalIgnoreCase) || signer.EndsWith("." + from, StringComparison.OrdinalIgnoreCase);
    private static bool IsInNetwork(IPAddress ip, IPAddress network, int prefix)
    {
        var a = ip.GetAddressBytes(); var b = network.GetAddressBytes();
        if (a.Length != b.Length || prefix < 0 || prefix > a.Length * 8) return false;
        for (var i = 0; i < a.Length; i++) { var bits = Math.Clamp(prefix - i * 8, 0, 8); var mask = bits == 0 ? 0 : 0xff << (8 - bits); if ((a[i] & mask) != (b[i] & mask)) return false; }
        return true;
    }

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
