using System.Text;
using System.Xml;
using System.Xml.Linq;
using Miautrix.Mail.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Miautrix.Mail.Web.Infrastructure;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
public sealed class AutodiscoveryController(AppDbContext db, IConfiguration configuration, IRequestContextAccessor context) : ControllerBase
{
    [HttpGet(DiscoveryRoutes.WellKnownAutoconfig)]
    [HttpGet(DiscoveryRoutes.MailAutoconfig)]
    [EnableRateLimiting("discovery")]
    public async Task<IActionResult> Autoconfig([FromQuery] string? emailaddress, CancellationToken ct)
    {
        var domainName = EmailDomain(emailaddress);
        var domain = domainName is null ? null : await db.Domains.AsNoTracking().FirstOrDefaultAsync(d => d.Name.ToLower() == domainName, ct);
        if (domain is null) return NotFound();
        var imap = PublicHost("IMAP"); var smtp = PublicHost("SMTP");
        var doc = new XDocument(new XElement("clientConfig", new XAttribute("version", "1.1"),
            new XElement("emailProvider", new XAttribute("id", domain.Name), new XElement("domain", domain.Name),
                new XElement("displayName", domain.Name), new XElement("displayShortName", domain.Name),
                Server("incomingServer", "imap", imap, 993, "SSL", "%EMAILADDRESS%", "plain"),
                Server("outgoingServer", "smtp", smtp, 465, "SSL", "%EMAILADDRESS%", "plain"),
                Server("outgoingServer", "smtp", smtp, 587, "STARTTLS", "%EMAILADDRESS%", "plain"))));
        return Content(doc.ToString(SaveOptions.DisableFormatting), "application/xml", Encoding.UTF8);
    }

