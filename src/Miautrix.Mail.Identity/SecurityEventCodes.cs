namespace Miautrix.Mail.Identity;

public static class SecurityEventCodes
{
    public const string AuthLoginSuccess = "AUTH-4001";
    public const string AuthLoginFailed = "AUTH-4010";
    public const string AuthAccountLocked = "AUTH-4011";
    public const string AuthMfaRequired = "AUTH-4020";
    public const string AuthMfaSuccess = "AUTH-4030";
    public const string AuthMfaFailed = "AUTH-4031";
    public const string AuthPasswordChanged = "AUTH-4040";
    public const string AuthSessionRevoked = "AUTH-4050";
    public const string AuthPrivilegeEscalation = "AUTH-4060";
    public const string AuthInboundWebhookRejected = "AUTH-4070";
}
