using System;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
[Route("api/v1/mailboxes/{mailboxId:guid}/drafts")]
public sealed class DraftController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IRequestContextAccessor _context;

    public DraftController(IMessageService messageService, IRequestContextAccessor context)
    {
        _messageService = messageService;
        _context = context;
    }

    [HttpPost]
    public async Task<IResult> Save(Guid mailboxId, [FromBody] DraftMessageRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _messageService.UpsertDraftAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, request, cancellationToken);
        return result.Success
            ? Results.Json(new ApiResponse<DraftMessageResult>(result), ApiJson.Options, statusCode: StatusCodes.Status200OK)
            : ApiResults.Error(HttpContext, StatusCodes.Status400BadRequest, "draft_save_failed", result.Message);
    }

    [HttpPut("{draftId:guid}")]
    public async Task<IResult> Update(Guid mailboxId, Guid draftId, [FromBody] DraftMessageRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _messageService.UpsertDraftAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, request with { DraftId = draftId }, cancellationToken);
        return result.Success
            ? Results.Json(new ApiResponse<DraftMessageResult>(result), ApiJson.Options, statusCode: StatusCodes.Status200OK)
            : ApiResults.Error(HttpContext, StatusCodes.Status400BadRequest, "draft_save_failed", result.Message);
    }

    [HttpPost("{draftId:guid}/send")]
    public async Task<IResult> Send(Guid mailboxId, Guid draftId, [FromBody] DraftMessageRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _messageService.SendDraftAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, draftId, request, cancellationToken);
        return result.Success
            ? Results.Json(new ApiResponse<SendMessageResult>(result), ApiJson.Options, statusCode: StatusCodes.Status200OK)
            : ApiResults.Error(HttpContext, StatusCodes.Status400BadRequest, "send_failed", result.Message);
    }

    [HttpDelete("{draftId:guid}")]
    public async Task<IResult> Discard(Guid mailboxId, Guid draftId, CancellationToken cancellationToken = default)
    {
        var deleted = await _messageService.DiscardDraftAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, draftId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    [HttpPost("{draftId:guid}/attachments")]
    public async Task<IResult> UploadAttachments(
        Guid mailboxId,
        Guid draftId,
        [FromForm] IFormFileCollection files,
        CancellationToken cancellationToken = default)
    {
        if (files is null || files.Count == 0)
        {
            return Results.BadRequest();
        }

        var uploaded = new List<AttachmentDto>();
        foreach (var file in files)
        {
            if (file is null || file.Length <= 0) continue;

            await using var stream = file.OpenReadStream();

            var dto = await _messageService.UploadDraftAttachmentAsync(
                _context.CurrentTenantId,
                _context.CurrentUserId,
                mailboxId,
                draftId,
                new AttachmentUploadInput(
                    file.FileName,
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    file.Length,
                    stream),
                cancellationToken);

            if (dto is not null)
            {
                uploaded.Add(dto);
            }
        }

        return Results.Json(new ApiResponse<IReadOnlyList<AttachmentDto>>(uploaded), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpDelete("{draftId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IResult> DeleteAttachment(Guid mailboxId, Guid draftId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var deleted = await _messageService.DeleteDraftAttachmentAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            mailboxId,
            draftId,
            attachmentId,
            cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
