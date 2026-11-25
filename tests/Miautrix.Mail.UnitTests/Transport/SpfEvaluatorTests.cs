using System.Net;
using Miautrix.Mail.UnitTests.Transport;
using Miautrix.Mail.Infrastructure.Mail;

namespace Miautrix.Mail.UnitTests.Transport;

public sealed class SpfEvaluatorTests
{
    private static readonly IPAddress Ip = IPAddress.Parse("192.0.2.1");
    public static TheoryData<string, string?, string> Cases => new()
    {
        {"v=spf1 all", null, "pass"}, {"v=spf1 +all", null, "pass"}, {"v=spf1 -all", null, "fail"}, {"v=spf1 ~all", null, "softfail"}, {"v=spf1 ?all", null, "neutral"},
        {"v=spf1 ip4:192.0.2.0/24 -all", null, "pass"}, {"v=spf1 ip4:198.51.100.1 -all", null, "fail"},
        {"v=spf1 ip6:2001:db8::/32 -all", "2001:db8::1", "pass"}, {"v=spf1 ip6:2001:db8::/32 -all", "2001:db9::1", "fail"},
        {"v=spf1 a -all", null, "pass"}, {"v=spf1 a:mail.example.test -all", null, "pass"}, {"v=spf1 a/24 -all", null, "pass"},
        {"v=spf1 mx -all", null, "pass"}, {"v=spf1 mx:mail.example.test -all", null, "pass"},
        {"v=spf1 ptr -all", null, "pass"}, {"v=spf1 ptr:example.test -all", null, "pass"},
        {"v=spf1 exists:%{i}.example.test -all", null, "pass"}, {"v=spf1 exists:missing.example.test -all", null, "fail"},
        {"v=spf1 include:inc.example.test -all", null, "pass"}, {"v=spf1 include:incfail.example.test -all", null, "fail"},
        {"v=spf1 redirect=inc.example.test", null, "pass"}, {"v=spf1 redirect=none.example.test", null, "permerror"},
        {"v=spf1 exists:%{s}.example.test -all", null, "pass"}, {"v=spf1 exists:%{l}.example.test -all", null, "pass"},
        {"v=spf1 exists:%{o}.example.test -all", null, "pass"}, {"v=spf1 exists:%{d}.example.test -all", null, "pass"},
        {"v=spf1 exists:%{h}.example.test -all", null, "pass"}, {"v=spf1 exists:%{c}.example.test -all", null, "permerror"},
        {"v=spf1 exists:%{v}.example.test -all", null, "pass"}, {"v=spf1 exists:%{r}.example.test -all", null, "permerror"},
        {"v=spf1 exists:%{t}.example.test -all", null, "permerror"}, {"v=spf1 exists:%%%{i}.example.test -all", null, "pass"},
        {"v=spf1 exists:%{d2}.example.test -all", null, "pass"}, {"v=spf1 exists:%{d1r}.example.test -all", null, "pass"},
        {"v=spf1 exists:%{S}.example.test -all", null, "pass"}, {"v=spf1 exists:%{p}.example.test -all", null, "pass"},
        {"v=spf1", null, "neutral"},
        {"v=spf1 foo=bar ip4:192.0.2.1 -all", null, "pass"},
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Evaluates_mechanisms_and_qualifiers(string record, string? ip, string expected)
    {
        var dns = new FakeDnsResolver();
        dns.Txt["example.test"] = [record];
        dns.Txt["inc.example.test"] = ["v=spf1 ip4:192.0.2.0/24 -all"];
        dns.Txt["incfail.example.test"] = ["v=spf1 -all"];
        dns.Addresses["example.test"] = [Ip]; dns.Addresses["mail.example.test"] = [Ip];
        dns.Addresses["192.0.2.1.example.test"] = [Ip];
        dns.Mx["example.test"] = [new("mail.example.test", 10)]; dns.Mx["mail.example.test"] = [new("mail.example.test", 10)];
        dns.Ptr[Ip.ToString()] = ["mail.example.test"];
        var actualIp = IPAddress.Parse(ip ?? "192.0.2.1");
        var result = await new SpfEvaluator(dns).EvaluateAsync(actualIp, "helo.example.test", "sender@example.test");
        Assert.Equal(expected, result.Result);
    }

    [Fact]
    public async Task Multiple_records_are_permerror_and_null_reverse_path_uses_helo()
    {
        var dns = new FakeDnsResolver(); dns.Txt["example.test"] = ["v=spf1 -all", "v=spf1 +all"];
        Assert.Equal("permerror", (await new SpfEvaluator(dns).EvaluateAsync(Ip, null, "x@example.test")).Result);
        dns.Txt["helo.test"] = ["v=spf1 ip4:192.0.2.0/24 -all"];
        Assert.Equal("pass", (await new SpfEvaluator(dns).EvaluateAsync(Ip, "helo.test", "<>")).Result);
    }

    [Fact]
    public async Task Exp_modifier_duplicates_and_non_exp_c_r_t_macros_are_permerror()
    {
        var dns = new FakeDnsResolver();
        dns.Txt["example.test"] = ["v=spf1 include:inc.example.test -all"];
        dns.Txt["inc.example.test"] = ["v=spf1 exists:%{c}.example.test -all"];
        Assert.Equal("permerror", (await new SpfEvaluator(dns).EvaluateAsync(Ip, "helo.example.test", "x@example.test")).Result);

        dns.Txt["example.test"] = ["v=spf1 exp=explain.example.test exp=other.example.test -all"];
        Assert.Equal("permerror", (await new SpfEvaluator(dns).EvaluateAsync(Ip, "helo.example.test", "x@example.test")).Result);
    }

    [Fact]
    public async Task Lookup_limit_void_limit_and_timeout_are_enforced()
    {
        var dns = new FakeDnsResolver { WildcardAddressAnswers = false }; dns.Txt["example.test"] = ["v=spf1 " + string.Join(' ', Enumerable.Repeat("exists:x.example.test", 11))];
        Assert.Equal("permerror", (await new SpfEvaluator(dns).EvaluateAsync(Ip, "example.test", "x@example.test")).Result);
        dns.Txt["example.test"] = ["v=spf1 exists:none.example.test exists:none2.example.test exists:none3.example.test -all"];
        Assert.Equal("permerror", (await new SpfEvaluator(dns).EvaluateAsync(Ip, "example.test", "x@example.test")).Result);
        dns.Timeout = true;
        Assert.Equal("temperror", (await new SpfEvaluator(dns).EvaluateAsync(Ip, "example.test", "x@example.test")).Result);
    }

    // Mechanism/qualifier cases are RFC 7208 §5/§6 adaptations; no public DNS is queried.
    // Open SPF test suite: https://github.com/ietf-wg-spfbis/spf-test-suite
}
