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
[Route("api/v1/quarantine")]
public sealed class QuarantineController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IRequestContextAccessor _context;

    public QuarantineController(IAdminService adminService, IRequestContextAccessor context)
    {
        _adminService = adminService;
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<QuarantineItemDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> List(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new QuarantineFilter(status, search, limit, cursor);
        var items = await _adminService.ListQuarantineAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            filter,
            cancellationToken);

        return Results.Json(
            new ApiResponse<IReadOnlyList<QuarantineItemDto>>(items),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<QuarantineItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _adminService.GetQuarantineItemAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            id,
            cancellationToken);

        if (item is null)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "quarantine_item_not_found",
                "Quarantine item not found.");
        }

        return Results.Json(
            new ApiResponse<QuarantineItemDto>(item),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpPost("{id:guid}/release")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Release(Guid id, CancellationToken cancellationToken)
    {
        var released = await _adminService.ReleaseQuarantineItemAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            id,
            cancellationToken);

        if (!released)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "quarantine_item_not_found",
                "Quarantine item not found.");
        }

        return Results.Json(
            new { success = true, message = "Message released and delivered to recipient inbox." },
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _adminService.DeleteQuarantineItemAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            id,
            cancellationToken);

        if (!deleted)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "quarantine_item_not_found",
                "Quarantine item not found.");
        }

        return Results.StatusCode(StatusCodes.Status204NoContent);
    }
}
