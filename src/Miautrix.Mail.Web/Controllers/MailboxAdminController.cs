using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Admin;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Miautrix.Mail.Web.Controllers;

/// <summary>
/// Administrative mailbox operations that do not belong to the mailbox owner's own
/// surface: orphan discovery, conversion to a shared mailbox, hard delete and export.
/// </summary>
[ApiController]
[Route("api/v1/mailboxes")]
public sealed class MailboxAdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IRequestContextAccessor _context;

    public MailboxAdminController(IAdminService adminService, IRequestContextAccessor context)
    {
        _adminService = adminService;
        _context = context;
    }

    [HttpGet("orphans")]
    public async Task<IResult> ListOrphans(CancellationToken cancellationToken)
    {
        var mailboxes = await _adminService.ListOrphanMailboxesAsync(
            _context.CurrentTenantId, _context.CurrentUserId, cancellationToken);
        return Results.Json(new ApiResponse<IReadOnlyList<OrphanMailboxDto>>(mailboxes), ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpPost("{mailboxId:guid}/assign")]
    public async Task<IResult> Assign(
        Guid mailboxId,
        [FromBody] AssignMailboxRequest request,
        CancellationToken cancellationToken)
    {
        var mailbox = await _adminService.AssignMailboxAsync(
            _context.CurrentTenantId, _context.CurrentUserId, mailboxId, request, cancellationToken);
        return Results.Json(new ApiResponse<SharedMailboxDto>(mailbox), ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpDelete("{mailboxId:guid}")]
    public async Task<IResult> Delete(
        Guid mailboxId,
        [FromBody] DeleteMailboxRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.DeleteMailboxAsync(
            _context.CurrentTenantId, _context.CurrentUserId, mailboxId, request, cancellationToken);
        return Results.Json(new ApiResponse<DeleteMailboxResult>(result), ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("{mailboxId:guid}/export")]
    public async Task<IResult> Export(Guid mailboxId, CancellationToken cancellationToken)
    {
        var archive = await _adminService.ExportMailboxAsync(
            _context.CurrentTenantId, _context.CurrentUserId, mailboxId, cancellationToken);

        FileStream stream;
        try
        {
            stream = new FileStream(
                archive.TempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        }
        catch (IOException)
        {
            TryDelete(archive.TempFilePath);
            return ApiResults.Error(HttpContext, StatusCodes.Status500InternalServerError,
                "export_failed", "The mailbox archive could not be read.");
        }

        return Results.File(stream, "application/zip", archive.FileName);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort: the temp file is reclaimed by the OS temp cleaner.
        }
    }
}
