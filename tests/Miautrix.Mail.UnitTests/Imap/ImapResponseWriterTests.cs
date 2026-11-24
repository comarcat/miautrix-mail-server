using Miautrix.Mail.Protocols.Imap;
using System.Text;

namespace Miautrix.Mail.UnitTests.Imap;

public sealed class ImapResponseWriterTests
{
    [Fact]
    public void Serializes_unicode_folded_header_literal_using_exact_octet_count()
    {
        var header = Encoding.UTF8.GetBytes("Subject: Olá\r\n\tcontinued\r\n\r\n");
        var prefix = Encoding.ASCII.GetBytes($"* 1 FETCH (BODY[HEADER] {{{header.Length}}}\r\n");
        var result = new ImapCommandResult("A1", "OK", "FETCH completed", new[]
        {
            ImapResponseSegment.Data(prefix), ImapResponseSegment.Data(header), ImapResponseSegment.Data(Encoding.ASCII.GetBytes(")\r\n"))
        });
        var bytes = ImapResponseWriter.Serialize(result);
        Assert.True(Contains(bytes, Encoding.ASCII.GetBytes($"BODY[HEADER] {{{header.Length}}}\r\n")));
        Assert.True(Contains(bytes, header));
        var trailer = Encoding.ASCII.GetBytes("A1 OK FETCH completed\r\n");
        Assert.Equal(trailer, bytes[^trailer.Length..]);
    }

    private static bool Contains(byte[] source, byte[] value)
    {
        for (var i = 0; i <= source.Length - value.Length; i++)
            if (source.AsSpan(i, value.Length).SequenceEqual(value)) return true;
        return false;
    }
}
