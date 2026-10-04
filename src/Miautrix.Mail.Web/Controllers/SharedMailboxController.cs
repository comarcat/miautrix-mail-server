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
[Route("api/v1/shared-mailboxes")]
public sealed class SharedMailboxController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IRequestContextAccessor _context;

    public SharedMailboxController(IAdminService adminService, IRequestContextAccessor context)
    {
        _adminService = adminService;
        _context = context;
    }

    [HttpGet]
    public async Task<IResult> List(CancellationToken cancellationToken)
    {
        var mailboxes = await _adminService.ListSharedMailboxesAsync(
            _context.CurrentTenantId, _context.CurrentUserId, cancellationToken);
        return Results.Json(new ApiResponse<IReadOnlyList<SharedMailboxDto>>(mailboxes), ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("{mailboxId:guid}")]
    public async Task<IResult> Get(Guid mailboxId, CancellationToken cancellationToken)
    {
        var mailbox = await _adminService.GetSharedMailboxAsync(
            _context.CurrentTenantId, _context.CurrentUserId, mailboxId, cancellationToken);
        if (mailbox is null)
            return ApiResults.Error(HttpContext, StatusCodes.Status404NotFound, "mailbox_not_found", "Shared mailbox not found.");

        return Results.Json(new ApiResponse<SharedMailboxDto>(mailbox), ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpPost]
    public async Task<IResult> Create([FromBody] CreateSharedMailboxRequest request, CancellationToken cancellationToken)
    {
        var mailbox = await _adminService.CreateSharedMailboxAsync(
            _context.CurrentTenantId, _context.CurrentUserId, request, cancellationToken);
        return Results.Json(new ApiResponse<SharedMailboxDto>(mailbox), ApiJson.Options,
            statusCode: StatusCodes.Status201Created);
    }

    [HttpPut("{mailboxId:guid}/delegates")]
    public async Task<IResult> ReplaceDelegates(
        Guid mailboxId,
        [FromBody] ReplaceMailboxDelegatesRequest request,
        CancellationToken cancellationToken)
    {
        var mailbox = await _adminService.UpdateSharedMailboxDelegatesAsync(
            _context.CurrentTenantId, _context.CurrentUserId, mailboxId,
            request.Delegates ?? Array.Empty<MailboxDelegateRequest>(), cancellationToken);
        return Results.Json(new ApiResponse<SharedMailboxDto>(mailbox), ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }
}

public sealed record ReplaceMailboxDelegatesRequest(IReadOnlyList<MailboxDelegateRequest>? Delegates);
