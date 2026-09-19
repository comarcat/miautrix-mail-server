using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Auth;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IRequestContextAccessor _requestContext;

    public AuthController(IAuthService authService, IRequestContextAccessor requestContext)
    {
        _authService = authService;
        _requestContext = requestContext;
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.AuthenticateAsync(
            request.EmailOrUsername,
            request.Password,
            request.TotpCode,
            ipAddress,
            cancellationToken);

        if (result.Status == AuthStatus.Success)
        {
            return Results.Json(
                new
                {
                    data = result.User,
                    token = result.Token,
                    refreshToken = result.RefreshToken,
                    expiresAt = result.ExpiresAt
                },
                ApiJson.Options,
                statusCode: StatusCodes.Status200OK);
        }

        return ApiResults.Error(
            HttpContext,
            result.Status == AuthStatus.LockedOut ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized,
            "auth_failed",
            result.Message);
    }

    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(
            _requestContext.CurrentTenantId,
            _requestContext.CurrentUserId,
            cancellationToken);

        if (user is null)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "user_not_found",
                "User not found.");
        }

        return Results.Json(
            new { data = user },
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress, cancellationToken);

        if (result.Status == AuthStatus.Success)
        {
            return Results.Json(
                new
                {
                    token = result.Token,
                    refreshToken = result.RefreshToken,
                    expiresAt = result.ExpiresAt
                },
                ApiJson.Options,
                statusCode: StatusCodes.Status200OK);
        }

        return ApiResults.Error(
            HttpContext,
            StatusCodes.Status401Unauthorized,
            "refresh_failed",
            result.Message);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> Logout(CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(
            _requestContext.CurrentTenantId,
            _requestContext.CurrentUserId,
            cancellationToken: cancellationToken);

        return Results.StatusCode(StatusCodes.Status204NoContent);
    }

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ChangePasswordAsync(
            _requestContext.CurrentTenantId,
            _requestContext.CurrentUserId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        if (result.Success)
        {
            return Results.Json(
                new { message = result.Message },
                ApiJson.Options,
                statusCode: StatusCodes.Status200OK);
        }

        return ApiResults.Error(
            HttpContext,
            StatusCodes.Status400BadRequest,
            "password_change_failed",
            result.Message);
    }
}

public sealed record LoginRequest(string EmailOrUsername, string Password, string? TotpCode = null);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
