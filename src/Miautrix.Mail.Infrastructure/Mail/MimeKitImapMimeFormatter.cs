using System.Text;
using Miautrix.Mail.Application.Mail.Mime;
using MimeKit;

namespace Miautrix.Mail.Infrastructure.Mail;

public sealed class MimeKitImapMimeFormatter : IImapMimeFormatter
{
    private const string EmptyEnvelope = "(NIL NIL NIL NIL NIL NIL NIL NIL NIL NIL)";

    /// <summary>
    /// RFC 3501 §7.4.2 ENVELOPE. Never throws: a message whose headers cannot be parsed yields an all-NIL
    /// envelope instead of failing the whole FETCH (IMAP-007).
    /// </summary>
    public string BuildEnvelope(byte[] rawMessage)
    {
        try { return Envelope(Load(rawMessage)); }
        catch (Exception) { return EmptyEnvelope; }
    }

    private static string Envelope(MimeMessage message)
    {
        // Date, Subject, In-Reply-To and Message-ID are the raw (unfolded, still RFC 2047 encoded) header values.
        var from = AddressList(message.From);
        var sender = message.Headers.Contains(HeaderId.Sender) && message.Sender is not null ? AddressList(message.Sender) : from;
        var replyTo = message.ReplyTo.Count > 0 ? AddressList(message.ReplyTo) : from;
        return $"({NString(RawHeader(message, HeaderId.Date))} {NString(RawHeader(message, HeaderId.Subject))} {from} {sender} {replyTo} {AddressList(message.To)} {AddressList(message.Cc)} {AddressList(message.Bcc)} {NString(RawHeader(message, HeaderId.InReplyTo))} {NString(RawHeader(message, HeaderId.MessageId))})";
    }

    private static string? RawHeader(MimeMessage message, HeaderId id)
    {
        var header = message.Headers.FirstOrDefault(h => h.Id == id);
        if (header is null) return null;
        var value = Encoding.UTF8.GetString(header.RawValue).Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        return value.Length == 0 ? null : value;
    }

    public string BuildBodyStructure(byte[] rawMessage, bool extended = true)
    {
        try { return Structure(Load(rawMessage).Body, extended); }
        catch { return "NIL"; }
    }

    public byte[] GetBodySection(byte[] rawMessage, string section)
    {
        try
        {
            var message = Load(rawMessage);
            var current = message.Body;
            var normalized = section.Trim();
            if (normalized.EndsWith(".MIME", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^5];
                current = GetPart(current, normalized);
                return current is null ? Array.Empty<byte>() : SerializeHeaders(current.Headers);
            }
            current = GetPart(current, normalized);
            if (current is null) return Array.Empty<byte>();
            if (current is MessagePart messagePart && messagePart.Message is not null)
            {
                using var nested = new MemoryStream();
                messagePart.Message.WriteTo(nested);
                return nested.ToArray();
            }
            if (current is MimePart part && part.Content is not null)
            {
                using var output = new MemoryStream();
                part.Content.DecodeTo(output);
                return output.ToArray();
            }
            using var stream = new MemoryStream();
            current.WriteTo(stream);
            return stream.ToArray();
        }
        catch { return Array.Empty<byte>(); }
    }

    private static MimeMessage Load(byte[] raw)
    {
        try
        {
            using var stream = new MemoryStream(raw, writable: false);
            return MimeMessage.Load(stream, persistent: false);
        }
        catch (FormatException)
        {
            // Tolerate stored messages that do not start with a header line (blank first line, leading
            // continuation or junk line): skip lines until the first "name:" header, else treat as headerless.
            var text = Encoding.Latin1.GetString(raw);
            var offset = 0;
            while (offset < text.Length)
            {
                var end = text.IndexOf('\n', offset);
                var line = (end < 0 ? text[offset..] : text[offset..end]).TrimEnd('\r');
                if (line.Length == 0) break;
                if (System.Text.RegularExpressions.Regex.IsMatch(line, "^[!-9;-~]+:"))
                {
                    using var retry = new MemoryStream(raw, offset, raw.Length - offset, writable: false);
                    return MimeMessage.Load(retry, persistent: false);
                }
                if (end < 0) break;
                offset = end + 1;
            }
            return new MimeMessage(new ParserOptions(), Array.Empty<Header>(), new TextPart("plain"));
        }
    }

    private static MimeEntity? GetPart(MimeEntity? root, string section)
    {
        if (root is null) return null;
        if (string.IsNullOrEmpty(section)) return root;
        var path = section.Split('.');
        var current = root;
        for (var position = 0; position < path.Length; position++)
        {
            if (!int.TryParse(path[position], out var index) || index <= 0) return null;
            if (current is Multipart multi)
            {
                if (index > multi.Count) return null;
                current = multi[index - 1];
                continue;
            }
            if (current is MessagePart nested && position > 0 && nested.Message?.Body is MimeEntity nestedBody)
            {
                if (nestedBody is Multipart nestedMultipart)
                {
                    if (index > nestedMultipart.Count) return null;
                    current = nestedMultipart[index - 1];
                }
                else if (index == 1) current = nestedBody;
                else return null;
                continue;
            }
            if (current is MessagePart && index == 1 && position == 0 && path.Length > 1) continue;
            if (index == 1 && path.Length == 1) continue;
            return null;
        }
        return current;
    }

