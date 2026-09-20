using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
[Route("api/v1/mailboxes")]
public sealed class MailboxController : ControllerBase
{
    private readonly IMailboxService _mailboxService;
    private readonly IRequestContextAccessor _context;

    public MailboxController(IMailboxService mailboxService, IRequestContextAccessor context)
    {
        _mailboxService = mailboxService;
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MailboxDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> List(CancellationToken cancellationToken)
    {
        var mailboxes = await _mailboxService.ListMailboxesAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);

        return Results.Json(
            new ApiResponse<IReadOnlyList<MailboxDto>>(mailboxes),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("primary")]
    [ProducesResponseType(typeof(ApiResponse<MailboxDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> GetPrimary(CancellationToken cancellationToken)
    {
        var mailbox = await _mailboxService.GetPrimaryMailboxAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);

        if (mailbox is null)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "mailbox_not_found",
                "Primary mailbox not found.");
        }

        return Results.Json(
            new ApiResponse<MailboxDto>(mailbox),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("{mailboxId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MailboxDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid mailboxId, CancellationToken cancellationToken)
    {
        var mailbox = await _mailboxService.GetMailboxAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            mailboxId,
            cancellationToken);

        if (mailbox is null)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "mailbox_not_found",
                "Mailbox not found.");
        }

        return Results.Json(
            new ApiResponse<MailboxDto>(mailbox),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("{mailboxId:guid}/folders")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FolderDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> GetFolders(Guid mailboxId, CancellationToken cancellationToken)
    {
        var folders = await _mailboxService.GetFoldersAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            mailboxId,
            cancellationToken);

        return Results.Json(
            new ApiResponse<IReadOnlyList<FolderDto>>(folders),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }
}
