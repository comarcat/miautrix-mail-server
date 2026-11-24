using Miautrix.Mail.Infrastructure.Mail;
using System.Text;

namespace Miautrix.Mail.UnitTests.Imap;

public sealed class ImapMimeFormatterTests
{
    [Fact]
    public void Builds_envelope_structure_and_body_section_from_mimekit()
    {
        var raw = Encoding.UTF8.GetBytes("From: Alice <alice@example.test>\r\nTo: Bob <bob@example.test>\r\nSubject: Café\r\nMessage-ID: <m1@example.test>\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nHello π\r\n");
        var formatter = new MimeKitImapMimeFormatter();

        var envelope = formatter.BuildEnvelope(raw);
        var structure = formatter.BuildBodyStructure(raw);
        var body = formatter.GetBodySection(raw, "1");

        Assert.Contains("Café", envelope);
        Assert.Contains("alice", envelope);
        Assert.Contains("TEXT", structure);
        Assert.Contains("Hello π", Encoding.UTF8.GetString(body));
    }

    [Fact]
    public void Builds_message_rfc822_body_structure_and_nested_message_section()
    {
        var raw = Encoding.UTF8.GetBytes("From: forwarder@example.test\r\nContent-Type: message/rfc822\r\n\r\nFrom: Alice <alice@example.test>\r\nTo: Bob <bob@example.test>\r\nSubject: nested\r\nContent-Type: text/plain\r\n\r\nNested body\r\n");
        var formatter = new MimeKitImapMimeFormatter();

        var structure = formatter.BuildBodyStructure(raw);
        var nested = formatter.GetBodySection(raw, "1");
        var nestedText = formatter.GetBodySection(raw, "1.1");

        Assert.Contains("MESSAGE", structure);
        Assert.Contains("nested", structure);
        Assert.Contains("Subject: nested", Encoding.UTF8.GetString(nested));
        Assert.Contains("Nested body", Encoding.UTF8.GetString(nested));
        Assert.Contains("Nested body", Encoding.UTF8.GetString(nestedText));
    }
}
