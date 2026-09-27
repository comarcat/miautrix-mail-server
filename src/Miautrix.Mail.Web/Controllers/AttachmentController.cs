using System;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
[Route("api/v1/messages")]
public sealed class AttachmentController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IRequestContextAccessor _context;

    public AttachmentController(IMessageService messageService, IRequestContextAccessor context)
    {
        _messageService = messageService;
        _context = context;
    }

    [HttpGet("{messageId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    public async Task<IResult> Download(
        Guid messageId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var download = await _messageService.DownloadAttachmentAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            messageId,
            attachmentId,
            cancellationToken);

        if (download is null)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "attachment_not_found",
                "Attachment not found.");
        }

        return Results.File(download.ContentStream, download.ContentType, download.FileName);
    }
}
