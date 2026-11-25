using System.Net;
using System.Xml;
using System.Xml.Linq;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.IntegrationTests.Api;

[Trait("Category", "MobileConfig")]
public sealed class MobileConfigTests : IClassFixture<WebApplicationFactory<Miautrix.Mail.Web.Program>>
{
    private readonly WebApplicationFactory<Miautrix.Mail.Web.Program> _factory;
    public MobileConfigTests(WebApplicationFactory<Miautrix.Mail.Web.Program> factory)
    {
        Environment.SetEnvironmentVariable("MIAUTRIX_PUBLIC_IMAP_HOST", "imap.mailhost.test");
        Environment.SetEnvironmentVariable("MIAUTRIX_PUBLIC_SMTP_HOST", "smtp.mailhost.test");
        _factory = factory;
    }

    [Fact]
    public async Task Mobileconfig_requires_request_identity()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/webmail/mobileconfig");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mobileconfig_only_contains_the_callers_email_and_no_password()
    {
        var (tenantId, userId, email) = await SeedUserAsync();
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/webmail/mobileconfig");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Headers.Add("X-User-Id", userId.ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-apple-aspen-config", response.Content.Headers.ContentType!.MediaType);
        var profileBytes = await response.Content.ReadAsByteArrayAsync();
        var profile = ParseProfile(profileBytes);
        var profileDictionary = profile.Root!.Element("dict")!;
        Assert.Equal("Configuration", PlistValue(profileDictionary, "PayloadType")!.Value);
        Assert.Equal("1", PlistValue(profileDictionary, "PayloadVersion")!.Value);
        Assert.False(string.IsNullOrWhiteSpace(PlistValue(profileDictionary, "PayloadDisplayName")!.Value));
        var profileUuid = PlistValue(profileDictionary, "PayloadUUID")!.Value;
        Assert.True(Guid.TryParse(profileUuid, out _));
        var mail = profileDictionary.Element("array")!.Element("dict")!;
        Assert.Equal("com.apple.mail.managed", PlistValue(mail, "PayloadType")!.Value);
        Assert.Equal("EmailTypeIMAP", PlistValue(mail, "EmailAccountType")!.Value);
        Assert.Equal("EmailAuthPassword", PlistValue(mail, "IncomingMailServerAuthentication")!.Value);
        Assert.Equal("EmailAuthPassword", PlistValue(mail, "OutgoingMailServerAuthentication")!.Value);
        Assert.Equal("true", PlistValue(mail, "OutgoingPasswordSameAsIncomingPassword")!.Name.LocalName);
        Assert.Equal(email, PlistValue(mail, "EmailAddress")!.Value);
        Assert.Equal(email, PlistValue(mail, "IncomingMailServerUsername")!.Value);
        Assert.Equal(email, PlistValue(mail, "OutgoingMailServerUsername")!.Value);
        Assert.Equal("993", PlistValue(mail, "IncomingMailServerPortNumber")!.Value);
        Assert.Equal("465", PlistValue(mail, "OutgoingMailServerPortNumber")!.Value);
        Assert.DoesNotContain(mail.Elements("key"), key => key.Value.Contains("Password", StringComparison.Ordinal) && key.Value != "OutgoingPasswordSameAsIncomingPassword");

        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/webmail/mobileconfig");
        secondRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        secondRequest.Headers.Add("X-User-Id", userId.ToString());
        using var secondResponse = await client.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondProfile = ParseProfile(await secondResponse.Content.ReadAsByteArrayAsync()).Root!.Element("dict")!;
        Assert.Equal(profileUuid, PlistValue(secondProfile, "PayloadUUID")!.Value);
        Assert.Equal(PlistValue(mail, "PayloadUUID")!.Value, PlistValue(secondProfile.Element("array")!.Element("dict")!, "PayloadUUID")!.Value);
    }

    [Fact]
    public async Task Mobileconfig_rejects_cross_tenant_user_identity()
    {
        var (tenantId, userId, _) = await SeedUserAsync();
        var otherTenant = Guid.NewGuid();
        await using (var db = CreateContext())
        {
            db.Tenants.Add(new Tenant { Id = otherTenant, Slug = $"other-{Guid.NewGuid():N}" });
            await db.SaveChangesAsync();
        }
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/webmail/mobileconfig");
        request.Headers.Add("X-Tenant-Id", otherTenant.ToString());
        request.Headers.Add("X-User-Id", userId.ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(tenantId, otherTenant);
    }

    private static async Task<(Guid Tenant, Guid User, string Email)> SeedUserAsync()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"user-{Guid.NewGuid():N}@mobile-profile.test";
        await using var db = CreateContext();
        db.Tenants.Add(new Tenant { Id = tenantId, Slug = $"mobile-{Guid.NewGuid():N}" });
        db.Users.Add(new User { Id = userId, TenantId = tenantId, Email = email, Name = "Mobile Test User" });
        await db.SaveChangesAsync();
        return (tenantId, userId, email);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION")).Options);

    private static XDocument ParseProfile(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null });
        var document = XDocument.Load(reader);
        Assert.Equal("-//Apple//DTD PLIST 1.0//EN", document.DocumentType?.PublicId);
        Assert.Equal("plist", document.Root?.Name.LocalName);
        return document;
    }

    private static XElement? PlistValue(XElement dictionary, string key)
    {
        var children = dictionary.Elements().ToArray();
        for (var index = 0; index + 1 < children.Length; index += 2)
        {
            if (children[index].Name.LocalName == "key" && children[index].Value == key) return children[index + 1];
        }
        return null;
    }
}
