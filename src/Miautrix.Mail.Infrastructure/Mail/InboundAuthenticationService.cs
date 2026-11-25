using System.Net;
using System.Text;
using MimeKit;
using MimeKit.Cryptography;
using Miautrix.Mail.Application.Transport;

namespace Miautrix.Mail.Infrastructure.Mail;

/// <summary>Inbound authentication implementation; all DNS access is through the injectable resolver.</summary>
public sealed class InboundAuthenticationService : IInboundAuthenticationService
{
    private readonly IDnsResolver dns;
    private readonly InboundAuthenticationOptions options;
    private readonly PublicSuffixList suffixList;
    private readonly Func<int> pctRoll;
    private readonly SpfEvaluator _spfEvaluator;

    public InboundAuthenticationService(IDnsResolver dns, InboundAuthenticationOptions options, PublicSuffixList? suffixList = null, Func<int>? pctRoll = null)
    {
        this.dns = dns;
        this.options = options;
        this.suffixList = suffixList ?? new PublicSuffixList();
        this.pctRoll = pctRoll ?? (() => Random.Shared.Next(100));
        _spfEvaluator = new SpfEvaluator(dns);
    }
    public async Task<InboundAuthenticationResult> EvaluateAsync(InboundAuthenticationInput input, CancellationToken cancellationToken = default)
    {
        if (options.Mode == AuthenticationMode.Off)
            return new(new("none", null), [], new("none", null, null, "none", []), $"{options.AuthservId}; none");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(10));
        var spf = await _spfEvaluator.EvaluateAsync(input.ClientIp, input.Helo, input.MailFrom, budget.Token);
        if (input.Path == InboundMailPath.CloudflareWebhook)
        {
            var cloudflareResults = input.CloudflareAuthenticationResults ?? ReadCloudflareAuthenticationResults(input.RawMessage);
            if (TryReadSpf(cloudflareResults, out var cloudflareSpf)) spf = cloudflareSpf;
        }
        var dkim = await EvaluateDkim(input.RawMessage, budget.Token);
        var message = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(input.RawMessage)));
        var fromDomains = message.From.Mailboxes.Select(m => m.Domain.ToLowerInvariant()).Distinct().ToArray();
        var dmarc = await EvaluateDmarc(fromDomains, spf, dkim, budget.Token);
        var auth = $"{options.AuthservId}; spf={spf.Result} smtp.mailfrom={(spf.Domain ?? "unknown")}; " +
                   (dkim.Count == 0 ? "dkim=none" : string.Join("; ", dkim.Select(x => $"dkim={x.Result} header.d={x.Domain ?? "unknown"}"))) +
                   $"; dmarc={dmarc.Result} header.from={dmarc.Domain ?? "unknown"} policy.dmarc={dmarc.Disposition ?? "none"}";
        return new(spf, dkim, dmarc, auth);
    }

    private static string? ReadCloudflareAuthenticationResults(string raw)
    {
        var headerEnd = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var headers = headerEnd >= 0 ? raw[..headerEnd] : raw.Split(new[] { "\n\n" }, StringSplitOptions.None)[0];
        foreach (var line in headers.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            if (line.StartsWith("Authentication-Results:", StringComparison.OrdinalIgnoreCase) && line.Contains("cloudflare", StringComparison.OrdinalIgnoreCase))
                return line[(line.IndexOf(':') + 1)..].Trim();
        return null;
    }

    private static bool TryReadSpf(string? authResults, out SpfAuthenticationResult result)
    {
        result = new("none", null);
        if (string.IsNullOrWhiteSpace(authResults)) return false;
        var segments = authResults.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !segments[0].Contains("cloudflare", StringComparison.OrdinalIgnoreCase)) return false;
        var spf = segments.Skip(1).FirstOrDefault(x => x.StartsWith("spf=", StringComparison.OrdinalIgnoreCase));
        if (spf is null) return false;
        var pair = spf.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var status = pair[0][4..].ToLowerInvariant();
        if (status is not ("pass" or "fail" or "softfail" or "neutral" or "temperror" or "permerror" or "none")) return false;
        var domain = pair.FirstOrDefault(x => x.StartsWith("smtp.mailfrom=", StringComparison.OrdinalIgnoreCase))?[14..].Trim('<', '>');
        if (domain?.Contains('@') == true) domain = domain[(domain.LastIndexOf('@') + 1)..];
        result = new(status, domain, "Cloudflare-authenticated webhook result");
        return true;
    }

    private async Task<DmarcAuthenticationResult> EvaluateDmarc(string[] fromDomains, SpfAuthenticationResult spf, IReadOnlyList<DkimAuthenticationResult> dkim, CancellationToken ct)
    {
        if (fromDomains.Length != 1) return new("fail", null, null, "none", []);
        var from = fromDomains[0];
        if (spf.Result == "temperror" || dkim.Any(signature => signature.Result == "temperror"))
            return new("temperror", from, null, "none", []);
        var (record, multipleRecords) = await FindDmarcRecord(from, ct);
        if (multipleRecords) return new("permerror", from, null, "none", []);
        string? policyDomain = from;
        if (record is null)
        {
            var org = suffixList.GetOrganizationalDomain(from);
            if (org is not null && !org.Equals(from, StringComparison.OrdinalIgnoreCase))
            {
                policyDomain = org;
                (record, multipleRecords) = await FindDmarcRecord(org, ct);
                if (multipleRecords) return new("permerror", from, null, "none", []);
            }
        }
        if (record is null) return new("none", from, null, "none", []);
        Dictionary<string, string> tags;
        try
        {
            tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in record.Split(';').Select(x => x.Trim()).Where(x => x.Contains('=')))
            {
                var pair = item.Split('=', 2);
                var key = pair[0].Trim();
                var tagValue = pair[1].Trim();
                if (key.Equals("p", StringComparison.OrdinalIgnoreCase) || key.Equals("sp", StringComparison.OrdinalIgnoreCase) || key.Equals("adkim", StringComparison.OrdinalIgnoreCase) || key.Equals("aspf", StringComparison.OrdinalIgnoreCase)) tagValue = tagValue.ToLowerInvariant();
                if (!tags.TryAdd(key, tagValue)) return new("permerror", from, null, "none", []);
            }
            if (!tags.TryGetValue("p", out var policy) || policy is not ("none" or "quarantine" or "reject")) return new("permerror", from, null, "none", []);
            if (tags.GetValueOrDefault("v")?.Equals("DMARC1", StringComparison.OrdinalIgnoreCase) != true) return new("permerror", from, null, "none", []);
        }
        catch { return new("permerror", from, null, "none", []); }

        var p = tags.GetValueOrDefault("p", "none").ToLowerInvariant();
        var spPolicy = tags.GetValueOrDefault("sp", p).ToLowerInvariant();
        var appliedPolicy = !from.Equals(policyDomain, StringComparison.OrdinalIgnoreCase) ? spPolicy : p;
        var pct = int.TryParse(tags.GetValueOrDefault("pct", "100"), out var value) && value is >= 0 and <= 100 ? value : 100;
        var aligned = new List<string>();
        if (spf.Result == "pass" && spf.Domain is not null && IsAligned(from, spf.Domain, tags.GetValueOrDefault("aspf", "r"))) aligned.Add("spf:" + spf.Domain);
        foreach (var signature in dkim.Where(x => x.Result == "pass" && x.Domain is not null))
            if (IsAligned(from, signature.Domain!, tags.GetValueOrDefault("adkim", "r"))) aligned.Add("dkim:" + signature.Domain);
        if (aligned.Count > 0) return new("pass", from, p, "none", aligned, spPolicy, pct);
        var apply = pct == 100 || (pct > 0 && pctRoll() < pct);
        var disposition = apply && (appliedPolicy is "reject" or "quarantine") ? appliedPolicy : "none";
        return new("fail", from, p, disposition, aligned, spPolicy, pct);
    }

    private async Task<(string? Record, bool Multiple)> FindDmarcRecord(string domain, CancellationToken ct)
    {
        var txt = await dns.GetTxtAsync("_dmarc." + domain, ct);
        var records = txt.Where(x => x.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase)).ToArray();
        return (records.Length == 1 ? records[0] : null, records.Length > 1);
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

    private bool IsAligned(string from, string identifier, string mode)
    {
        if (mode.Equals("s", StringComparison.OrdinalIgnoreCase)) return from.Equals(identifier, StringComparison.OrdinalIgnoreCase);
        var fromOrg = suffixList.GetOrganizationalDomain(from) ?? from;
        var identifierOrg = suffixList.GetOrganizationalDomain(identifier) ?? identifier;
        return fromOrg.Equals(identifierOrg, StringComparison.OrdinalIgnoreCase);
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
