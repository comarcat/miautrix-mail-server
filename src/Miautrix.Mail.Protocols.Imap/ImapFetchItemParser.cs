namespace Miautrix.Mail.Protocols.Imap;

public sealed record ImapFetchItem(string Name, string? Section = null, bool Peek = false, uint? PartialStart = null, uint? PartialCount = null);

public static class ImapFetchItemParser
{
    public static IReadOnlyList<ImapFetchItem> Parse(string input)
    {
        var items = new List<ImapFetchItem>();
        var i = 0;
        while (i < input.Length)
        {
            while (i < input.Length && (char.IsWhiteSpace(input[i]) || input[i] is '(' or ')')) i++;
            if (i >= input.Length) break;
            var start = i;
            var squareDepth = 0;
            var parenDepth = 0;
            while (i < input.Length)
            {
                var c = input[i];
                if (c == '[') squareDepth++;
                else if (c == ']') squareDepth--;
                else if (c == '(' && squareDepth > 0) parenDepth++;
                else if (c == ')' && squareDepth > 0) parenDepth--;
                else if (char.IsWhiteSpace(c) && squareDepth == 0) break;
                i++;
            }
            var raw = input[start..i].Trim('(', ')');
            if (raw.Length == 0) continue;
            var partialStart = (uint?)null;
            var partialCount = (uint?)null;
            var partialAt = raw.LastIndexOf('<');
            if (partialAt >= 0 && raw.EndsWith('>'))
            {
                var values = raw[(partialAt + 1)..^1].Split('.');
                if (values.Length != 2 || !uint.TryParse(values[0], out var first) || !uint.TryParse(values[1], out var count))
                    throw new FormatException("Invalid FETCH partial range.");
                partialStart = first;
                partialCount = count;
                raw = raw[..partialAt];
            }
            var peek = raw.StartsWith("BODY.PEEK[", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("BINARY.PEEK[", StringComparison.OrdinalIgnoreCase);
            var sectionStart = raw.IndexOf('[');
            string name;
            string? section = null;
            if (sectionStart >= 0)
            {
                if (!raw.EndsWith(']')) throw new FormatException("Invalid FETCH body section.");
                name = raw.StartsWith("BINARY.SIZE[", StringComparison.OrdinalIgnoreCase) ? "BINARY.SIZE"
                    : raw.StartsWith("BINARY", StringComparison.OrdinalIgnoreCase) ? (peek ? "BINARY.PEEK" : "BINARY")
                    : peek ? "BODY.PEEK" : "BODY";
                section = raw[(sectionStart + 1)..^1].Trim();
            }
            else
            {
                name = raw.ToUpperInvariant();
                if (name == "BODY.PEEK") throw new FormatException("BODY.PEEK requires a section.");
                if (name is not "UID" and not "FLAGS" and not "INTERNALDATE" and not "RFC822.SIZE" and not "ENVELOPE" and not "BODYSTRUCTURE" and not "BODY" and not "RFC822" and not "RFC822.HEADER" and not "RFC822.TEXT" and not "ALL" and not "FAST" and not "FULL")
                    throw new FormatException($"Unknown FETCH data item: {name}");
            }
            items.Add(new ImapFetchItem(name, section, peek, partialStart, partialCount));
        }
        if (items.Count == 1)
        {
            var macro = items[0].Name;
            var expansion = macro switch
            {
                "ALL" => new[] { "FLAGS", "INTERNALDATE", "RFC822.SIZE", "ENVELOPE" },
                "FAST" => new[] { "FLAGS", "INTERNALDATE", "RFC822.SIZE" },
                "FULL" => new[] { "FLAGS", "INTERNALDATE", "RFC822.SIZE", "ENVELOPE", "BODY" },
                _ => null
            };
            if (expansion is not null) return expansion.Select(name => new ImapFetchItem(name)).ToArray();
        }
        return items;
    }
}
