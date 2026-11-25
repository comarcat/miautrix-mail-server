using Miautrix.Mail.Infrastructure.Mail;
using Miautrix.Mail.Application.Mail.Mime;
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

    [Fact]
    public void Decodes_binary_sections_and_reports_unknown_transfer_encoding()
    {
        var formatter = new MimeKitImapMimeFormatter();
        var encoded = Encoding.ASCII.GetBytes("Content-Type: text/plain\r\nContent-Transfer-Encoding: base64\r\n\r\nSGVsbG8gYmluYXJ5\r\n");
        var unknown = Encoding.ASCII.GetBytes("Content-Type: application/octet-stream\r\nContent-Transfer-Encoding: x-unknown\r\n\r\nabc\r\n");

        Assert.True(formatter.TryGetBinarySection(encoded, "1", out var decoded));
        Assert.Equal("Hello binary", Encoding.ASCII.GetString(decoded).Trim());
        Assert.False(formatter.TryGetBinarySection(unknown, "1", out _));
    }

    [Fact]
    public void Binary_sections_require_leaf_parts_and_normalize_text_line_endings()
    {
        var formatter = new MimeKitImapMimeFormatter();
        var multipart = Encoding.ASCII.GetBytes("Content-Type: multipart/mixed; boundary=x\r\n\r\n--x\r\nContent-Type: text/plain\r\n\r\nA\nB\r\n--x--\r\n");
        var text = Encoding.ASCII.GetBytes("Content-Type: text/plain\r\n\r\nA\nB\r");

        Assert.Equal(ImapBinarySectionStatus.NotLeafBodyPart, formatter.GetBinarySection(multipart, "", out _));
        Assert.Equal(ImapBinarySectionStatus.Success, formatter.GetBinarySection(text, "1", out var decoded));
        Assert.Equal("A\r\nB\r\n", Encoding.ASCII.GetString(decoded));
    }

    [Fact]
    public void Binary_sections_reject_multipart_and_message_entities_but_allow_nested_leaf_parts()
    {
        var formatter = new MimeKitImapMimeFormatter();
        var multipart = Encoding.ASCII.GetBytes("Content-Type: multipart/mixed; boundary=x\r\n\r\n--x\r\nContent-Type: text/plain\r\n\r\none\r\n--x\r\nContent-Type: text/plain\r\n\r\ntwo\r\n--x--\r\n");
        var nestedMessage = Encoding.ASCII.GetBytes("Content-Type: message/rfc822\r\n\r\nFrom: nested@example.test\r\nContent-Type: multipart/mixed; boundary=y\r\n\r\n--y\r\nContent-Type: text/plain\r\n\r\nnested leaf\r\n--y--\r\n");
        var globalMessage = Encoding.ASCII.GetBytes("Content-Type: message/global\r\n\r\nFrom: nested@example.test\r\nSubject: global\r\n\r\nmessage bytes\r\n");
        var notification = Encoding.ASCII.GetBytes("Content-Type: message/disposition-notification\r\n\r\nReporting-UA: mail.example.test\r\n");

        Assert.Equal(ImapBinarySectionStatus.NotLeafBodyPart, formatter.GetBinarySection(multipart, "", out _));
        Assert.Equal(ImapBinarySectionStatus.Success, formatter.GetBinarySection(multipart, "2", out var secondPart));
        Assert.Equal("two", Encoding.ASCII.GetString(secondPart));
        Assert.Equal(ImapBinarySectionStatus.NotLeafBodyPart, formatter.GetBinarySection(nestedMessage, "1", out _));
        Assert.Equal(ImapBinarySectionStatus.Success, formatter.GetBinarySection(nestedMessage, "1.1", out var nestedLeaf));
        Assert.Equal("nested leaf", Encoding.ASCII.GetString(nestedLeaf));
        Assert.Equal(ImapBinarySectionStatus.NotLeafBodyPart, formatter.GetBinarySection(globalMessage, "1", out _));
        Assert.Equal(ImapBinarySectionStatus.Success, formatter.GetBinarySection(notification, "1", out _));
    }
}
