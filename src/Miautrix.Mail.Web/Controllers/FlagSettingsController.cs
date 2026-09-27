using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
[Route("api/v1/mailboxes/{mailboxId:guid}/settings/flags")]
public sealed class FlagSettingsController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IRequestContextAccessor _context;

    public FlagSettingsController(IMessageService messageService, IRequestContextAccessor context)
    {
        _messageService = messageService;
        _context = context;
    }

    [HttpPut("{color}/alert")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> SetAlertConfig(
        Guid mailboxId,
        string color,
        [FromBody] SetAlertConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var success = await _messageService.SetFlagAlertConfigAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            mailboxId,
            color,
            request.AlertConfigurationJson ?? "{}",
            cancellationToken);

        return success ? Results.NoContent() : Results.NotFound();
    }

    [HttpGet("alerts")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FlagAlertConfiguration>>), StatusCodes.Status200OK)]
    public async Task<IResult> GetAlertConfigs(
        Guid mailboxId,
        CancellationToken cancellationToken = default)
    {
        var configs = await _messageService.GetFlagAlertConfigsAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            mailboxId,
            cancellationToken);

        return Results.Json(
            new ApiResponse<IReadOnlyList<FlagAlertConfiguration>>(configs),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }
}
