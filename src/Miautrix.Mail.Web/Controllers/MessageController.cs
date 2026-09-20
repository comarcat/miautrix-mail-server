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
[Route("api/v1/mailboxes/{mailboxId:guid}/messages")]
public sealed class MessageController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IRequestContextAccessor _context;

    public MessageController(IMessageService messageService, IRequestContextAccessor context)
    {
        _messageService = messageService;
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MessageSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> List(
        Guid mailboxId,
        [FromQuery] Guid? folder_id = null,
        [FromQuery] string? folder_role = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? is_read = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new MessageListFilter(folder_id, folder_role, search, is_read, limit, cursor);
        var page = await _messageService.ListMessagesAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            mailboxId,
            filter,
            cancellationToken);

        var meta = new PaginationMeta(page.NextCursor, page.HasMore, page.TotalCount);
        return Results.Json(
            new ApiResponse<IReadOnlyList<MessageSummaryDto>>(page.Items, meta),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("{messageId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MessageDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(
        Guid mailboxId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var message = await _messageService.GetMessageAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            mailboxId,
            messageId,
            cancellationToken);

        if (message is null)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "message_not_found",
                "Message not found.");
        }

        return Results.Json(
            new ApiResponse<MessageDetailDto>(message),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpPost("{messageId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> MarkRead(
        Guid mailboxId,
        Guid messageId,
        [FromBody] MarkReadRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await _messageService.MarkReadAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            mailboxId,
            messageId,
            request.IsRead,
            cancellationToken);

        if (!updated)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "message_not_found",
                "Message not found.");
        }

        return Results.Json(
            new { success = true },
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpPost("{messageId:guid}/move")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Move(
        Guid mailboxId,
        Guid messageId,
        [FromBody] MoveMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var moved = await _messageService.MoveMessageAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            mailboxId,
            messageId,
            request.TargetFolderId,
            cancellationToken);

        if (!moved)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "move_failed",
                "Message or target folder not found.");
        }

        return Results.Json(
            new { success = true },
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpDelete("{messageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(
        Guid mailboxId,
        Guid messageId,
        [FromQuery] bool permanent = false,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _messageService.DeleteMessageAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            mailboxId,
            messageId,
            permanent,
            cancellationToken);

        if (!deleted)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "message_not_found",
                "Message not found.");
        }

        return Results.StatusCode(StatusCodes.Status204NoContent);
    }

    [HttpPost("send")]
    [ProducesResponseType(typeof(ApiResponse<SendMessageResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IResult> Send(
        Guid mailboxId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _messageService.SendMessageAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            mailboxId,
            request,
            cancellationToken);

        if (!result.Success)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "send_failed",
                result.Message);
        }

        return Results.Json(
            new ApiResponse<SendMessageResult>(result),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }
}

public sealed record MarkReadRequest(bool IsRead);
public sealed record MoveMessageRequest(Guid TargetFolderId);
