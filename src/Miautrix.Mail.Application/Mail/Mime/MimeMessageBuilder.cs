using System.Text;

namespace Miautrix.Mail.Application.Mail.Mime;

public sealed record MimeMessageRequest(
    string From,
    IReadOnlyList<string> To,
    IReadOnlyList<string>? Cc,
    IReadOnlyList<string>? Bcc,
    string Subject,
    string? BodyText,
    string? BodyHtml,
    IReadOnlyList<MimeAttachment>? Attachments,
    DateTimeOffset Date,
    string? InReplyTo = null,
    IReadOnlyList<string>? References = null);

public sealed record BuiltMimeMessage(
    string RawMessage,
    string TopLevelHeaders,
    string MessageId,
    long SizeBytes);

public static class MimeMessageBuilder
{
    public static BuiltMimeMessage Build(MimeMessageRequest request)
    {
        Validate(request);
        var messageId = BuildMessageId(request.From);
        var body = BuildBody(request);
        var headers = BuildHeaders(request, messageId, body.ContentType);
        var raw = headers + "\r\n\r\n" + body.Content + "\r\n";
        return new BuiltMimeMessage(raw, headers, messageId, Encoding.UTF8.GetByteCount(raw));
    }

    private static void Validate(MimeMessageRequest request)
    {
        MimeEncoders.RejectHeaderInjection(request.From, nameof(request.From));
        MimeEncoders.RejectHeaderInjection(request.Subject, nameof(request.Subject));
        foreach (var recipient in request.To.Concat(request.Cc ?? Array.Empty<string>()).Concat(request.Bcc ?? Array.Empty<string>()))
        {
            MimeEncoders.RejectHeaderInjection(recipient, "recipient");
        }
    }

    private static string BuildHeaders(MimeMessageRequest request, string messageId, string contentType)
    {
        var headers = new List<string>
        {
            MimeEncoders.Header("From", MimeEncoders.FormatAddress(request.From)),
            MimeEncoders.Header("To", MimeEncoders.FormatAddressList(request.To))
        };

        if (request.Cc is { Count: > 0 })
        {
            headers.Add(MimeEncoders.Header("Cc", MimeEncoders.FormatAddressList(request.Cc)));
        }

        headers.Add(MimeEncoders.Header("Subject", string.IsNullOrWhiteSpace(request.Subject) ? "(No Subject)" : request.Subject));
        headers.Add($"Date: {request.Date:R}");
        headers.Add($"Message-ID: {messageId}");
        if (!string.IsNullOrWhiteSpace(request.InReplyTo))
        {
            headers.Add(MimeEncoders.Header("In-Reply-To", request.InReplyTo));
        }

        if (request.References is { Count: > 0 })
        {
            headers.Add(MimeEncoders.Header("References", string.Join(" ", request.References)));
        }

        headers.Add("MIME-Version: 1.0");
        headers.Add("Content-Type: " + contentType);
        return string.Join("\r\n", headers);
    }

    private static (string ContentType, string Content) BuildBody(MimeMessageRequest request)
    {
        var attachments = request.Attachments ?? Array.Empty<MimeAttachment>();
        var text = request.BodyText;
        var html = request.BodyHtml;

        if (attachments.Count == 0)
        {
            if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(html))
            {
                var boundary = NewBoundary(text + html);
                return ($"multipart/alternative; boundary=\"{boundary}\"", BuildAlternative(boundary, text, html, Array.Empty<MimeAttachment>()));
            }

            return BuildSingleTextPart(string.IsNullOrEmpty(html) ? "text/plain" : "text/html", text ?? html ?? string.Empty);
        }

        var outer = NewBoundary(string.Concat(attachments.Select(a => Encoding.UTF8.GetString(a.Content))));
        var inner = NewBoundary((text ?? string.Empty) + (html ?? string.Empty));
        var builder = new StringBuilder();
        AppendBoundary(builder, outer);
        builder.Append("Content-Type: multipart/alternative; boundary=\"").Append(inner).Append("\"\r\n\r\n");
        builder.Append(BuildAlternative(inner, text, html, attachments.Where(a => IsCalendar(a)).ToList()));
        foreach (var attachment in attachments.Where(a => !IsCalendar(a)))
        {
            builder.Append("\r\n");
            AppendBoundary(builder, outer);
            AppendAttachment(builder, attachment);
        }

