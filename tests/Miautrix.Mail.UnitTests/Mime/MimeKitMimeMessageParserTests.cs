using Miautrix.Mail.Infrastructure.Mail;

namespace Miautrix.Mail.UnitTests.Mime;

public sealed class MimeKitMimeMessageParserTests
{
    private readonly MimeKitMimeMessageParser _parser = new();

    [Fact]
    public void Decodes_single_part_quoted_printable_html_and_utf8()
    {
        const string raw = "MIME-Version: 1.0\r\nContent-Type: text/html; charset=utf-8\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\n<p>Hi=20caf=C3=A9 =3D ok=\r\n!</p>";
        var parsed = _parser.Parse(raw);
        Assert.Contains("Hi café = ok!", parsed.BodyHtml);
    }

    [Fact]
    public void Decodes_single_part_base64_html()
    {
        const string raw = "MIME-Version: 1.0\r\nContent-Type: text/html; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\nPHA+SGVsbG88L3A+";
        Assert.Equal("<p>Hello</p>", _parser.Parse(raw).BodyHtml);
    }

    [Fact]
    public void Parses_multipart_alternative_and_nested_mixed()
    {
        const string raw = "MIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=outer\r\n\r\n--outer\r\nContent-Type: multipart/alternative; boundary=inner\r\n\r\n--inner\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nplain\r\n--inner\r\nContent-Type: text/html; charset=utf-8\r\n\r\n<b>html</b>\r\n--inner--\r\n--outer--\r\n";
        var parsed = _parser.Parse(raw);
        Assert.Equal("plain", parsed.BodyText);
        Assert.Equal("<b>html</b>", parsed.BodyHtml);
    }

    [Theory]
    [InlineData("iso-8859-1", "caf=E9", "café")]
    [InlineData("windows-1252", "price=80", "price€")]
    public void Honors_declared_charset(string charset, string encoded, string expected)
    {
        var parsed = _parser.Parse($"Content-Type: text/plain; charset={charset}\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\n{encoded}");
        Assert.Equal(expected, parsed.BodyText);
    }

    [Fact]
    public void Decodes_encoded_word_subjects()
    {
        var parsed = _parser.Parse("Subject: =?UTF-8?B?5pel5pys?= =?UTF-8?Q?caf=C3=A9?=\r\n\r\nbody");
        Assert.Equal("日本café", parsed.Subject);
    }

    [Fact]
    public void Does_not_treat_body_boundary_like_line_as_mime_boundary()
    {
        var parsed = _parser.Parse("Content-Type: text/plain\r\n\r\n--this-is-body-text\r\nhello");
        Assert.Contains("--this-is-body-text", parsed.BodyText);
    }

    [Fact]
    public void Returns_attachments_and_inline_images()
    {
        const string raw = "MIME-Version: 1.0\r\nContent-Type: multipart/related; boundary=x\r\n\r\n--x\r\nContent-Type: text/html\r\n\r\n<img src=\"cid:pic\">\r\n--x\r\nContent-Type: image/png\r\nContent-Disposition: inline; filename=pic.png\r\nContent-ID: <pic>\r\nContent-Transfer-Encoding: base64\r\n\r\naGVsbG8=\r\n--x--\r\n";
        var parsed = _parser.Parse(raw);
        var attachment = Assert.Single(parsed.Attachments);
        Assert.True(attachment.IsInline);
        Assert.Equal("pic", attachment.ContentId);
        Assert.Equal("hello", System.Text.Encoding.UTF8.GetString(attachment.Content));
    }

    [Fact]
    public void Malformed_message_returns_best_effort_without_throwing()
    {
        var parsed = _parser.Parse("Subject: broken\r\nContent-Type: multipart/mixed; boundary=missing\r\n\r\n--wrong");
        Assert.NotNull(parsed);
    }
}
