using System;
using System.Threading;
using System.Threading.Tasks;

namespace Miautrix.Mail.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> AuthenticateAsync(
        string emailOrUsername,
        string password,
        string? totpCode = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<AuthUserDto?> GetCurrentUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ChangePasswordResult> ChangePasswordAsync(
        Guid tenantId,
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<AuthResult> RefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        Guid tenantId,
        Guid userId,
        string? rawSessionToken = null,
        CancellationToken cancellationToken = default);
}