        AppendClosingBoundary(builder, outer);
        return ($"multipart/mixed; boundary=\"{outer}\"", builder.ToString());
    }

    private static (string ContentType, string Content) BuildSingleTextPart(string contentType, string value)
    {
        var content = MimeEncoders.EncodeTextPart(value, out var encoding);
        return ($"{contentType}; charset=utf-8\r\nContent-Transfer-Encoding: {encoding}", content);
    }

    private static string BuildAlternative(string boundary, string? text, string? html, IReadOnlyList<MimeAttachment> calendarAttachments)
    {
        var builder = new StringBuilder();
        AppendBoundary(builder, boundary);
        AppendTextPart(builder, "text/plain", text ?? StripHtml(html) ?? string.Empty);
        if (!string.IsNullOrEmpty(html))
        {
            builder.Append("\r\n");
            AppendBoundary(builder, boundary);
            AppendTextPart(builder, "text/html", html);
        }

        foreach (var calendar in calendarAttachments)
        {
            builder.Append("\r\n");
            AppendBoundary(builder, boundary);
            AppendAttachment(builder, calendar);
        }

        AppendClosingBoundary(builder, boundary);
        return builder.ToString();
    }

    private static void AppendTextPart(StringBuilder builder, string contentType, string value)
    {
        var content = MimeEncoders.EncodeTextPart(value, out var encoding);
        builder.Append("Content-Type: ").Append(contentType).Append("; charset=utf-8\r\n");
        builder.Append("Content-Transfer-Encoding: ").Append(encoding).Append("\r\n\r\n");
        builder.Append(content).Append("\r\n");
    }

    private static void AppendAttachment(StringBuilder builder, MimeAttachment attachment)
    {
        var contentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType.Trim();
        builder.Append("Content-Type: ").Append(contentType);
        if (!string.IsNullOrWhiteSpace(attachment.Method))
        {
            builder.Append("; method=").Append(attachment.Method);
        }

        if (!string.IsNullOrWhiteSpace(attachment.Component))
        {
            builder.Append("; component=").Append(attachment.Component);
        }

        builder.Append("; name=\"").Append(EscapeQuoted(attachment.FileName)).Append("\"\r\n");
        builder.Append("Content-Transfer-Encoding: base64\r\n");
        builder.Append("Content-Disposition: ").Append(attachment.Inline ? "inline" : "attachment").Append("; ")
            .Append(MimeEncoders.ContentDispositionFilename(attachment.FileName)).Append("\r\n");
        if (!string.IsNullOrWhiteSpace(attachment.ContentId))
        {
            builder.Append("Content-ID: <").Append(EscapeQuoted(attachment.ContentId)).Append(">\r\n");
        }

        builder.Append("\r\n");
        builder.Append(MimeEncoders.Base64Block(attachment.Content)).Append("\r\n");
    }

    private static void AppendBoundary(StringBuilder builder, string boundary) =>
        builder.Append("--").Append(boundary).Append("\r\n");

    private static void AppendClosingBoundary(StringBuilder builder, string boundary) =>
        builder.Append("--").Append(boundary).Append("--\r\n");

    private static string NewBoundary(string content)
    {
        for (var i = 0; i < 10; i++)
        {
            var boundary = "=_miautrix_" + Guid.NewGuid().ToString("N");
            if (!content.Contains(boundary, StringComparison.Ordinal))
            {
                return boundary;
            }
        }

        throw new InvalidOperationException("Unable to generate a MIME boundary that does not collide with the content.");
    }

    private static string BuildMessageId(string from)
    {
        var domain = "miautrix.local";
        var at = from.LastIndexOf('@');
        if (at >= 0 && at + 1 < from.Length)
        {
            domain = from[(at + 1)..].Trim('>', ' ', '\t');
        }

        return $"<{Guid.NewGuid():N}@{domain}>";
    }

    private static bool IsCalendar(MimeAttachment attachment) =>
        attachment.ContentType.StartsWith("text/calendar", StringComparison.OrdinalIgnoreCase) ||
        attachment.FileName.EndsWith(".ics", StringComparison.OrdinalIgnoreCase);

    private static string EscapeQuoted(string value) =>
        (string.IsNullOrWhiteSpace(value) ? "attachment" : Path.GetFileName(value.Trim())).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string? StripHtml(string? html) =>
        string.IsNullOrWhiteSpace(html) ? null : System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
}
