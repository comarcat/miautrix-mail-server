using Miautrix.Mail.Protocols.Imap;
using System.Text;

namespace Miautrix.Mail.UnitTests.Imap;

public sealed class ImapConnectionReaderTests
{
    [Fact]
    public async Task Reads_line_then_literal_from_same_buffer()
    {
        var bytes = Encoding.UTF8.GetBytes("A1 APPEND INBOX {5}\r\nhello\r\nA2 NOOP\r\n");
        var reader = new ImapConnectionReader(new MemoryStream(bytes), bufferSize: 256);
        Assert.Equal("A1 APPEND INBOX {5}", Encoding.UTF8.GetString(await reader.ReadLineAsync() ?? throw new EndOfStreamException()));
        Assert.Equal("hello", Encoding.UTF8.GetString(await reader.ReadLiteralAsync(5)));
        Assert.Equal(string.Empty, Encoding.UTF8.GetString(await reader.ReadLineAsync() ?? throw new EndOfStreamException()));
        Assert.Equal("A2 NOOP", Encoding.UTF8.GetString(await reader.ReadLineAsync() ?? throw new EndOfStreamException()));
    }

    [Fact]
    public async Task Rejects_lines_and_literals_over_limits()
    {
        var lineReader = new ImapConnectionReader(new MemoryStream(Encoding.ASCII.GetBytes("12345\r\n")), maxLineBytes: 4, bufferSize: 16);
        await Assert.ThrowsAsync<ImapLineTooLongException>(() => lineReader.ReadLineAsync());
        var literalReader = new ImapConnectionReader(new MemoryStream(new byte[10]), maxLiteralBytes: 4, bufferSize: 16);
        await Assert.ThrowsAsync<InvalidDataException>(() => literalReader.ReadLiteralAsync(5));
    }

    [Fact]
    public async Task Line_limit_violation_retains_the_command_tag()
    {
        var reader = new ImapConnectionReader(new MemoryStream(Encoding.ASCII.GetBytes("TAG1 NOOP very-long-argument\r\n")), maxLineBytes: 8, bufferSize: 64);
        var exception = await Assert.ThrowsAsync<ImapCommandCollectionException>(() =>
            ImapCommandCollector.ReadAsync(reader, (_, _) => Task.CompletedTask, 64));
        Assert.Equal("TAG1", exception.Tag);
    }
}