    private static string Structure(MimeEntity? entity, bool extended)
    {
        if (entity is null) return "NIL";
        if (entity is Multipart multipart)
        {
            var multiStructure = "(" + string.Join(" ", multipart.Select(part => Structure(part, extended))) + " " + Quote(multipart.ContentType.MediaSubtype.ToUpperInvariant());
            if (extended) multiStructure += $" {Parameters(multipart.ContentType.Parameters)} {Disposition(multipart.ContentDisposition)} NIL NIL";
            return multiStructure + ")";
        }
        if (entity is MessagePart messagePart)
        {
            using var nestedStream = new MemoryStream();
            messagePart.Message?.WriteTo(nestedStream);
            var nestedMessage = messagePart.Message;
            var nestedBody = nestedMessage is null ? "NIL" : Structure(nestedMessage.Body, extended);
            var nestedEnvelope = nestedMessage is null ? "NIL" : Envelope(nestedMessage);
            var encodedSize = nestedStream.Length;
            var description = messagePart.Headers["Content-Description"];
            var transferEncoding = messagePart.Headers["Content-Transfer-Encoding"] ?? "7bit";
            var messageStructure = $"(\"MESSAGE\" \"RFC822\" {Parameters(messagePart.ContentType.Parameters)} {NString(messagePart.ContentId)} {NString(description)} {Quote(transferEncoding.ToUpperInvariant())} {encodedSize} {nestedEnvelope} {nestedBody} {LineCount(nestedStream.ToArray())}";
            if (extended) messageStructure += $" NIL {Disposition(messagePart.ContentDisposition)} NIL NIL";
            return messageStructure + ")";
        }
        if (entity is not MimePart part) return "NIL";
        var type = Quote(part.ContentType.MediaType.ToUpperInvariant());
        var subtype = Quote(part.ContentType.MediaSubtype.ToUpperInvariant());
        var size = part.Content?.Stream?.Length ?? 0;
        var encoding = Quote(part.ContentTransferEncoding.ToString().ToUpperInvariant());
        var structure = $"({type} {subtype} {Parameters(part.ContentType.Parameters)} {NString(part.ContentId)} {NString(part.ContentDescription)} {encoding} {size}";
        if (part.ContentType.MediaType.Equals("text", StringComparison.OrdinalIgnoreCase))
        {
            var lines = 0;
            if (part.Content is not null) { using var stream = new MemoryStream(); try { part.Content.DecodeTo(stream); lines = LineCount(stream.ToArray()); } catch { } }
            structure += $" {lines}";
        }
        if (extended) structure += $" NIL {Disposition(part.ContentDisposition)} NIL NIL";
        return structure + ")";
    }

    private static string AddressList(InternetAddressList list)
    {
        var addresses = new List<string>();
        foreach (var address in list)
        {
            if (address is MailboxAddress mailbox) addresses.Add(FormatMailbox(mailbox));
            else if (address is GroupAddress group)
            {
                // RFC 3501 group syntax: start (NIL NIL "group-name" NIL), members, end (NIL NIL NIL NIL).
                addresses.Add($"(NIL NIL {NString(EncodePhrase(group.Name) ?? string.Empty)} NIL)");
                addresses.AddRange(group.Members.OfType<MailboxAddress>().Select(FormatMailbox));
                addresses.Add("(NIL NIL NIL NIL)");
            }
        }
        var values = string.Join(" ", addresses);
        return values.Length == 0 ? "NIL" : $"({values})";
    }

    private static string FormatMailbox(MailboxAddress mailbox)
    {
        var local = mailbox.LocalPart ?? string.Empty;
        var host = mailbox.Domain ?? string.Empty;
        if (host.Any(c => c > 127))
        {
            try { host = new System.Globalization.IdnMapping().GetAscii(host); } catch (ArgumentException) { }
        }
        // An empty host would read as group syntax; send "" rather than NIL for a mailbox.
        var hostValue = host.Length == 0 ? "\"\"" : NString(host);
        return $"({NString(EncodePhrase(mailbox.Name))} NIL {(local.Length == 0 ? "\"\"" : NString(local))} {hostValue})";
    }

    /// <summary>Display names go back to RFC 2047 form when they contain non-ASCII (ENVELOPE carries raw header text).</summary>
    private static string? EncodePhrase(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return name.All(c => c < 128) ? name : Encoding.ASCII.GetString(MimeKit.Utils.Rfc2047.EncodePhrase(Encoding.UTF8, name));
    }

    private static string Parameters(ParameterList parameters)
    {
        var values = parameters.SelectMany(parameter => new[] { Quote(parameter.Name), Quote(parameter.Value) }).ToArray();
        return values.Length == 0 ? "NIL" : $"({string.Join(' ', values)})";
    }

    private static string Disposition(ContentDisposition? disposition)
    {
        if (disposition is null) return "NIL";
        var parameters = Parameters(disposition.Parameters);
        return $"({Quote(disposition.Disposition.ToUpperInvariant())} {parameters})";
    }

    private static string AddressList(InternetAddress? address)
    {
        if (address is null) return "NIL";
        var list = new InternetAddressList { address };
        return AddressList(list);
    }

    private static string NString(string? value) => string.IsNullOrEmpty(value) ? "NIL" : Quote(value);

    /// <summary>
    /// IMAP4rev1 quoted strings are 7-bit without CR, LF or NUL. Anything else is sent as a literal, whose
    /// length is the UTF-8 byte count (the response is written as UTF-8).
    /// </summary>
    private static string Quote(string value)
    {
        value = value.Replace("\r", string.Empty).Replace("\n", string.Empty);
        if (value.All(c => c is > (char)0 and < (char)128))
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        value = value.Replace("\0", string.Empty);
        return "{" + Encoding.UTF8.GetByteCount(value) + "}\r\n" + value;
    }
    private static byte[] SerializeHeaders(HeaderList headers) => Encoding.UTF8.GetBytes(string.Join("\r\n", headers.Select(h => $"{h.Field}: {h.Value}")) + "\r\n\r\n");
    private static int LineCount(byte[] bytes) => Encoding.UTF8.GetString(bytes).Count(c => c == '\n');
}
