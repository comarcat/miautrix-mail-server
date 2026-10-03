using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Miautrix.Mail.Application.Mail.Mime;
using Xunit;

namespace Miautrix.Mail.UnitTests.Calendar;

[Trait("Category", "Calendar")]
public sealed class MimeMessageBuilderTests
{
    private static MimeMessageRequest Request(
        string? text = "Hello",
        string? html = null,
        IReadOnlyList<MimeAttachment>? attachments = null,
        IReadOnlyList<string>? cc = null,
        IReadOnlyList<string>? bcc = null,
        string subject = "Subject",
        string from = "sender@example.com") =>
        new(
            From: from,
            To: new[] { "to@example.com" },
            Cc: cc,
            Bcc: bcc,
            Subject: subject,
            BodyText: text,
            BodyHtml: html,
            Attachments: attachments,
            Date: DateTimeOffset.UtcNow);

    [Fact]
    public void Text_only_produces_plain_text_content_type()
    {
        var built = MimeMessageBuilder.Build(Request(text: "Plain body", html: null));

        Assert.Contains("Content-Type: text/plain; charset=utf-8", built.RawMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("multipart", built.RawMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_only_produces_html_content_type()
    {
        var built = MimeMessageBuilder.Build(Request(text: null, html: "<p>Hi</p>"));

        Assert.Contains("Content-Type: text/html; charset=utf-8", built.RawMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_and_html_produce_multipart_alternative()
    {
        var built = MimeMessageBuilder.Build(Request(text: "Plain", html: "<p>Rich</p>"));

        Assert.Contains("multipart/alternative", built.RawMessage, StringComparison.Ordinal);
        Assert.Contains("text/plain", built.RawMessage, StringComparison.Ordinal);
        Assert.Contains("text/html", built.RawMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Attachment_produces_multipart_mixed_with_base64()
    {
        var attachment = new MimeAttachment("report.pdf", "application/pdf", new byte[] { 1, 2, 3, 4, 5 });
        var built = MimeMessageBuilder.Build(Request(text: "Body", attachments: new[] { attachment }));

        Assert.Contains("multipart/mixed", built.RawMessage, StringComparison.Ordinal);
        Assert.Contains("Content-Transfer-Encoding: base64", built.RawMessage, StringComparison.Ordinal);
        Assert.Contains("report.pdf", built.RawMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Calendar_attachment_lives_inside_alternative_part()
    {
        var ics = new MimeAttachment("invitation.ics", "text/calendar; method=REQUEST", Encoding.UTF8.GetBytes("BEGIN:VCALENDAR"), Method: "REQUEST");
        var built = MimeMessageBuilder.Build(Request(text: "Body", html: "<p>Body</p>", attachments: new[] { ics }));

        Assert.Contains("multipart/mixed", built.RawMessage, StringComparison.Ordinal);
        Assert.Contains("text/calendar", built.RawMessage, StringComparison.Ordinal);
        Assert.Contains("method=REQUEST", built.RawMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void All_lines_use_crlf()
    {
        var built = MimeMessageBuilder.Build(Request(text: "Line one\nLine two", html: "<p>x</p>"));

        // Every LF must be preceded by CR.
        var raw = built.RawMessage;
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '\n')
            {
                Assert.True(i > 0 && raw[i - 1] == '\r', $"Bare LF at index {i}");
            }
        }
    }

    [Fact]
    public void Long_subject_is_folded_below_line_limit()
    {
        var subject = string.Join(" ", Enumerable.Repeat("scheduling", 20));
        var built = MimeMessageBuilder.Build(Request(subject: subject));

        var subjectLines = built.TopLevelHeaders
            .Split("\r\n")
            .SkipWhile(l => !l.StartsWith("Subject:", StringComparison.Ordinal))
            .TakeWhile((l, idx) => idx == 0 || l.StartsWith(" ", StringComparison.Ordinal))
            .ToList();

        Assert.True(subjectLines.Count > 1, "Long subject should fold onto continuation lines.");
        Assert.All(subjectLines, line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 78));
    }

    [Fact]
    public void Non_ascii_subject_is_encoded_word()
    {
        var built = MimeMessageBuilder.Build(Request(subject: "Réunion café ☕"));

        Assert.Contains("=?UTF-8?B?", built.RawMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Base64_wraps_at_76_characters()
    {
        var attachment = new MimeAttachment("blob.bin", "application/octet-stream", Enumerable.Range(0, 500).Select(i => (byte)i).ToArray());
        var built = MimeMessageBuilder.Build(Request(text: "Body", attachments: new[] { attachment }));

        // The base64 body block lines must not exceed 76 chars.
        var lines = built.RawMessage.Split("\r\n");
        var base64Lines = lines.Where(l => l.Length > 0 && l.All(c =>
            char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '='));
        Assert.All(base64Lines, line => Assert.True(line.Length <= 76, $"Line too long: {line.Length}"));
    }

    [Fact]
    public void Bcc_is_never_emitted_in_headers()
    {
        var built = MimeMessageBuilder.Build(Request(bcc: new[] { "secret@example.com" }));

        Assert.DoesNotContain("secret@example.com", built.RawMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bcc:", built.RawMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cc_is_emitted_in_headers()
    {
        var built = MimeMessageBuilder.Build(Request(cc: new[] { "cc@example.com" }));

        Assert.Contains("Cc:", built.RawMessage, StringComparison.Ordinal);
        Assert.Contains("cc@example.com", built.RawMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("evil@example.com\r\nBcc: victim@example.com")]
    [InlineData("evil@example.com\nInjected: header")]
    public void Header_injection_in_recipient_is_rejected(string injected)
    {
        var request = new MimeMessageRequest(
            From: "sender@example.com",
            To: new[] { injected },
            Cc: null,
            Bcc: null,
            Subject: "Subject",
            BodyText: "Body",
            BodyHtml: null,
            Attachments: null,
            Date: DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => MimeMessageBuilder.Build(request));
    }

    [Fact]
    public void Subject_with_crlf_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => MimeMessageBuilder.Build(Request(subject: "Hello\r\nInjected: value")));
    }

    [Fact]
    public void Size_bytes_matches_raw_utf8_length()
    {
        var built = MimeMessageBuilder.Build(Request(text: "Body", html: "<p>Body</p>"));

        Assert.Equal(Encoding.UTF8.GetByteCount(built.RawMessage), built.SizeBytes);
    }

    [Fact]
    public void Message_id_uses_sender_domain()
    {
        var built = MimeMessageBuilder.Build(Request(from: "sender@corp.example"));

        Assert.EndsWith("@corp.example>", built.MessageId, StringComparison.Ordinal);
    }
}
