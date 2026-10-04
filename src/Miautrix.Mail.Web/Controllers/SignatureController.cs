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
[Route("api/v1/mail/signatures")]
public sealed class SignatureController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IRequestContextAccessor _context;

    public SignatureController(IMessageService messageService, IRequestContextAccessor context)
    {
        _messageService = messageService;
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MailSignatureDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> List(CancellationToken cancellationToken = default)
    {
        var signatures = await _messageService.ListSignaturesAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);

        return Results.Json(new ApiResponse<IReadOnlyList<MailSignatureDto>>(signatures), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MailSignatureDto>), StatusCodes.Status200OK)]
    public async Task<IResult> Create([FromBody] MailSignatureRequest request, CancellationToken cancellationToken = default)
    {
        var signature = await _messageService.UpsertSignatureAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            request,
            null,
            cancellationToken);

        return Results.Json(new ApiResponse<MailSignatureDto>(signature), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpPut("{signatureId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MailSignatureDto>), StatusCodes.Status200OK)]
    public async Task<IResult> Update(Guid signatureId, [FromBody] MailSignatureRequest request, CancellationToken cancellationToken = default)
    {
        var signature = await _messageService.UpsertSignatureAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            request,
            signatureId,
            cancellationToken);

        return Results.Json(new ApiResponse<MailSignatureDto>(signature), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpDelete("{signatureId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(Guid signatureId, CancellationToken cancellationToken = default)
    {
        var deleted = await _messageService.DeleteSignatureAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            signatureId,
            cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }

    [HttpPut("{signatureId:guid}/default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> SetDefault(Guid signatureId, CancellationToken cancellationToken = default)
    {
        var updated = await _messageService.SetDefaultSignatureAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            signatureId,
            cancellationToken);

        return updated ? Results.NoContent() : Results.NotFound();
    }
}
