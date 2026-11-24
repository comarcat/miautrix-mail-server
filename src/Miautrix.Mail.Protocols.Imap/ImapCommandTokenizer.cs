using System.Text;

namespace Miautrix.Mail.Protocols.Imap;

public enum ImapTokenKind { Atom, QuotedString, Literal, LeftParenthesis, RightParenthesis }
public sealed record ImapToken(ImapTokenKind Kind, string Text, byte[]? LiteralBytes = null);
public sealed record ImapCommand(string Tag, string Name, IReadOnlyList<ImapToken> Arguments);

public static class ImapCommandTokenizer
{
    public static ImapCommand Parse(string commandText, IReadOnlyList<byte[]>? literals = null)
    {
        var tokens = Tokenize(commandText, literals);
        if (tokens.Count < 2 || tokens[0].Kind != ImapTokenKind.Atom || tokens[1].Kind != ImapTokenKind.Atom)
            throw new FormatException("IMAP command requires a tag and command name.");
        return new ImapCommand(tokens[0].Text, tokens[1].Text.ToUpperInvariant(), tokens.Skip(2).ToArray());
    }

    public static IReadOnlyList<ImapToken> Tokenize(string text, IReadOnlyList<byte[]>? literals = null)
    {
        literals ??= Array.Empty<byte[]>();
        var result = new List<ImapToken>();
        for (var i = 0; i < text.Length;)
        {
            if (char.IsWhiteSpace(text[i])) { i++; continue; }
            if (text[i] == '(') { result.Add(new(ImapTokenKind.LeftParenthesis, "(")); i++; continue; }
            if (text[i] == ')') { result.Add(new(ImapTokenKind.RightParenthesis, ")")); i++; continue; }
            if (text[i] == '\u0001')
            {
                var end = text.IndexOf('\u0002', i + 1);
                if (end < 0 || !int.TryParse(text.AsSpan(i + 1, end - i - 1), out var index) || index < 0 || index >= literals.Count)
                    throw new FormatException("Invalid IMAP literal placeholder.");
                result.Add(new(ImapTokenKind.Literal, Encoding.UTF8.GetString(literals[index]), literals[index]));
                i = end + 1;
                continue;
            }
            if (text[i] == '"')
            {
                i++;
                var value = new StringBuilder();
                var closed = false;
                while (i < text.Length)
                {
                    var ch = text[i++];
                    if (ch == '"') { closed = true; break; }
                    if (ch == '\\')
                    {
                        if (i >= text.Length || (text[i] != '\\' && text[i] != '"')) throw new FormatException("Invalid quoted-string escape.");
                        ch = text[i++];
                    }
                    value.Append(ch);
                }
                if (!closed) throw new FormatException("Unterminated quoted string.");
                result.Add(new(ImapTokenKind.QuotedString, value.ToString()));
                continue;
            }
            var start = i;
            var squareDepth = 0;
            while (i < text.Length)
            {
                var current = text[i];
                if (current == '[') squareDepth++;
                else if (current == ']') squareDepth--;
                else if (squareDepth == 0 && (char.IsWhiteSpace(current) || current is '(' or ')')) break;
                i++;
            }
            result.Add(new(ImapTokenKind.Atom, text[start..i]));
        }
        return result;
    }

    public static string JoinValues(IEnumerable<ImapToken> tokens) => string.Join(' ', tokens.Select(t => t.Kind switch
    {
        ImapTokenKind.LeftParenthesis => "(",
        ImapTokenKind.RightParenthesis => ")",
        _ => t.Text
    }));
}
