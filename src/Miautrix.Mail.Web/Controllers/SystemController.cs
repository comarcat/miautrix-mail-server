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
[Route("api/v1/system")]
public sealed class SystemController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IRequestContextAccessor _context;

    public SystemController(IAdminService adminService, IRequestContextAccessor context)
    {
        _adminService = adminService;
        _context = context;
    }

    [HttpGet("dashboard-summary")]
    [ProducesResponseType(typeof(ApiResponse<DashboardSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IResult> GetDashboardSummary(CancellationToken cancellationToken)
    {
        var summary = await _adminService.GetDashboardSummaryAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);

        return Results.Json(
            new ApiResponse<DashboardSummaryDto>(summary),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("info")]
    [ProducesResponseType(typeof(ApiResponse<SystemInfoDto>), StatusCodes.Status200OK)]
    public async Task<IResult> GetInfo(CancellationToken cancellationToken)
    {
        var info = await _adminService.GetSystemInfoAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);

        return Results.Json(
            new ApiResponse<SystemInfoDto>(info),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("licensing")]
    [ProducesResponseType(typeof(ApiResponse<LicensingDto>), StatusCodes.Status200OK)]
    public async Task<IResult> GetLicensing(CancellationToken cancellationToken)
    {
        var licensing = await _adminService.GetLicensingAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);

        return Results.Json(
            new ApiResponse<LicensingDto>(licensing),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("backup")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BackupJobDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> GetBackups(CancellationToken cancellationToken)
    {
        var backups = await _adminService.GetBackupJobsAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);

        return Results.Json(
            new ApiResponse<IReadOnlyList<BackupJobDto>>(backups),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpPost("backup")]
    [ProducesResponseType(typeof(BackupActionResult), StatusCodes.Status200OK)]
    public async Task<IResult> CreateBackup(CancellationToken cancellationToken)
    {
        var job = await _adminService.CreateBackupAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);

        return Results.Json(
            new BackupActionResult(true, $"Backup '{job.Name}' completed successfully."),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }
}

public sealed record BackupActionResult(bool Success, string Message);
