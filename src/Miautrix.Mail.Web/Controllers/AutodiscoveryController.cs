using System.Net;
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
[EnableRateLimiting("discovery")]
public sealed class AutodiscoveryController(AppDbContext db, IConfiguration configuration, IRequestContextAccessor context) : ControllerBase
{
    [HttpGet("/.well-known/autoconfig/mail/config-v1.1.xml")]
    [HttpGet("/mail/config-v1.1.xml")]
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

    [HttpGet("/autodiscover/autodiscover.xml")]
    [HttpPost("/autodiscover/autodiscover.xml")]
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

    [HttpGet("/.well-known/mta-sts.txt")]
    public async Task<IActionResult> MtaSts(CancellationToken ct)
    {
        var host = Request.Host.Host;
        if (!host.StartsWith("mta-sts.", StringComparison.OrdinalIgnoreCase)) return NotFound();
        var domainName = host[8..].ToLowerInvariant();
        var domain = await db.Domains.AsNoTracking().FirstOrDefaultAsync(d => d.Name.ToLower() == domainName && d.MtaStsMode != "none", ct);
        if (domain is null) return NotFound();
        var mx = PublicHost("SMTP");
        var text = $"version: STSv1\nmode: {domain.MtaStsMode}\nmx: {mx}\nmax_age: {domain.MtaStsMaxAge}\n";
        return Content(text, "text/plain", Encoding.UTF8);
    }

    [HttpGet("/api/v1/webmail/mobileconfig")]
    public async Task<IActionResult> MobileConfig(CancellationToken ct)
    {
        if (context.CurrentUserId == Guid.Empty || context.CurrentTenantId == Guid.Empty) return Unauthorized();
        var email = await db.Users.AsNoTracking().Where(u => u.TenantId == context.CurrentTenantId && u.Id == context.CurrentUserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (email is null) return NotFound();
        var domain = EmailDomain(email); if (domain is null) return NotFound();
        var imap = PublicHost("IMAP"); var smtp = PublicHost("SMTP");
        var payload = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict><key>PayloadContent</key><array><dict><key>EmailAddress</key><string>{WebUtility.HtmlEncode(email)}</string><key>IncomingMailServerHostName</key><string>{imap}</string><key>IncomingMailServerPortNumber</key><integer>993</integer><key>IncomingMailServerUseSSL</key><true/><key>IncomingMailServerUsername</key><string>{WebUtility.HtmlEncode(email)}</string><key>OutgoingMailServerHostName</key><string>{smtp}</string><key>OutgoingMailServerPortNumber</key><integer>465</integer><key>OutgoingMailServerUseSSL</key><true/><key>OutgoingMailServerUsername</key><string>{WebUtility.HtmlEncode(email)}</string></dict></array><key>PayloadType</key><string>Configuration</string></dict></plist>";
        return File(Encoding.UTF8.GetBytes(payload), "application/x-apple-aspen-config", $"{domain}.mobileconfig");
    }

    private string PublicHost(string protocol) => configuration[$"MIAUTRIX_PUBLIC_{protocol}_HOST"] ?? Environment.GetEnvironmentVariable("MIAUTRIX_HOSTNAME") ?? throw new InvalidOperationException("MIAUTRIX_HOSTNAME is required for client discovery.");
    private static string? EmailDomain(string? email) { var at = email?.LastIndexOf('@') ?? -1; return at <= 0 || at == email!.Length - 1 ? null : email[(at + 1)..].Trim().ToLowerInvariant(); }
    private static XElement Server(string type, string name, string host, int port, string socket, string username, string auth) => new(type, new XAttribute("type", name), new XElement("hostname", host), new XElement("port", port), new XElement("socketType", socket), new XElement("username", username), new XElement("authentication", auth));
    private static XElement Protocol(XNamespace ns, string type, string host, int port) => new(ns + "Protocol", new XElement(ns + "Type", type), new XElement(ns + "Server", host), new XElement(ns + "Port", port), new XElement(ns + "SSL", "on"), new XElement(ns + "SPA", "off"), new XElement(ns + "AuthRequired", "on"));
}
