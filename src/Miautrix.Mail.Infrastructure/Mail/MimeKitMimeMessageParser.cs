using System.Text;
using Miautrix.Mail.Application.Mail.Mime;
using MimeKit;

namespace Miautrix.Mail.Infrastructure.Mail;

public sealed class MimeKitMimeMessageParser : IMimeMessageParser
{
    public ParsedMailMessage Parse(string rawMessage)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var rawBytes = Encoding.UTF8.GetBytes(rawMessage ?? string.Empty);
        using var stream = new MemoryStream(rawBytes);
        MimeMessage message;
        try
        {
            message = MimeMessage.Load(stream, persistent: false);
        }
        catch (Exception) when (rawMessage is not null)
        {
            return BestEffort(rawMessage);
        }

        var headers = string.Join("\r\n", message.Headers.Select(h => $"{h.Field}: {h.Value}"));
        var attachments = new List<ParsedMailAttachment>();
        foreach (var part in message.BodyParts.OfType<MimePart>())
        {
            var fileName = part.FileName;
            var isAttachment = part.IsAttachment ||
                (!string.IsNullOrWhiteSpace(fileName) && !part.ContentType.IsMimeType("text", "plain") && !part.ContentType.IsMimeType("text", "html"));
            if (!isAttachment) continue;

            if (part.Content is null) continue;
            using var content = new MemoryStream();
            try { part.Content.DecodeTo(content); }
            catch (Exception) { continue; }
            attachments.Add(new ParsedMailAttachment(
                fileName,
                part.ContentType.MimeType,
                part.ContentId,
                part.ContentDisposition?.Disposition.Equals("inline", StringComparison.OrdinalIgnoreCase) == true,
                content.ToArray()));
        }

        return new ParsedMailMessage(
            headers,
            message.Subject,
            message.Date == DateTimeOffset.MinValue ? null : message.Date.ToUniversalTime(),
            message.From.ToString(), message.To.ToString(), message.Cc.ToString(), message.MessageId,
            message.TextBody, message.HtmlBody, attachments);
    }

    private static ParsedMailMessage BestEffort(string raw)
    {
        var normalized = raw.Replace("\r\n", "\n", StringComparison.Ordinal);
        var split = normalized.IndexOf("\n\n", StringComparison.Ordinal);
        var headers = split < 0 ? normalized : normalized[..split];
        var subject = headers.Split('\n').FirstOrDefault(x => x.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))?
            .Split(':', 2).Skip(1).FirstOrDefault()?.Trim();
        return new ParsedMailMessage(headers, subject, null, null, null, null, null,
            split < 0 ? null : normalized[(split + 2)..], null, Array.Empty<ParsedMailAttachment>());
    }
}
