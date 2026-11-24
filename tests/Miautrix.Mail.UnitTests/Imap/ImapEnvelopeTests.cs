using System.Text;
using Miautrix.Mail.Infrastructure.Mail;

namespace Miautrix.Mail.UnitTests.Imap;

/// <summary>IMAP-007: FETCH ENVELOPE must never fail and must follow RFC 3501 §7.4.2.</summary>
public sealed class ImapEnvelopeTests
{
    private const char Nul = (char)0;
    private readonly MimeKitImapMimeFormatter _formatter = new();

    public static TheoryData<string> MalformedMessages() => new()
    {
        "",
        "\r\n\r\nbody without headers",
        " leading continuation: x\r\nFrom: a@example.test\r\n\r\nbody",
        "not a header line\r\nFrom: a@example.test\r\nSubject: after junk\r\n\r\nbody",
        "From sender@example.test Mon Nov 24 10:00:00 2026\r\nFrom: a@example.test\r\n\r\nbody",
        "From: a@example.test\nSubject: bare LF\n\nbody",
        "From: a@example.test\r\nSubject: nul" + Nul + "byte\r\n\r\nbody",
        "From: a@example.test\r\nSubject: no body",
        "From: undisclosed-recipients:;\r\nTo: Group: a@example.test, b@example.test;\r\n\r\nx",
        "From: <>\r\nTo: a@\r\nCc: @\r\nBcc: \"\" <>\r\n\r\nx",
        "From: \"Bad\" <a@example.test\r\nReply-To: <@route.example:a@example.test>\r\n\r\nx",
        "Date: not a date\r\nSubject: =?UTF-8?B?invalid?=\r\nMessage-ID: no-brackets\r\n\r\nx",
        "Subject: line\r\n folded\r\n\tagain\r\nFrom: =?UTF-8?B?8J+YgCBFbW9qaQ==?= <a@example.test>\r\n\r\nx",
        "Subject: été raw 8-bit\r\nFrom: José <josé@bücher.example>\r\n\r\nx",
        "From: a@example.test\r\nContent-Type: multipart/mixed; boundary=\"x\"\r\n\r\n--x\r\nbroken",
    };

    [Theory]
    [MemberData(nameof(MalformedMessages))]
    public void Envelope_is_valid_rfc3501_for_malformed_and_unusual_messages(string raw)
    {
        var envelope = _formatter.BuildEnvelope(Encoding.UTF8.GetBytes(raw));
        EnvelopeValidator.AssertValid(envelope);
    }

    [Fact]
    public void Message_without_headers_returns_all_nil_envelope_instead_of_failing()
    {
        Assert.Equal("(NIL NIL NIL NIL NIL NIL NIL NIL NIL NIL)", _formatter.BuildEnvelope(Encoding.ASCII.GetBytes("\r\n\r\nbody")));
    }

    [Fact]
    public void Junk_first_line_is_skipped_and_headers_are_still_read()
    {
        var envelope = _formatter.BuildEnvelope(Encoding.ASCII.GetBytes("not a header line\r\nSubject: after junk\r\n\r\nbody"));
        Assert.StartsWith("(NIL \"after junk\" ", envelope);
    }

    [Fact]
    public void Sender_and_reply_to_default_to_from_and_raw_fields_are_preserved()
    {
        var raw = "Date: Mon, 24 Nov 2026 10:00:00 -0500\r\nSubject: =?UTF-8?Q?Caf=C3=A9?=\r\nFrom: Alice <alice@example.test>\r\n"
            + "To: bob@example.test\r\nIn-Reply-To: <parent@example.test>\r\nMessage-ID: <id-1@example.test>\r\n\r\nbody";
        var envelope = _formatter.BuildEnvelope(Encoding.ASCII.GetBytes(raw));
        const string alice = "((\"Alice\" NIL \"alice\" \"example.test\"))";
        Assert.Equal(
            $"(\"Mon, 24 Nov 2026 10:00:00 -0500\" \"=?UTF-8?Q?Caf=C3=A9?=\" {alice} {alice} {alice} ((NIL NIL \"bob\" \"example.test\")) NIL NIL \"<parent@example.test>\" \"<id-1@example.test>\")",
            envelope);
    }

    [Fact]
    public void Group_addresses_use_rfc3501_group_syntax()
    {
        var envelope = _formatter.BuildEnvelope(Encoding.ASCII.GetBytes("To: Team: a@example.test;\r\n\r\nx"));
        Assert.Contains("((NIL NIL \"Team\" NIL) (NIL NIL \"a\" \"example.test\") (NIL NIL NIL NIL))", envelope);
    }

    [Fact]
    public void Non_ascii_never_appears_inside_a_quoted_string()
    {
        var raw = "Subject: été\r\nFrom: José <a@bücher.example>\r\n\r\nx";
        var envelope = _formatter.BuildEnvelope(Encoding.UTF8.GetBytes(raw));
        EnvelopeValidator.AssertValid(envelope);
        Assert.Contains("\"xn--bcher-kva.example\"", envelope);
        Assert.Contains("=?utf-8?", envelope, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Minimal RFC 3501 parser for ENVELOPE: 10 fields; address lists of 4-field addresses.</summary>
    private static class EnvelopeValidator
    {
        public static void AssertValid(string envelope)
        {
            var bytes = Encoding.UTF8.GetBytes(envelope);
            var position = 0;
            var root = ParseList(bytes, ref position);
            Assert.Equal(bytes.Length, position);
            Assert.Equal(10, root.Count);
            for (var i = 0; i < 10; i++)
            {
                if (i is >= 2 and <= 7)
                {
                    if (root[i] is List<object?> addresses)
                        foreach (var address in addresses)
                        {
                            var fields = Assert.IsType<List<object?>>(address);
                            Assert.Equal(4, fields.Count);
                            Assert.All(fields, f => Assert.True(f is null or string));
                        }
                    else Assert.Null(root[i]);
                }
                else Assert.True(root[i] is null or string, $"field {i} must be an nstring");
            }
        }

        private static List<object?> ParseList(byte[] b, ref int p)
        {
            Assert.Equal((byte)'(', b[p++]);
            var items = new List<object?>();
            while (true)
            {
                if (b[p] == (byte)')') { p++; return items; }
                if (items.Count > 0) { Assert.Equal((byte)' ', b[p]); p++; }
                items.Add(ParseItem(b, ref p));
            }
        }

        private static object? ParseItem(byte[] b, ref int p)
        {
            if (b[p] == (byte)'(') return ParseList(b, ref p);
            if (b[p] == (byte)'"')
            {
                p++;
                var sb = new StringBuilder();
                while (b[p] != (byte)'"')
                {
                    if (b[p] == (byte)'\\') p++;
                    Assert.True(b[p] is > 0 and < 128 and not 13 and not 10, "quoted strings must be 7-bit without CR/LF/NUL");
                    sb.Append((char)b[p++]);
                }
                p++;
                return sb.ToString();
            }
            if (b[p] == (byte)'{')
            {
                var close = Array.IndexOf(b, (byte)'}', p);
                var length = int.Parse(Encoding.ASCII.GetString(b, p + 1, close - p - 1));
                Assert.Equal((byte)'\r', b[close + 1]);
                Assert.Equal((byte)'\n', b[close + 2]);
                p = close + 3 + length;
                return Encoding.UTF8.GetString(b, close + 3, length);
            }
            Assert.Equal("NIL", Encoding.ASCII.GetString(b, p, 3));
            p += 3;
            return null;
        }
    }
}
