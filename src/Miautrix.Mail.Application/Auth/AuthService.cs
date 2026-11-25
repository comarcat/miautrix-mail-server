using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITotpService _totpService;
    private readonly ISessionManager _sessionManager;
    private readonly ISecurityEventSink _eventSink;

    public AuthService(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        ITotpService totpService,
        ISessionManager sessionManager,
        ISecurityEventSink eventSink)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _totpService = totpService;
        _sessionManager = sessionManager;
        _eventSink = eventSink;
    }

    public async Task<AuthResult> AuthenticateAsync(
        string emailOrUsername,
        string password,
        string? totpCode = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(emailOrUsername) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(AuthStatus.Failed, "Username and password are required.");
        }

        var normalizedInput = emailOrUsername.Trim().ToLowerInvariant();

        // 1. Locate user by email
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedInput, cancellationToken);

        if (user is null)
        {
            _eventSink.RecordEvent(Guid.Empty, null, SecurityEventCodes.AuthLoginFailed, $"Failed login for unknown user '{emailOrUsername}'.", ipAddress);
            return new AuthResult(AuthStatus.Failed, "Invalid username or password.", EventCode: SecurityEventCodes.AuthLoginFailed);
        }

        if (!user.IsActive)
        {
            _eventSink.RecordEvent(user.TenantId, user.Id, SecurityEventCodes.AuthAccountLocked, $"Account '{user.Email}' is disabled.", ipAddress);
            return new AuthResult(AuthStatus.LockedOut, "User account is disabled.", EventCode: SecurityEventCodes.AuthAccountLocked);
        }

        // 2. Fetch credential
        var credential = await _db.UserCredentials
            .FirstOrDefaultAsync(c => c.TenantId == user.TenantId && c.UserId == user.Id, cancellationToken);

        if (credential is null)
        {
            _eventSink.RecordEvent(user.TenantId, user.Id, SecurityEventCodes.AuthLoginFailed, "User has no credentials configured.", ipAddress);
            return new AuthResult(AuthStatus.Failed, "Invalid username or password.", EventCode: SecurityEventCodes.AuthLoginFailed);
        }

        // 3. Verify password hash (Argon2id)
        bool passwordValid = _passwordHasher.VerifyPassword(password, credential.PasswordHash);
        if (!passwordValid)
        {
            _eventSink.RecordEvent(user.TenantId, user.Id, SecurityEventCodes.AuthLoginFailed, "Invalid password attempt.", ipAddress);
            return new AuthResult(AuthStatus.Failed, "Invalid username or password.", EventCode: SecurityEventCodes.AuthLoginFailed);
        }

        // 4. Resolve roles and permissions
        var rolesAndPerms = await ResolveRolesAndPermissionsAsync(user.TenantId, user.Id, cancellationToken);

        // 5. Check primary mailbox if exists
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == user.TenantId && m.Address.ToLower() == user.Email.ToLower(), cancellationToken);

        var userDto = new AuthUserDto(
            user.Id,
            user.TenantId,
            user.Email,
            user.Name,
            user.IsActive,
            credential.MustChangePassword,
            rolesAndPerms.Roles,
            rolesAndPerms.Permissions,
            mailbox?.Address);

        // 6. Issue tokens
        var configuredMinutes = await _db.Domains.Where(d => d.TenantId == user.TenantId && d.SessionLifetimeMinutes.HasValue)
            .Select(d => d.SessionLifetimeMinutes).FirstOrDefaultAsync(cancellationToken);
        var sessionLifetime = configuredMinutes is > 0 and <= 1440 ? TimeSpan.FromMinutes(configuredMinutes.Value) : _sessionManager.DefaultSessionLifetime;
        var (sessionToken, refreshToken) = _sessionManager.IssueSessionTokens(sessionLifetime);
        var session = new Session { TenantId = user.TenantId, UserId = user.Id, AccessTokenHash = sessionToken.HashedToken, ExpiresAt = sessionToken.ExpiresAt, LastSeenAt = DateTimeOffset.UtcNow, ClientHash = string.IsNullOrWhiteSpace(ipAddress) ? null : _sessionManager.HashToken(ipAddress) };
        _db.Sessions.Add(session);
        _db.RefreshTokens.Add(new RefreshToken { TenantId = user.TenantId, UserId = user.Id, SessionId = session.Id, TokenHash = refreshToken.HashedToken, ExpiresAt = refreshToken.ExpiresAt });
        await _db.SaveChangesAsync(cancellationToken);
        _eventSink.RecordEvent(user.TenantId, user.Id, SecurityEventCodes.AuthLoginSuccess, "User logged in successfully.", ipAddress);

        return new AuthResult(
            AuthStatus.Success,
            "Login successful.",
            sessionToken.RawToken,
            refreshToken.RawToken,
            sessionToken.ExpiresAt,
            userDto,
            SecurityEventCodes.AuthLoginSuccess);
    }

    public async Task<AuthUserDto?> GetCurrentUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var credential = await _db.UserCredentials
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.UserId == userId, cancellationToken);

        var rolesAndPerms = await ResolveRolesAndPermissionsAsync(tenantId, userId, cancellationToken);
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Address.ToLower() == user.Email.ToLower(), cancellationToken);

        return new AuthUserDto(
            user.Id,
            user.TenantId,
            user.Email,
            user.Name,
            user.IsActive,
            credential?.MustChangePassword ?? false,
            rolesAndPerms.Roles,
            rolesAndPerms.Permissions,
            mailbox?.Address);
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(
        Guid tenantId,
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default,
        Guid? currentSessionId = null)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return new ChangePasswordResult(false, "Current password and new password are required.");
        }

        if (newPassword.Length < 8)
        {
            return new ChangePasswordResult(false, "New password must be at least 8 characters long.");
        }

        var credential = await _db.UserCredentials
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.UserId == userId, cancellationToken);

        if (credential is null)
        {
            return new ChangePasswordResult(false, "Credentials not found.");
        }

        if (!_passwordHasher.VerifyPassword(currentPassword, credential.PasswordHash))
        {
            _eventSink.RecordEvent(tenantId, userId, SecurityEventCodes.AuthLoginFailed, "Invalid current password during password change.");
            return new ChangePasswordResult(false, "Current password is incorrect.");
        }

        credential.PasswordHash = _passwordHasher.HashPassword(newPassword);
        credential.Algorithm = "argon2id";
        credential.MustChangePassword = false;
        credential.UpdatedAt = DateTimeOffset.UtcNow;

        var otherSessions = _db.Sessions.Where(s => s.TenantId == tenantId && s.UserId == userId && s.RevokedAt == null && (!currentSessionId.HasValue || s.Id != currentSessionId.Value));
        await otherSessions.ExecuteUpdateAsync(setters => setters.SetProperty(s => s.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);
        await _db.RefreshTokens.Where(t => t.TenantId == tenantId && t.UserId == userId && t.RevokedAt == null && (!currentSessionId.HasValue || t.SessionId != currentSessionId.Value))
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        _eventSink.RecordEvent(tenantId, userId, SecurityEventCodes.AuthPasswordChanged, "User password changed successfully.");

        return new ChangePasswordResult(true, "Password changed successfully.");
    }

    public async Task<AuthResult> RefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return new AuthResult(AuthStatus.Failed, "Refresh token is required.");
        var hash = _sessionManager.HashToken(rawRefreshToken);
        var stored = await _db.RefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (stored is null) return new AuthResult(AuthStatus.Failed, "Refresh token is invalid.");
        var session = await _db.Sessions.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == stored.SessionId, cancellationToken);
        if (session is null) return new AuthResult(AuthStatus.Failed, "Refresh token is invalid.");
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored.TokenHash), Encoding.UTF8.GetBytes(hash))) return new AuthResult(AuthStatus.Failed, "Refresh token is invalid.");
        if (stored.UsedAt.HasValue)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            await _db.RefreshTokens.IgnoreQueryFilters().Where(x => x.SessionId == session.Id && x.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return new AuthResult(AuthStatus.Failed, "Refresh token reuse detected; session revoked.");
        }
        if (stored.RevokedAt.HasValue || stored.ExpiresAt <= DateTimeOffset.UtcNow || session.RevokedAt.HasValue || session.ExpiresAt <= DateTimeOffset.UtcNow)
            return new AuthResult(AuthStatus.Failed, "Refresh token is expired or revoked.");
        stored.UsedAt = DateTimeOffset.UtcNow;
        var (access, refresh) = _sessionManager.IssueSessionTokens();
        session.AccessTokenHash = access.HashedToken;
        session.ExpiresAt = access.ExpiresAt;
        session.LastSeenAt = DateTimeOffset.UtcNow;
        _db.RefreshTokens.Add(new RefreshToken { TenantId = session.TenantId, UserId = session.UserId, SessionId = session.Id, TokenHash = refresh.HashedToken, ExpiresAt = refresh.ExpiresAt });
        await _db.SaveChangesAsync(cancellationToken);
        return new AuthResult(AuthStatus.Success, "Tokens refreshed.", access.RawToken, refresh.RawToken, access.ExpiresAt);
    }

    public async Task LogoutAsync(
        Guid tenantId,
        Guid userId,
        string? rawSessionToken = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(rawSessionToken))
        {
            var hash = _sessionManager.HashToken(rawSessionToken);
            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.UserId == userId && s.AccessTokenHash == hash, cancellationToken);
            if (session is not null)
            {
                session.RevokedAt = DateTimeOffset.UtcNow;
                await _db.RefreshTokens.Where(t => t.SessionId == session.Id && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        _eventSink.RecordEvent(tenantId, userId, SecurityEventCodes.AuthSessionRevoked, "User session logged out.");
    }

    private async Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> ResolveRolesAndPermissionsAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var memberships = await _db.Memberships
            .Where(m => m.TenantId == tenantId && m.UserId == userId)
            .ToListAsync(cancellationToken);

        var roleIds = memberships
            .Where(m => m.RoleId.HasValue)
            .Select(m => m.RoleId!.Value)
            .Distinct()
            .ToList();

        var roles = await _db.Roles
            .Where(r => r.TenantId == tenantId && roleIds.Contains(r.Id))
            .Select(r => r.Code)
            .ToListAsync(cancellationToken);

        var rolePermissions = await _db.RolePermissions
            .Where(rp => rp.TenantId == tenantId && rp.RoleId.HasValue && roleIds.Contains(rp.RoleId.Value))
            .ToListAsync(cancellationToken);

        var permissionIds = rolePermissions
            .Where(rp => rp.PermissionId.HasValue)
            .Select(rp => rp.PermissionId!.Value)
            .Distinct()
            .ToList();

        var permissions = await _db.Permissions
            .Where(p => p.TenantId == tenantId && permissionIds.Contains(p.Id))
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);

        return (roles, permissions);
    }
}