    [HttpGet(DiscoveryRoutes.OutlookAutodiscover)]
    [HttpPost(DiscoveryRoutes.OutlookAutodiscover)]
    [EnableRateLimiting("discovery")]
    public async Task<IActionResult> Outlook(CancellationToken ct)
    {
        string? email = null;
        try
        {
            if (Request.ContentLength is > 65536) return BadRequest();
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var body = await reader.ReadToEndAsync(ct);
            using var stringReader = new StringReader(body);
            using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
            var xml = XDocument.Load(xmlReader, LoadOptions.None);
            email = xml.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("EMailAddress", StringComparison.OrdinalIgnoreCase))?.Value;
        }
        catch { return BadRequest(); }
        var domainName = EmailDomain(email);
        var domain = domainName is null ? null : await db.Domains.AsNoTracking().FirstOrDefaultAsync(d => d.Name.ToLower() == domainName, ct);
        if (domain is null) return NotFound();
        var imap = PublicHost("IMAP"); var smtp = PublicHost("SMTP");
        XNamespace ns = "http://schemas.microsoft.com/exchange/autodiscover/outlook/responseschema/2006a";
        var response = new XDocument(new XElement(ns + "Autodiscover",
            new XElement(ns + "Response", new XElement(ns + "Account", new XElement(ns + "AccountType", "email"), new XElement(ns + "Action", "settings"),
                Protocol(ns, "IMAP", imap, 993), Protocol(ns, "SMTP", smtp, 465), Protocol(ns, "SMTP", smtp, 587)))));
        // Preserve the submitted address in the POX response.
        response.Root!.Element(ns + "Response")!.AddFirst(new XElement(ns + "User", new XElement(ns + "EMailAddress", email)));
        return Content(response.ToString(SaveOptions.DisableFormatting), "text/xml", Encoding.UTF8);
    }

    [HttpGet(DiscoveryRoutes.MtaStsPolicy)]
    [EnableRateLimiting("discovery")]
    public async Task<IActionResult> MtaSts(CancellationToken ct)
    {
        var host = Request.Host.Host;
        if (!host.StartsWith("mta-sts.", StringComparison.OrdinalIgnoreCase)) return NotFound();
        var domainName = host[8..].ToLowerInvariant();
        var domain = await db.Domains.AsNoTracking().FirstOrDefaultAsync(d => d.Name.ToLower() == domainName && d.MtaStsMode != "none", ct);
        if (domain is null) return NotFound();
        var mxHosts = string.IsNullOrWhiteSpace(domain.MtaStsMx)
            ? [PublicHost("SMTP")]
            : domain.MtaStsMx.Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var text = $"version: STSv1\nmode: {domain.MtaStsMode}\n{string.Join("\n", mxHosts.Select(mx => $"mx: {mx}"))}\nmax_age: {domain.MtaStsMaxAge}\n";
        return Content(text, "text/plain", Encoding.UTF8);
    }

    [HttpGet("/api/v1/webmail/mobileconfig")]
    public async Task<IActionResult> MobileConfig(CancellationToken ct)
    {
        // Identity comes only from the authenticated session (SessionAuthenticationMiddleware).
        var userId = context.CurrentUserId;
        var tenantId = context.CurrentTenantId;
        var email = await db.Users.AsNoTracking().Where(u => u.TenantId == tenantId && u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (email is null) return NotFound();
        var domain = EmailDomain(email); if (domain is null) return NotFound();
        var imap = PublicHost("IMAP"); var smtp = PublicHost("SMTP");
        var payloadId = DeterministicPayloadGuid(userId, "mail");
        var profileId = DeterministicPayloadGuid(userId, "configuration");
        var profile = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            new XElement("plist", new XAttribute("version", "1.0"),
                new XElement("dict",
                    PlistKey("PayloadContent"), new XElement("array",
                        new XElement("dict",
                            PlistKey("EmailAccountType"), PlistString("EmailTypeIMAP"),
                            PlistKey("EmailAddress"), PlistString(email),
                            PlistKey("EmailAccountDescription"), PlistString($"Miautrix Mail - {domain}"),
                            PlistKey("IncomingMailServerAuthentication"), PlistString("EmailAuthPassword"),
                            PlistKey("IncomingMailServerHostName"), PlistString(imap),
                            PlistKey("IncomingMailServerPortNumber"), PlistInteger(993),
                            PlistKey("IncomingMailServerUseSSL"), new XElement("true"),
                            PlistKey("IncomingMailServerUsername"), PlistString(email),
                            PlistKey("OutgoingMailServerAuthentication"), PlistString("EmailAuthPassword"),
                            PlistKey("OutgoingMailServerHostName"), PlistString(smtp),
                            PlistKey("OutgoingMailServerPortNumber"), PlistInteger(465),
                            PlistKey("OutgoingMailServerUseSSL"), new XElement("true"),
                            PlistKey("OutgoingMailServerUsername"), PlistString(email),
                            PlistKey("OutgoingPasswordSameAsIncomingPassword"), new XElement("true"),
                            PlistKey("PayloadDisplayName"), PlistString($"Mail account for {email}"),
                            PlistKey("PayloadIdentifier"), PlistString($"org.miautrix.mail.account.{userId:N}"),
                            PlistKey("PayloadType"), PlistString("com.apple.mail.managed"),
                            PlistKey("PayloadUUID"), PlistString(payloadId.ToString().ToUpperInvariant()),
                            PlistKey("PayloadVersion"), PlistInteger(1))),
                    PlistKey("PayloadDisplayName"), PlistString($"Miautrix Mail - {domain}"),
                    PlistKey("PayloadIdentifier"), PlistString($"org.miautrix.mail.configuration.{userId:N}"),
                    PlistKey("PayloadUUID"), PlistString(profileId.ToString().ToUpperInvariant()),
                    PlistKey("PayloadVersion"), PlistInteger(1),
                    PlistKey("PayloadType"), PlistString("Configuration"))));
        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = false, Indent = false }))
            profile.Save(writer);
        return File(output.ToArray(), "application/x-apple-aspen-config", $"{domain}.mobileconfig");
    }

    private string PublicHost(string protocol) => configuration[$"MIAUTRIX_PUBLIC_{protocol}_HOST"] ?? Environment.GetEnvironmentVariable("MIAUTRIX_HOSTNAME") ?? throw new InvalidOperationException("MIAUTRIX_HOSTNAME is required for client discovery.");
    private static string? EmailDomain(string? email) { var at = email?.LastIndexOf('@') ?? -1; return at <= 0 || at == email!.Length - 1 ? null : email[(at + 1)..].Trim().ToLowerInvariant(); }
    private static XElement PlistKey(string key) => new("key", key);
    private static XElement PlistString(string value) => new("string", value);
    private static XElement PlistInteger(int value) => new("integer", value);
    private static Guid DeterministicPayloadGuid(Guid userId, string purpose)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes($"miautrix-mail:{userId:N}:{purpose}"));
        var guid = bytes[..16];
        guid[6] = (byte)((guid[6] & 0x0F) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3F) | 0x80);
        return new Guid(guid);
    }
    private static XElement Server(string type, string name, string host, int port, string socket, string username, string auth) => new(type, new XAttribute("type", name), new XElement("hostname", host), new XElement("port", port), new XElement("socketType", socket), new XElement("username", username), new XElement("authentication", auth));
    private static XElement Protocol(XNamespace ns, string type, string host, int port) => new(ns + "Protocol", new XElement(ns + "Type", type), new XElement(ns + "Server", host), new XElement(ns + "Port", port), new XElement(ns + "SSL", "on"), new XElement(ns + "SPA", "off"), new XElement(ns + "AuthRequired", "on"));
}
