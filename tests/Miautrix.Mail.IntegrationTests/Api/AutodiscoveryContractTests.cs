using System.Net;
using System.Text;
using System.Xml.Linq;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.IntegrationTests.Api;

[Trait("Category", "Autodiscovery")]
[Collection("AutodiscoveryContract")]
public sealed class AutodiscoveryContractTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;
    private readonly string HostDomain = $"autodisc-{Guid.NewGuid():N}.test";
    private readonly Guid _tenantId = Guid.NewGuid();

    public AutodiscoveryContractTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    {
        Environment.SetEnvironmentVariable("MIAUTRIX_PUBLIC_IMAP_HOST", "imap.mailhost.test");
        Environment.SetEnvironmentVariable("MIAUTRIX_PUBLIC_SMTP_HOST", "smtp.mailhost.test");
        _factory = factory;
    }

    [Fact]
    public async Task Autoconfig_has_expected_servers_ports_and_does_not_enumerate_local_parts()
    {
        await SeedDomainAsync();
        using var client = _factory.CreateClient();
        var existing = await client.GetStringAsync($"/.well-known/autoconfig/mail/config-v1.1.xml?emailaddress=alice@{HostDomain}");
        var missing = await client.GetStringAsync($"/.well-known/autoconfig/mail/config-v1.1.xml?emailaddress=not-a-user@{HostDomain}");
        Assert.Equal(existing, missing);
        var xml = XDocument.Parse(existing);
        Assert.Equal("1.1", xml.Root!.Attribute("version")!.Value);
        Assert.Equal(new[] { "imap", "smtp", "smtp" }, xml.Descendants().Where(e => e.Name.LocalName is "incomingServer" or "outgoingServer").Select(e => e.Attribute("type")!.Value));
        var servers = xml.Descendants().Where(e => e.Name.LocalName is "incomingServer" or "outgoingServer").ToArray();
        Assert.Equal(new[] { "993", "465", "587" }, servers.Select(e => e.Element("port")!.Value));
        Assert.Equal(new[] { "SSL", "SSL", "STARTTLS" }, servers.Select(e => e.Element("socketType")!.Value));
        Assert.All(servers, server =>
        {
            Assert.Equal("%EMAILADDRESS%", server.Element("username")!.Value);
            Assert.Equal("plain", server.Element("authentication")!.Value);
            Assert.NotNull(server.Element("hostname"));
        });
        Assert.Contains("%EMAILADDRESS%", existing);
        Assert.Contains("imap.mailhost.test", existing);
        Assert.Contains("smtp.mailhost.test", existing);
    }

    [Fact]
    public async Task Outlook_parses_email_address_and_echoes_it_in_pox_schema()
    {
        await SeedDomainAsync();
        using var client = _factory.CreateClient();
        var email = $"client@{HostDomain}";
        var xml = $"<Autodiscover><Request><EMailAddress>{email}</EMailAddress></Request></Autodiscover>";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/autodiscover/autodiscover.xml") { Content = new StringContent(xml, Encoding.UTF8, "text/xml") };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var doc = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("http://schemas.microsoft.com/exchange/autodiscover/outlook/responseschema/2006a", doc.Root!.Name.NamespaceName);
        Assert.Equal(email, doc.Descendants().Single(e => e.Name.LocalName == "EMailAddress").Value);
        var protocols = doc.Descendants().Where(e => e.Name.LocalName == "Protocol").ToArray();
        Assert.Equal(new[] { "IMAP", "SMTP", "SMTP" }, protocols.Select(p => p.Element(p.Name.Namespace + "Type")!.Value));
        Assert.Equal(new[] { "993", "465", "587" }, protocols.Select(p => p.Element(p.Name.Namespace + "Port")!.Value));
        Assert.All(protocols, p =>
        {
            Assert.Equal("on", p.Element(p.Name.Namespace + "SSL")!.Value);
            Assert.Equal("off", p.Element(p.Name.Namespace + "SPA")!.Value);
            Assert.Equal("on", p.Element(p.Name.Namespace + "AuthRequired")!.Value);
        });
    }

    [Fact]
    public async Task Unknown_domain_returns_not_found()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/.well-known/autoconfig/mail/config-v1.1.xml?emailaddress=x@missing-autodisc.test");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task SeedDomainAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")).Options;
        await using var db = new AppDbContext(options);
        db.Tenants.Add(new Tenant { Id = _tenantId, Slug = $"ad-{Guid.NewGuid():N}" });
        db.Domains.Add(new Miautrix.Mail.Domain.Domain { TenantId = _tenantId, Name = HostDomain });
        await db.SaveChangesAsync();
    }
}

[CollectionDefinition("AutodiscoveryContract", DisableParallelization = true)]
public sealed class AutodiscoveryContractCollection;

[Trait("Category", "Autodiscovery")]
public sealed class AutodiscoveryRateLimitTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;
    public AutodiscoveryRateLimitTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory) => _factory = factory;

    [Fact]
    public async Task Discovery_is_rate_limited_by_client_ip()
    {
        using var client = _factory.CreateClient();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 70; i++)
        {
            last?.Dispose();
            last = await client.GetAsync($"/.well-known/autoconfig/mail/config-v1.1.xml?emailaddress=x@rate-check-{i}.test");
            if (last.StatusCode == HttpStatusCode.TooManyRequests) break;
        }
        using (last) Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }
}
