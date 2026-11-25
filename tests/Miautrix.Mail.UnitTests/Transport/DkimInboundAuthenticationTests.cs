using System.Security.Cryptography;
using System.Text;
using MimeKit;
using MimeKit.Cryptography;
using Miautrix.Mail.Application.Transport;
using Miautrix.Mail.Infrastructure.Mail;

namespace Miautrix.Mail.UnitTests.Transport;

public sealed class DkimInboundAuthenticationTests
{
    private const string Domain = "example.test";
    private static readonly InboundAuthenticationOptions Options = new() { AuthservId = "mx.example.test" };

    [Fact]
    public async Task Valid_signature_passes_and_modified_body_or_header_fails()
    {
        using var key = RSA.Create(2048);
        var signed = Sign(key, "s1");
        var verification = (await Evaluate(signed, key, "s1")).Dkim.Single();
        Assert.True(verification.Result == "pass", verification.ToString());
        Assert.Equal("fail", (await Evaluate(signed.Replace("body text", "body edit", StringComparison.Ordinal), key, "s1")).Dkim.Single().Result);
        Assert.Equal("fail", (await Evaluate(signed.Replace("Subject: hello", "Subject: changed", StringComparison.Ordinal), key, "s1")).Dkim.Single().Result);
    }

    [Fact]
    public async Task Rsa_sha1_is_never_pass_and_short_key_fails()
    {
        using var strong = RSA.Create(1024);
        var sha1 = Sign(strong, "s1", DkimSignatureAlgorithm.RsaSha1);
        Assert.NotEqual("pass", (await Evaluate(sha1, strong, "s1")).Dkim.Single().Result);
        using var weak = RSA.Create(512);
        var weakMessage = Sign(weak, "weak");
        Assert.Equal("fail", (await Evaluate(weakMessage, weak, "weak")).Dkim.Single().Result);
    }

    [Fact]
    public async Task Expired_signature_is_not_pass_and_missing_key_is_permerror()
    {
        using var key = RSA.Create(2048);
        var expired = Sign(key, "s1", expired: true);
        Assert.NotEqual("pass", (await Evaluate(expired, key, "s1")).Dkim.Single().Result);
        var valid = Sign(key, "s1");
        var missing = await new InboundAuthenticationService(new FakeDnsResolver(), Options)
            .EvaluateAsync(new(null, null, "sender@example.test", valid, InboundMailPath.Smtp25));
        Assert.Equal("permerror", missing.Dkim.Single().Result);
    }

    [Fact]
    public async Task Dns_timeout_is_reported_as_temperror()
    {
        using var key = RSA.Create(2048);
        var dns = new FakeDnsResolver { Timeout = true };
        var timedOut = await new InboundAuthenticationService(dns, Options)
            .EvaluateAsync(new(null, null, "sender@example.test", Sign(key, "s1"), InboundMailPath.CloudflareWebhook));
        Assert.Equal("temperror", timedOut.Dkim.Single().Result);
        Assert.Equal("temperror", timedOut.Dmarc.Result);
    }

    [Fact]
    public async Task Two_signatures_are_reported_individually()
    {
        using var key = RSA.Create(2048);
        var message = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(Sign(key, "good"))));
        var second = CreateSigner(key, "bad", DkimSignatureAlgorithm.RsaSha256);
        second.Sign(message, new List<HeaderId> { HeaderId.From, HeaderId.Subject, HeaderId.Date });
        var badHeader = message.Headers.First(h => h.Id == HeaderId.DkimSignature && h.Value.Contains("s=bad", StringComparison.Ordinal));
        var signatureValue = badHeader.Value;
        var signatureIndex = signatureValue.IndexOf("b=", StringComparison.Ordinal) + 2;
        while (signatureIndex < signatureValue.Length && char.IsWhiteSpace(signatureValue[signatureIndex])) signatureIndex++;
        badHeader.Value = signatureValue[..signatureIndex] + (signatureValue[signatureIndex] == 'A' ? 'B' : 'A') + signatureValue[(signatureIndex + 1)..];
        var signed = Serialize(message);
        var dns = new FakeDnsResolver();
        dns.Txt["good._domainkey." + Domain] = [PublicRecord(key)];
        dns.Txt["bad._domainkey." + Domain] = [PublicRecord(key)];
        var result = await new InboundAuthenticationService(dns, Options).EvaluateAsync(new(null, null, "sender@example.test", signed, InboundMailPath.Smtp25));
        Assert.Equal(2, result.Dkim.Count);
        Assert.Contains(result.Dkim, item => item.Selector == "good" && item.Result == "pass");
        Assert.Contains(result.Dkim, item => item.Selector == "bad" && item.Result == "fail");
    }

    [Fact]
    public async Task Webhook_without_ip_still_checks_dkim_and_accepts_only_explicit_cloudflare_spf_result()
    {
        using var key = RSA.Create(2048);
        var message = Sign(key, "s1");
        var dns = new FakeDnsResolver(); dns.Txt["s1._domainkey." + Domain] = [PublicRecord(key)];
        var service = new InboundAuthenticationService(dns, Options);
        var legacy = await service.EvaluateAsync(new(null, null, "sender@example.test", message, InboundMailPath.CloudflareWebhook));
        Assert.Equal("none", legacy.Spf.Result);
        Assert.Equal("pass", legacy.Dkim.Single().Result);
        var withCloudflare = await service.EvaluateAsync(new(null, null, "sender@example.test", message, InboundMailPath.CloudflareWebhook, "cloudflare; spf=pass smtp.mailfrom=example.test"));
        Assert.Equal("pass", withCloudflare.Spf.Result);
    }

    private static async Task<InboundAuthenticationResult> Evaluate(string raw, RSA key, string selector)
    {
        var dns = new FakeDnsResolver(); dns.Txt[$"{selector}._domainkey.{Domain}"] = [PublicRecord(key)];
        return await new InboundAuthenticationService(dns, Options).EvaluateAsync(new(null, null, "sender@example.test", raw, InboundMailPath.Smtp25));
    }

    private static string Sign(RSA key, string selector, DkimSignatureAlgorithm algorithm = DkimSignatureAlgorithm.RsaSha256, bool expired = false)
    {
        var message = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes("From: Sender <sender@example.test>\r\nTo: recipient@example.test\r\nSubject: hello\r\nDate: Tue, 01 Jan 2030 00:00:00 +0000\r\nMessage-ID: <test@example.test>\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nbody text\r\n")));
        var signer = CreateSigner(key, selector, algorithm);
        if (expired) signer.SignaturesExpireAfter = TimeSpan.FromSeconds(-10);
        signer.Sign(message, new List<HeaderId> { HeaderId.From, HeaderId.To, HeaderId.Subject, HeaderId.Date, HeaderId.MessageId });
        return Serialize(message);
    }

    private static string Serialize(MimeMessage message)
    {
        using var stream = new MemoryStream(); message.WriteTo(stream); return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static string ToPem(RSA key) => "-----BEGIN PRIVATE KEY-----\n" + Convert.ToBase64String(key.ExportPkcs8PrivateKey(), Base64FormattingOptions.InsertLineBreaks) + "\n-----END PRIVATE KEY-----";
    private static DkimSigner CreateSigner(RSA key, string selector, DkimSignatureAlgorithm algorithm)
    {
        var stream = new MemoryStream(Encoding.ASCII.GetBytes(ToPem(key)));
        return new DkimSigner(stream, Domain, selector, algorithm);
    }
    private static string PublicRecord(RSA key) => "v=DKIM1; k=rsa; p=" + Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
}
