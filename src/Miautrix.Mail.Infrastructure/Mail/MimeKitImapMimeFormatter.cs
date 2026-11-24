using System.Text;
using Miautrix.Mail.Application.Mail.Mime;
using MimeKit;

namespace Miautrix.Mail.Infrastructure.Mail;

public sealed class MimeKitImapMimeFormatter : IImapMimeFormatter
{
    public string BuildEnvelope(byte[] rawMessage)
    {
        return Envelope(Load(rawMessage));
    }

    private static string Envelope(MimeMessage message)
    {
        var date = message.Date == DateTimeOffset.MinValue ? "NIL" : Quote(message.Date.ToString("ddd, dd MMM yyyy HH:mm:ss zzz", System.Globalization.CultureInfo.InvariantCulture));
        return $"({date} {NString(message.Subject)} {AddressList(message.From)} {AddressList(message.Sender)} {AddressList(message.ReplyTo)} {AddressList(message.To)} {AddressList(message.Cc)} {AddressList(message.Bcc)} {NString(message.InReplyTo)} {NString(message.MessageId)})";
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
        using var stream = new MemoryStream(raw, writable: false);
        return MimeMessage.Load(stream, persistent: false);
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
                addresses.Add($"({NString(group.Name)} NIL NIL NIL)");
                addresses.AddRange(group.Members.OfType<MailboxAddress>().Select(FormatMailbox));
                addresses.Add("(NIL NIL NIL NIL)");
            }
        }
        var values = string.Join(" ", addresses);
        return values.Length == 0 ? "NIL" : $"({values})";
    }

    private static string FormatMailbox(MailboxAddress mailbox)
    {
        var at = mailbox.Address.LastIndexOf('@');
        var local = at < 0 ? mailbox.Address : mailbox.Address[..at];
        var host = at < 0 ? string.Empty : mailbox.Address[(at + 1)..];
        return $"({NString(mailbox.Name)} NIL {NString(local)} {NString(host)})";
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
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", string.Empty).Replace("\n", string.Empty) + "\"";
    private static byte[] SerializeHeaders(HeaderList headers) => Encoding.UTF8.GetBytes(string.Join("\r\n", headers.Select(h => $"{h.Field}: {h.Value}")) + "\r\n\r\n");
    private static int LineCount(byte[] bytes) => Encoding.UTF8.GetString(bytes).Count(c => c == '\n');
}
