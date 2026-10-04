using Miautrix.Mail.Identity;
using Xunit;

namespace Miautrix.Mail.SecurityTests.Identity;

[Trait("Category", "Identity")]
public class IdentityTests
{
    private readonly IPasswordHasher _passwordHasher = new Argon2idPasswordHasher();
    private readonly ITotpService _totpService = new TotpService();
    private readonly ISessionManager _sessionManager = new SessionManager();

    [Fact]
    public void When_user_with_totp_submits_correct_password_without_second_factor_refuses_and_writes_auth4020()
    {
        // ARRANGE
        var eventSink = new InMemorySecurityEventSink();
        var authService = new AuthenticationService(_passwordHasher, _totpService, _sessionManager, eventSink);

        var rawPassword = "CorrectHorseBatteryStaple#2026";
        var passwordHash = _passwordHasher.HashPassword(rawPassword);
        var totpSecret = _totpService.GenerateSecret();

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new UserAccount(
            UserId: userId,
            TenantId: tenantId,
            Username: "admin@miautrix.local",
            PasswordHash: passwordHash,
            IsTotpEnabled: true,
            TotpSecret: totpSecret,
            IsLockedOut: false);

        // ACT: Submit correct password without second factor (totpCode = null)
        var result = authService.Authenticate(user, rawPassword, totpCode: null);

        // ASSERT: Must be refused and status MfaRequired
        Assert.Equal(LoginStatus.MfaRequired, result.Status);
        Assert.Null(result.SessionToken);

        // ASSERT: Exactly one AUTH-4020 event written
        var events = eventSink.GetEvents();
        Assert.Single(events);
        var authEvent = events[0];
        Assert.Equal(SecurityEventCodes.AuthMfaRequired, authEvent.EventCode);
        Assert.Equal("AUTH-4020", authEvent.EventCode);
        Assert.Equal(tenantId, authEvent.TenantId);
        Assert.Equal(userId, authEvent.UserId);
    }

    [Fact]
    public void When_user_with_totp_submits_correct_password_and_valid_totp_succeeds_and_writes_auth4001()
    {
        // ARRANGE
        var eventSink = new InMemorySecurityEventSink();
        var authService = new AuthenticationService(_passwordHasher, _totpService, _sessionManager, eventSink);

        var rawPassword = "CorrectPassword123!";
        var passwordHash = _passwordHasher.HashPassword(rawPassword);
        var totpSecret = _totpService.GenerateSecret();

        // Compute valid totp
        var secretBytes = OtpNet.Base32Encoding.ToBytes(totpSecret);
        var totp = new OtpNet.Totp(secretBytes);
        var validCode = totp.ComputeTotp();

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new UserAccount(
            UserId: userId,
            TenantId: tenantId,
            Username: "user@miautrix.local",
            PasswordHash: passwordHash,
            IsTotpEnabled: true,
            TotpSecret: totpSecret,
            IsLockedOut: false);

        // ACT
        var result = authService.Authenticate(user, rawPassword, totpCode: validCode);

        // ASSERT
        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.NotNull(result.SessionToken);
        Assert.NotNull(result.RefreshToken);

        var events = eventSink.GetEvents();
        Assert.Equal(2, events.Count); // AUTH-4030 (MFA success) + AUTH-4001 (Login success)
        Assert.Contains(events, e => e.EventCode == SecurityEventCodes.AuthMfaSuccess);
        Assert.Contains(events, e => e.EventCode == SecurityEventCodes.AuthLoginSuccess);
    }

    [Fact]
    public void When_user_submits_wrong_password_refuses_and_writes_auth4010()
    {
        // ARRANGE
        var eventSink = new InMemorySecurityEventSink();
        var authService = new AuthenticationService(_passwordHasher, _totpService, _sessionManager, eventSink);

        var passwordHash = _passwordHasher.HashPassword("RealPassword");
        var user = new UserAccount(
            UserId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            Username: "test@miautrix.local",
            PasswordHash: passwordHash,
            IsTotpEnabled: false,
            TotpSecret: null);

        // ACT
        var result = authService.Authenticate(user, "WrongPassword");

        // ASSERT
        Assert.Equal(LoginStatus.Failed, result.Status);
        var events = eventSink.GetEvents();
        Assert.Single(events);
        Assert.Equal(SecurityEventCodes.AuthLoginFailed, events[0].EventCode);
        Assert.Equal("AUTH-4010", events[0].EventCode);
    }
}
