using System.Net;
using System.Security.Cryptography;
using System.Text;
using MimeKit;
using MimeKit.Cryptography;
using Miautrix.Mail.Application.Transport;
using Miautrix.Mail.Infrastructure.Mail;

namespace Miautrix.Mail.UnitTests.Transport;

public sealed class DmarcEvaluationTests
{
    private const string FromDomain = "news.example.test";

    [Theory]
    [InlineData("r", "example.test", "pass")]
    [InlineData("s", "example.test", "fail")]
    [InlineData("r", "other.test", "fail")]
    [InlineData("s", "news.example.test", "pass")]
    public async Task Spf_alignment_matrix(string mode, string spfDomain, string expected)
    {
        var dns = DnsWithDmarc("v=DMARC1; p=reject; aspf=" + mode);
        dns.Txt[spfDomain] = ["v=spf1 ip4:192.0.2.1 -all"];
        var result = await Evaluate(dns, "sender@" + spfDomain, $"From: a@{FromDomain}\r\n\r\nbody\r\n");
        Assert.Equal(expected, result.Dmarc.Result);
    }

    [Theory]
    [InlineData("r", "example.test", "pass")]
    [InlineData("s", "example.test", "fail")]
    [InlineData("r", "other.test", "fail")]
    [InlineData("s", "news.example.test", "pass")]
    public async Task Dkim_alignment_matrix(string mode, string signingDomain, string expected)
    {
        using var key = RSA.Create(2048);
        var raw = Sign(key, signingDomain);
        var dns = DnsWithDmarc("v=DMARC1; p=reject; adkim=" + mode);
        dns.Txt["selector._domainkey." + signingDomain] = ["v=DKIM1; k=rsa; p=" + Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())];
        var result = await Evaluate(dns, "sender@invalid.test", raw);
        Assert.Equal(expected, result.Dmarc.Result);
    }

    [Fact]
    public async Task Organizational_domain_fallback_sp_policy_and_pct_are_applied()
    {
        var psl = new PublicSuffixList();
        Assert.Equal("example.co.uk", psl.GetOrganizationalDomain("a.b.example.co.uk"));
        var dns = new FakeDnsResolver();
        dns.Txt["_dmarc.example.test"] = ["v=DMARC1; p=reject; sp=quarantine; pct=20"];
        var passed = await Evaluate(dns, "sender@invalid.test", $"From: a@{FromDomain}\r\n\r\nbody\r\n", () => 99);
        Assert.Equal("none", passed.Dmarc.Disposition);
        Assert.Equal("quarantine", passed.Dmarc.SubdomainPolicy);
        Assert.Equal(20, passed.Dmarc.Percentage);
        Assert.Equal("quarantine", (await Evaluate(dns, "sender@invalid.test", $"From: a@{FromDomain}\r\n\r\nbody\r\n", () => 0)).Dmarc.Disposition);
    }

    [Fact]
    public async Task Missing_record_is_none_and_multiple_from_is_fail()
    {
        var dns = new FakeDnsResolver();
        Assert.Equal("none", (await Evaluate(dns, "sender@invalid.test", "From: a@example.test\r\n\r\nbody\r\n")).Dmarc.Result);
        Assert.Equal("fail", (await Evaluate(dns, "sender@invalid.test", "From: a@example.test, b@example.org\r\n\r\nbody\r\n")).Dmarc.Result);
    }

    private static FakeDnsResolver DnsWithDmarc(string record)
    {
        var dns = new FakeDnsResolver(); dns.Txt["_dmarc." + FromDomain] = [record]; return dns;
    }

    private static Task<InboundAuthenticationResult> Evaluate(FakeDnsResolver dns, string sender, string raw, Func<int>? roll = null) =>
        new InboundAuthenticationService(dns, new InboundAuthenticationOptions { AuthservId = "mx.example.test" }, pctRoll: roll)
            .EvaluateAsync(new(IPAddress.Parse("192.0.2.1"), "helo.test", sender, raw, InboundMailPath.Smtp25));

    private static string Sign(RSA key, string signingDomain)
    {
        var msg = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes($"From: a@{FromDomain}\r\nSubject: hi\r\n\r\nbody\r\n")));
        var pem = "-----BEGIN PRIVATE KEY-----\n" + Convert.ToBase64String(key.ExportPkcs8PrivateKey(), Base64FormattingOptions.InsertLineBreaks) + "\n-----END PRIVATE KEY-----";
        using var pemStream = new MemoryStream(Encoding.ASCII.GetBytes(pem));
        new DkimSigner(pemStream, signingDomain, "selector", DkimSignatureAlgorithm.RsaSha256).Sign(msg, new List<HeaderId> { HeaderId.From, HeaderId.Subject });
        using var output = new MemoryStream(); msg.WriteTo(output); return Encoding.UTF8.GetString(output.ToArray());
    }
}
