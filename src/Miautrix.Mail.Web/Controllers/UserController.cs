using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Admin;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UserController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IRequestContextAccessor _context;

    public UserController(IAdminService adminService, IRequestContextAccessor context)
    {
        _adminService = adminService;
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminUserDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> List(CancellationToken cancellationToken)
    {
        var users = await _adminService.ListUsersAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);

        return Results.Json(
            new ApiResponse<IReadOnlyList<AdminUserDto>>(users),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _adminService.GetUserAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            userId,
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
            new ApiResponse<AdminUserDto>(user),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDto>), StatusCodes.Status201Created)]
    public async Task<IResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _adminService.CreateUserAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            request,
            cancellationToken);

        return Results.Json(
            new ApiResponse<AdminUserDto>(user),
            ApiJson.Options,
            statusCode: StatusCodes.Status201Created);
    }

    [HttpPut("{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(Guid userId, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _adminService.UpdateUserAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            userId,
            request,
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
            new ApiResponse<AdminUserDto>(user),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(Guid userId, CancellationToken cancellationToken)
    {
        var deleted = await _adminService.DeleteUserAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            userId,
            cancellationToken);

        if (!deleted)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "user_not_found",
                "User not found.");
        }

        return Results.StatusCode(StatusCodes.Status204NoContent);
    }
}
