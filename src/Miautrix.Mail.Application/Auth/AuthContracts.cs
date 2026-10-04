using System;
using System.Collections.Generic;

namespace Miautrix.Mail.Application.Auth;

public enum AuthStatus
{
    Success,
    Failed,
    MfaRequired,
    LockedOut,
    UserNotFound
}

public sealed record AuthUserDto(
    Guid Id,
    Guid TenantId,
    string Email,
    string Name,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    string? MailboxAddress = null);

public sealed record AuthResult(
    AuthStatus Status,
    string Message,
    string? Token = null,
    string? RefreshToken = null,
    DateTimeOffset? ExpiresAt = null,
    AuthUserDto? User = null,
    string? EventCode = null);

public sealed record ChangePasswordResult(
    bool Success,
    string Message);
