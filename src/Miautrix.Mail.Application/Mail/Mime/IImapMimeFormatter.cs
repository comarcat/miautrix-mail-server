namespace Miautrix.Mail.Application.Mail.Mime;

public interface IImapMimeFormatter
{
    string BuildEnvelope(byte[] rawMessage);
    string BuildBodyStructure(byte[] rawMessage, bool extended = true);
    byte[] GetBodySection(byte[] rawMessage, string section);
}
