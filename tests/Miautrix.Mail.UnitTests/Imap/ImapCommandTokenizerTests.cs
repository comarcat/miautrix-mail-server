using Miautrix.Mail.Protocols.Imap;
using System.Text;

namespace Miautrix.Mail.UnitTests.Imap;

public sealed class ImapCommandTokenizerTests
{
    [Fact]
    public void Parses_quoted_escapes_atoms_lists_and_sections()
    {
        var command = ImapCommandTokenizer.Parse("A1 LOGIN \"user \\\"name\\\"\" \"p\\\\w\"");
        Assert.Equal("A1", command.Tag);
        Assert.Equal("LOGIN", command.Name);
        Assert.Equal("user \"name\"", command.Arguments[0].Text);
        Assert.Equal("p\\w", command.Arguments[1].Text);

        var fetch = ImapCommandTokenizer.Parse("A2 FETCH 1 (UID BODY.PEEK[HEADER.FIELDS (SUBJECT FROM)])");
        Assert.Contains(fetch.Arguments, token => token.Kind == ImapTokenKind.LeftParenthesis);
        Assert.Contains(fetch.Arguments, token => token.Text == "BODY.PEEK[HEADER.FIELDS (SUBJECT FROM)]");
    }

    [Fact]
    public async Task Collector_accepts_multiple_synchronized_and_non_synchronized_literals()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("A1 LOGIN {4}\r\nuser {6+}\r\nsecret\r\n"));
        var reader = new ImapConnectionReader(stream, bufferSize: 256);
        var continuations = 0;
        var collected = await ImapCommandCollector.ReadAsync(reader,
            (_, _) => { continuations++; return Task.CompletedTask; }, 100);
        var parsed = ImapCommandTokenizer.Parse(collected.Text, collected.Literals);
        Assert.Equal(1, continuations);
        Assert.Equal(2, collected.Literals.Count);
        Assert.Equal("user", parsed.Arguments[0].Text);
        Assert.Equal("secret", parsed.Arguments[1].Text);
    }

    [Fact]
    public async Task Oversize_synchronizing_literal_is_rejected_without_continuation_and_stream_can_continue()
    {
        var reader = new ImapConnectionReader(new MemoryStream(Encoding.ASCII.GetBytes("A1 APPEND INBOX {4}\r\nA2 NOOP\r\n")), bufferSize: 128);
        var continuationCount = 0;
        var exception = await Assert.ThrowsAsync<ImapLiteralTooLargeException>(() => ImapCommandCollector.ReadAsync(reader,
            (_, _) => { continuationCount++; return Task.CompletedTask; }, maxLiteralBytes: 3));

        Assert.Equal("A1", exception.Tag);
        Assert.Equal("APPEND", exception.CommandName);
        Assert.False(exception.NonSynchronizing);
        Assert.Equal(0, continuationCount);
        Assert.Equal("A2 NOOP", Encoding.ASCII.GetString(await reader.ReadLineAsync() ?? []));
    }

    [Fact]
    public async Task Oversize_non_synchronizing_literal_is_marked_connection_fatal()
    {
        var reader = new ImapConnectionReader(new MemoryStream(Encoding.ASCII.GetBytes("A1 APPEND INBOX {4+}\r\ndata\r\n")), bufferSize: 128);
        var exception = await Assert.ThrowsAsync<ImapLiteralTooLargeException>(() => ImapCommandCollector.ReadAsync(reader,
            (_, _) => Task.CompletedTask, maxLiteralBytes: 3));

        Assert.Equal("A1", exception.Tag);
        Assert.True(exception.NonSynchronizing);
    }
}
