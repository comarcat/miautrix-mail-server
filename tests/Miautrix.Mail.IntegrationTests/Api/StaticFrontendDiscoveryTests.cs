using System.Net;
using System.Text;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Miautrix.Mail.IntegrationTests.Api;

[Trait("Category", "Autodiscovery")]
[Collection("StaticFrontendDiscovery")]
public sealed class StaticFrontendDiscoveryTests
{
    [Fact]
    public async Task Static_frontends_fall_through_for_discovery_and_keep_spa_assets_and_admin_working()
    {
        var root = Path.Combine(Path.GetTempPath(), $"miautrix-static-test-{Guid.NewGuid():N}");
        var webmail = Path.Combine(root, "webmail");
        var admin = Path.Combine(root, "admin");
        Directory.CreateDirectory(Path.Combine(webmail, "assets"));
        Directory.CreateDirectory(admin);
        await File.WriteAllTextAsync(Path.Combine(webmail, "index.html"), "WEBMAIL-SPA-INDEX");
        await File.WriteAllTextAsync(Path.Combine(webmail, "assets", "x.js"), "window.assetLoaded = true;");
        await File.WriteAllTextAsync(Path.Combine(admin, "index.html"), "ADMIN-SPA-INDEX");

        try
        {
            var domainName = $"static-{Guid.NewGuid():N}.test";
            await SeedDomainAsync(domainName);
            using var factory = new WebApplicationFactory<Miautrix.Mail.Web.Program>().WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MIAUTRIX_WEBMAIL_DIR"] = webmail,
                    ["MIAUTRIX_ADMIN_DIR"] = admin,
                    ["MIAUTRIX_PUBLIC_IMAP_HOST"] = "imap.mailhost.test",
                    ["MIAUTRIX_PUBLIC_SMTP_HOST"] = "smtp.mailhost.test",
                })));
            using var client = factory.CreateClient();

            foreach (var path in new[]
            {
                $"/.well-known/autoconfig/mail/config-v1.1.xml?emailaddress=user@{domainName}",
                $"/mail/config-v1.1.xml?emailaddress=user@{domainName}",
            })
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Contains("clientConfig", await response.Content.ReadAsStringAsync());
                Assert.NotEqual("WEBMAIL-SPA-INDEX", await response.Content.ReadAsStringAsync());
            }

            using (var outlookRequest = new HttpRequestMessage(HttpMethod.Post, "/AutoDiscover/AutoDiscover.XML")
            {
                Content = new StringContent($"<Autodiscover><Request><EMailAddress>user@{domainName}</EMailAddress></Request></Autodiscover>", Encoding.UTF8, "text/xml")
            })
            using (var response = await client.SendAsync(outlookRequest))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Contains("responseschema/2006a", await response.Content.ReadAsStringAsync());
            }

            using (var policyRequest = new HttpRequestMessage(HttpMethod.Get, "/.well-known/mta-sts.txt"))
            {
                policyRequest.Headers.Host = $"mta-sts.{domainName}";
                using var response = await client.SendAsync(policyRequest);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
                Assert.StartsWith("version: STSv1\nmode: testing\n", await response.Content.ReadAsStringAsync());
            }

            foreach (var path in new[] { "/", "/deep/spa/route" })
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("WEBMAIL-SPA-INDEX", await response.Content.ReadAsStringAsync());
            }

            using (var response = await client.GetAsync("/admin/"))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("ADMIN-SPA-INDEX", await response.Content.ReadAsStringAsync());
            }

            using (var response = await client.GetAsync("/assets/x.js"))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("window.assetLoaded = true;", await response.Content.ReadAsStringAsync());
                Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
                Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
                Assert.Contains("Content-Security-Policy", response.Headers.Select(h => h.Key));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task SeedDomainAsync(string domainName)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")).Options);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Slug = $"static-{Guid.NewGuid():N}" });
        db.Domains.Add(new Miautrix.Mail.Domain.Domain { TenantId = tenantId, Name = domainName, MtaStsMode = "testing", MtaStsMx = "mx.test" });
        await db.SaveChangesAsync();
    }
}

[CollectionDefinition("StaticFrontendDiscovery", DisableParallelization = true)]
public sealed class StaticFrontendDiscoveryCollection;
