using Miautrix.Mail.Protocols.Imap;

namespace Miautrix.Mail.UnitTests.Imap;

public sealed class ImapSearchAndFetchParserTests
{
    [Fact]
    public void Parses_search_charset_or_not_and_groups()
    {
        var command = ImapCommandTokenizer.Parse("A1 SEARCH CHARSET UTF-8 (OR FROM \"alice example\" SUBJECT invoice) NOT (SEEN DELETED)");
        var query = ImapSearchParser.Parse(command.Arguments);
        Assert.Equal("UTF-8", query.Charset);
        Assert.IsType<ImapSearchAnd>(query.Root);
        Assert.IsType<ImapSearchKey>(ImapSearchParser.Parse(ImapCommandTokenizer.Parse("A1 SEARCH 1:*").Arguments).Root);
        Assert.Throws<FormatException>(() => ImapSearchParser.Parse(ImapCommandTokenizer.Parse("A2 SEARCH ALL BOGUS").Arguments));
        Assert.Throws<ImapUnsupportedCharsetException>(() => ImapSearchParser.Parse(ImapCommandTokenizer.Parse("A3 SEARCH CHARSET KOI8-R ALL").Arguments));
    }

    [Fact]
    public void Parses_fetch_macros_sections_partials_and_peek()
    {
        var items = ImapFetchItemParser.Parse("UID FLAGS ENVELOPE BODYSTRUCTURE BODY.PEEK[HEADER.FIELDS (SUBJECT FROM)]<10.20> RFC822.SIZE");
        Assert.Equal(new[] { "UID", "FLAGS", "ENVELOPE", "BODYSTRUCTURE", "BODY.PEEK", "RFC822.SIZE" }, items.Select(i => i.Name));
        Assert.Equal("HEADER.FIELDS (SUBJECT FROM)", items[4].Section);
        Assert.True(items[4].Peek);
        Assert.Equal((uint)10, items[4].PartialStart);
        Assert.Equal((uint)20, items[4].PartialCount);
        Assert.Throws<FormatException>(() => ImapFetchItemParser.Parse("BOGUS"));
        Assert.Equal(new[] { "FLAGS", "INTERNALDATE", "RFC822.SIZE", "ENVELOPE" }, ImapFetchItemParser.Parse("ALL").Select(i => i.Name));
        Assert.Throws<FormatException>(() => ImapFetchItemParser.Parse("BODY.PEEK"));
    }

    [Fact]
    public void Enforces_state_transitions_for_command_families()
    {
        Assert.True(ImapStateMachine.IsAllowed(ImapState.NotAuthenticated, "LOGIN"));
        Assert.False(ImapStateMachine.IsAllowed(ImapState.NotAuthenticated, "FETCH"));
        Assert.True(ImapStateMachine.IsAllowed(ImapState.Authenticated, "SELECT"));
        Assert.False(ImapStateMachine.IsAllowed(ImapState.Authenticated, "STORE"));
        Assert.True(ImapStateMachine.IsAllowed(ImapState.Selected, "FETCH"));
        Assert.False(ImapStateMachine.IsAllowed(ImapState.LoggedOut, "NOOP"));
    }
}
