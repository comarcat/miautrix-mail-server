namespace Miautrix.Mail.Protocols.Smtp;

public record SmtpResponse(int Code, string EnhancedCode, string Message)
{
    public bool IsSuccess => Code is >= 200 and < 400;

    public string ToSmtpString() => $"{Code} {EnhancedCode} {Message}";

    public static readonly SmtpResponse Ok = new(250, "2.0.0", "OK");
    public static readonly SmtpResponse Queued = new(250, "2.0.0", "Message accepted and queued");
    public static readonly SmtpResponse RelayAccessDenied = new(550, "5.7.1", "Relay access denied");
    public static readonly SmtpResponse AuthenticationRequired = new(530, "5.7.0", "Authentication required");
    public static readonly SmtpResponse EncryptionRequired = new(538, "5.7.11", "Encryption required for requested authentication mechanism");
    public static readonly SmtpResponse InvalidRecipient = new(550, "5.1.1", "User unknown");
    public static readonly SmtpResponse SyntaxError = new(500, "5.5.2", "Syntax error, command unrecognized");
}

public interface ISmtpDomainValidator
{
    bool IsDomainLocal(string emailAddress, out Guid tenantId);
}

public interface ISmtpAuthenticator
{
    bool Authenticate(string username, string password, bool isTlsEncrypted, out Guid tenantId, out Guid userId);
}
