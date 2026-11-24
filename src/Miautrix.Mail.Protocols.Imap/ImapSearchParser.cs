namespace Miautrix.Mail.Protocols.Imap;

public abstract record ImapSearchNode;
public sealed record ImapSearchAnd(IReadOnlyList<ImapSearchNode> Children) : ImapSearchNode;
public sealed record ImapSearchNot(ImapSearchNode Child) : ImapSearchNode;
public sealed record ImapSearchOr(ImapSearchNode Left, ImapSearchNode Right) : ImapSearchNode;
public sealed record ImapSearchKey(string Name, IReadOnlyList<string> Values) : ImapSearchNode;
public sealed record ImapSearchQuery(string Charset, ImapSearchNode Root);
public sealed class ImapUnsupportedCharsetException(string charset) : Exception(charset);

public static class ImapSearchParser
{
    private static readonly HashSet<string> OneArgumentKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "FROM", "TO", "CC", "BCC", "SUBJECT", "BODY", "TEXT", "SINCE", "BEFORE", "ON",
        "SENTSINCE", "SENTBEFORE", "SENTON", "LARGER", "SMALLER", "UID", "HEADER"
    };
    private static readonly HashSet<string> ZeroArgumentKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALL", "SEEN", "UNSEEN", "ANSWERED", "UNANSWERED", "FLAGGED", "UNFLAGGED", "DELETED",
        "UNDELETED", "DRAFT", "UNDRAFT", "NEW", "OLD", "RECENT"
    };

    public static ImapSearchQuery Parse(IReadOnlyList<ImapToken> tokens)
    {
        var charset = "US-ASCII";
        var offset = 0;
        if (tokens.Count > 0 && tokens[0].Text.Equals("CHARSET", StringComparison.OrdinalIgnoreCase))
        {
            if (tokens.Count < 2) throw new FormatException("CHARSET requires a value.");
            charset = tokens[1].Text.ToUpperInvariant();
            offset = 2;
            if (charset is not "UTF-8" and not "US-ASCII") throw new ImapUnsupportedCharsetException(charset);
        }
        var parser = new Reader(tokens, offset);
        var root = parser.ReadAndGroup(stopAtRightParen: false);
        if (!parser.AtEnd) throw new FormatException("Unexpected SEARCH token.");
        return new ImapSearchQuery(charset, root);
    }

    private sealed class Reader(IReadOnlyList<ImapToken> tokens, int offset)
    {
        private int _position = offset;
        public bool AtEnd => _position >= tokens.Count;

        public ImapSearchNode ReadAndGroup(bool stopAtRightParen)
        {
            var children = new List<ImapSearchNode>();
            while (_position < tokens.Count && (!stopAtRightParen || tokens[_position].Kind != ImapTokenKind.RightParenthesis))
            {
                var token = tokens[_position++];
                if (token.Kind == ImapTokenKind.LeftParenthesis)
                {
                    var child = ReadAndGroup(true);
                    if (_position >= tokens.Count || tokens[_position++].Kind != ImapTokenKind.RightParenthesis) throw new FormatException("Unclosed SEARCH group.");
                    children.Add(child);
                    continue;
                }
                if (token.Kind is ImapTokenKind.RightParenthesis or ImapTokenKind.Literal) throw new FormatException("Invalid SEARCH token.");
                var name = token.Text.ToUpperInvariant();
                if (name == "NOT") { children.Add(new ImapSearchNot(ReadCriterion())); continue; }
                if (name == "OR") { children.Add(new ImapSearchOr(ReadCriterion(), ReadCriterion())); continue; }
                children.Add(ReadKey(name));
            }
            if (children.Count == 0) throw new FormatException("Empty SEARCH group.");
            return children.Count == 1 ? children[0] : new ImapSearchAnd(children);
        }

        private ImapSearchNode ReadCriterion()
        {
            if (_position >= tokens.Count) throw new FormatException("Missing SEARCH criterion.");
            if (tokens[_position].Kind == ImapTokenKind.LeftParenthesis)
            {
                _position++;
                var group = ReadAndGroup(true);
                if (_position >= tokens.Count || tokens[_position++].Kind != ImapTokenKind.RightParenthesis) throw new FormatException("Unclosed SEARCH group.");
                return group;
            }
            if (tokens[_position].Kind != ImapTokenKind.Atom) throw new FormatException("Invalid SEARCH criterion.");
            var name = tokens[_position++].Text.ToUpperInvariant();
            if (name == "NOT") return new ImapSearchNot(ReadCriterion());
            if (name == "OR") return new ImapSearchOr(ReadCriterion(), ReadCriterion());
            return ReadKey(name);
        }

        private ImapSearchNode ReadKey(string name)
        {
            if (ZeroArgumentKeys.Contains(name)) return new ImapSearchKey(name, Array.Empty<string>());
            if (SequenceSet.TryParse(name, out _)) return new ImapSearchKey(name, Array.Empty<string>());
            if (!OneArgumentKeys.Contains(name)) throw new FormatException($"Unknown SEARCH key: {name}");
            var count = name == "HEADER" ? 2 : 1;
            if (_position + count > tokens.Count) throw new FormatException($"SEARCH key {name} requires {count} argument(s).");
            var values = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var value = tokens[_position++];
                if (value.Kind is ImapTokenKind.LeftParenthesis or ImapTokenKind.RightParenthesis) throw new FormatException($"Invalid SEARCH argument for {name}.");
                values.Add(value.Text);
            }
            return new ImapSearchKey(name, values);
        }
    }
}
