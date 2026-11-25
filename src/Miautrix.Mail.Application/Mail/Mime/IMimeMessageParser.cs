namespace Miautrix.Mail.Application.Mail.Mime;

public interface IMimeMessageParser
{
    ParsedMailMessage Parse(string rawMessage);
}

public sealed record ParsedMailAttachment(
    string? FileName,
    string ContentType,
    string? ContentId,
    bool IsInline,
    byte[] Content);

public sealed record ParsedMailMessage(
    string RawHeaders,
    string? Subject,
    DateTimeOffset? Date,
    string? From,
    string? To,
    string? Cc,
    string? Bcc,
    string? MessageId,
    string? BodyText,
    string? BodyHtml,
    IReadOnlyList<ParsedMailAttachment> Attachments);
