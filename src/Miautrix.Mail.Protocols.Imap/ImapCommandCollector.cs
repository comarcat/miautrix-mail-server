using System.Text;

namespace Miautrix.Mail.Protocols.Imap;

public sealed record CollectedImapCommand(string Text, IReadOnlyList<byte[]> Literals);
public class ImapCommandCollectionException(string tag, string message) : Exception(message)
{
    public string Tag { get; } = tag;
}
public sealed class ImapLiteralTooLargeException(string tag, bool nonSynchronizing, string commandName)
    : ImapCommandCollectionException(tag, "IMAP literal exceeds configured limit.")
{
    public bool NonSynchronizing { get; } = nonSynchronizing;
    public string CommandName { get; } = commandName;
}

public static class ImapCommandCollector
{
    public static async Task<CollectedImapCommand> ReadAsync(
        ImapConnectionReader reader,
        Func<bool, CancellationToken, Task> sendContinuation,
        int maxLiteralBytes,
        CancellationToken cancellationToken = default)
    {
        var text = new StringBuilder();
        var literals = new List<byte[]>();
        var line = await ReadLineAsync(reader, text, cancellationToken) ?? throw new EndOfStreamException();
        while (true)
        {
            var marker = FindLiteralMarker(line);
            if (marker is null)
            {
                text.Append(Encoding.UTF8.GetString(line));
                break;
            }

            text.Append(Encoding.UTF8.GetString(line.AsSpan(0, marker.Value.Start)));
            var literalIndex = literals.Count;
            text.Append('\u0001').Append(literalIndex).Append('\u0002');
            if (marker.Value.Length > maxLiteralBytes)
                throw new ImapLiteralTooLargeException(ExtractTag(text.ToString()), marker.Value.NonSynchronizing, ExtractCommandName(text.ToString()));
            if (!marker.Value.NonSynchronizing) await sendContinuation(true, cancellationToken);
            literals.Add(await reader.ReadLiteralAsync(marker.Value.Length, cancellationToken));
            line = await ReadLineAsync(reader, text, cancellationToken) ?? throw new EndOfStreamException("Command ended before literal continuation.");
            // Literal data is followed by command continuation text. Preserve that text and scan
            // it for another literal marker before dispatch.
        }
        return new CollectedImapCommand(text.ToString(), literals);
    }

    private static async Task<byte[]?> ReadLineAsync(ImapConnectionReader reader, StringBuilder commandText, CancellationToken ct)
    {
        try { return await reader.ReadLineAsync(ct); }
        catch (ImapLineTooLongException ex)
        {
            var partial = Encoding.UTF8.GetString(ex.PartialLine);
            var tag = ExtractTag(commandText.Length == 0 ? partial : commandText.ToString());
            throw new ImapCommandCollectionException(tag, "IMAP command line exceeded maximum length.");
        }
    }

    private static string ExtractTag(string commandText)
    {
        var tag = commandText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return tag is not null && tag.All(ch => !char.IsControl(ch)) ? tag : "A0";
    }

    private static string ExtractCommandName(string commandText)
    {
        var parts = commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1 && parts[1].All(ch => char.IsAsciiLetterOrDigit(ch)) ? parts[1].ToUpperInvariant() : "UNKNOWN";
    }

    private static (int Start, int Length, bool NonSynchronizing)? FindLiteralMarker(ReadOnlySpan<byte> line)
    {
        var quoted = false;
        var escaped = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = (char)line[i];
            if (quoted)
            {
                if (escaped) escaped = false;
                else if (ch == '\\') escaped = true;
                else if (ch == '"') quoted = false;
                continue;
            }
            if (ch == '"') { quoted = true; continue; }
            if (ch != '{') continue;
            var end = i + 1;
            while (end < line.Length && line[end] is >= (byte)'0' and <= (byte)'9') end++;
            var plus = end < line.Length && line[end] == (byte)'+';
            if (end == i + 1) continue;
            if (plus) end++;
            if (end >= line.Length || line[end] != (byte)'}') continue;
            if (end != line.Length - 1) continue;
            if (!int.TryParse(Encoding.ASCII.GetString(line[(i + 1)..(plus ? end - 1 : end)]), out var length)) throw new InvalidDataException("Invalid IMAP literal length.");
            return (i, length, plus);
        }
        return null;
    }
}
