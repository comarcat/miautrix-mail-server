namespace Miautrix.Mail.Application.Mail.Mime;

public sealed record MimeAttachment(
    string FileName,
    string ContentType,
    byte[] Content,
    bool Inline = false,
    string? ContentId = null,
    string? Method = null,
    string? Component = null);
