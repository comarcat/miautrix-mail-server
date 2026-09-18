namespace Miautrix.Mail.Identity;

public enum LoginStatus
{
    Success,
    Failed,
    MfaRequired,
    LockedOut
}

public sealed record LoginResult(
    LoginStatus Status,
    string? Message = null,
    AuthToken? SessionToken = null,
    AuthToken? RefreshToken = null,
    string? EventCodeWritten = null);

public sealed record UserAccount(
    Guid UserId,
    Guid TenantId,
    string Username,
    string PasswordHash,
    bool IsTotpEnabled,
    string? TotpSecret,
    bool IsLockedOut = false);

public interface ISecurityEventSink
{
    void RecordEvent(Guid tenantId, Guid? userId, string eventCode, string message, string? ipAddress = null);
    IReadOnlyList<SecurityEventRecord> GetEvents();
}

public sealed record SecurityEventRecord(
    Guid TenantId,
    Guid? UserId,
    string EventCode,
    string Message,
    string? IpAddress,
    DateTimeOffset Timestamp);

public sealed class InMemorySecurityEventSink : ISecurityEventSink
{
    private readonly List<SecurityEventRecord> _events = [];
    private readonly object _lock = new();

    public void RecordEvent(Guid tenantId, Guid? userId, string eventCode, string message, string? ipAddress = null)
    {
        lock (_lock)
        {
            _events.Add(new SecurityEventRecord(tenantId, userId, eventCode, message, ipAddress, DateTimeOffset.UtcNow));
        }
    }

    public IReadOnlyList<SecurityEventRecord> GetEvents()
    {
        lock (_lock)
        {
            return _events.ToList();
        }
    }
}

public interface IAuthenticationService
{
    LoginResult Authenticate(UserAccount user, string password, string? totpCode = null, string? ipAddress = null);
}

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITotpService _totpService;
    private readonly ISessionManager _sessionManager;
    private readonly ISecurityEventSink _eventSink;

    public AuthenticationService(
        IPasswordHasher passwordHasher,
        ITotpService totpService,
        ISessionManager sessionManager,
        ISecurityEventSink eventSink)
    {
        _passwordHasher = passwordHasher;
        _totpService = totpService;
        _sessionManager = sessionManager;
        _eventSink = eventSink;
    }

    public LoginResult Authenticate(UserAccount user, string password, string? totpCode = null, string? ipAddress = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsLockedOut)
        {
            _eventSink.RecordEvent(user.TenantId, user.UserId, SecurityEventCodes.AuthAccountLocked, "Account is locked out", ipAddress);
            return new LoginResult(LoginStatus.LockedOut, "Account is locked out", EventCodeWritten: SecurityEventCodes.AuthAccountLocked);
        }

        bool passwordValid = _passwordHasher.VerifyPassword(password, user.PasswordHash);
        if (!passwordValid)
        {
            _eventSink.RecordEvent(user.TenantId, user.UserId, SecurityEventCodes.AuthLoginFailed, "Invalid password", ipAddress);
            return new LoginResult(LoginStatus.Failed, "Invalid credentials", EventCodeWritten: SecurityEventCodes.AuthLoginFailed);
        }

        // Password is valid. Check if TOTP is required.
        if (user.IsTotpEnabled)
        {
            if (string.IsNullOrWhiteSpace(totpCode))
            {
                // Refuse login and write exactly one AUTH-4020 event
                _eventSink.RecordEvent(user.TenantId, user.UserId, SecurityEventCodes.AuthMfaRequired, "MFA challenge required; second factor not provided", ipAddress);
                return new LoginResult(LoginStatus.MfaRequired, "Second factor required", EventCodeWritten: SecurityEventCodes.AuthMfaRequired);
            }

            bool totpValid = !string.IsNullOrEmpty(user.TotpSecret) && _totpService.VerifyCode(user.TotpSecret, totpCode);
            if (!totpValid)
            {
                _eventSink.RecordEvent(user.TenantId, user.UserId, SecurityEventCodes.AuthMfaFailed, "Invalid second factor", ipAddress);
                return new LoginResult(LoginStatus.Failed, "Invalid second factor", EventCodeWritten: SecurityEventCodes.AuthMfaFailed);
            }

            _eventSink.RecordEvent(user.TenantId, user.UserId, SecurityEventCodes.AuthMfaSuccess, "MFA second factor verified", ipAddress);
        }

        // Login success
        var (sessionToken, refreshToken) = _sessionManager.IssueSessionTokens();
        _eventSink.RecordEvent(user.TenantId, user.UserId, SecurityEventCodes.AuthLoginSuccess, "User logged in successfully", ipAddress);

        return new LoginResult(LoginStatus.Success, "Login successful", sessionToken, refreshToken, SecurityEventCodes.AuthLoginSuccess);
    }
}
