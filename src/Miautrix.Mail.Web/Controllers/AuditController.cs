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
[Route("api/v1/audit")]
public sealed class AuditController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IRequestContextAccessor _context;

    public AuditController(IAdminService adminService, IRequestContextAccessor context)
    {
        _adminService = adminService;
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuditLogDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> List(
        [FromQuery] string? action = null,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new AuditFilter(action, search, limit, cursor);
        var logs = await _adminService.ListAuditLogsAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            filter,
            cancellationToken);

        return Results.Json(
            new ApiResponse<IReadOnlyList<AuditLogDto>>(logs),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }
}
