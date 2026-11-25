namespace Miautrix.Mail.Application.Mail.Mime;

public interface IImapMimeFormatter
{
    string BuildEnvelope(byte[] rawMessage);
    string BuildBodyStructure(byte[] rawMessage, bool extended = true);
    byte[] GetBodySection(byte[] rawMessage, string section);
    byte[] GetBinarySection(byte[] rawMessage, string section) => GetBodySection(rawMessage, section);
    bool TryGetBinarySection(byte[] rawMessage, string section, out byte[] bytes)
    {
        bytes = GetBinarySection(rawMessage, section);
        return true;
    }
}
