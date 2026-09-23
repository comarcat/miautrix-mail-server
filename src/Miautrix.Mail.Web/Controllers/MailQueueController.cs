using Miautrix.Mail.Application.Queue;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Miautrix.Mail.Web.Controllers;

/// <summary>
/// Transport over the SMTP queue. Thin by design: it parses the wire request into a
/// service call, then maps the service result back onto the wire. No business logic
/// lives here.
/// </summary>
[ApiController]
[Route("api/v1/mail/queue")]
public sealed class MailQueueController : ControllerBase
{
    private static readonly string[] WireStatuses = ["queued", "retrying", "dead_letter", "delivered"];

    private readonly IRequestContextAccessor _contextAccessor;
    private readonly IMailQueueService _queueService;

    public MailQueueController(IRequestContextAccessor contextAccessor, IMailQueueService queueService)
    {
        _contextAccessor = contextAccessor;
        _queueService = queueService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<QueueItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> List(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 20,
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "start_at")] string? startAt = null,
        [FromQuery(Name = "end_at")] string? endAt = null,
        [FromQuery] string? domain = null,
        CancellationToken cancellationToken = default)
    {
        QueueStatusFilter? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            statusFilter = status.ToLowerInvariant() switch
            {
                "queued" => QueueStatusFilter.Queued,
                "retrying" => QueueStatusFilter.Retrying,
                "dead_letter" => QueueStatusFilter.DeadLetter,
                "delivered" => QueueStatusFilter.Delivered,
                _ => null,
            };

            if (statusFilter is null)
            {
                return ApiResults.Error(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "validation_failed",
                    "Query parameter 'status' must be one of: queued, retrying, dead_letter, delivered.",
                    new Dictionary<string, string[]> { ["status"] = [$"Received '{status}'."] });
            }
        }

        if (!TryParseDate(startAt, out var parsedStartAt))
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "validation_failed",
                "Query parameter 'start_at' must be a valid date/time.",
                new Dictionary<string, string[]> { ["start_at"] = [$"Received '{startAt}'."] });
        }

        if (!TryParseDate(endAt, out var parsedEndAt))
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "validation_failed",
                "Query parameter 'end_at' must be a valid date/time.",
                new Dictionary<string, string[]> { ["end_at"] = [$"Received '{endAt}'."] });
        }

        var filter = new QueueFilter(statusFilter, search, limit, cursor, parsedStartAt, parsedEndAt, domain);
        var page = await _queueService.ListAsync(
            _contextAccessor.CurrentTenantId,
            _contextAccessor.CurrentUserId,
            filter,
            cancellationToken);

        var data = page.Items.Select(ToDto).ToList();
        var meta = new PaginationMeta(page.NextCursor, page.HasMore);
        return Results.Json(
            new ApiResponse<IReadOnlyList<QueueItemDto>>(data, meta),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(typeof(ApiResponse<QueueItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> Retry(Guid id, [FromBody] RetryQueueItemRequest request, CancellationToken cancellationToken)
    {
        var item = await _queueService.RetryAsync(
            _contextAccessor.CurrentTenantId,
            _contextAccessor.CurrentUserId,
            id,
            request.Reason ?? string.Empty,
            cancellationToken);

        return Results.Json(
            new ApiResponse<QueueItemDto>(ToDto(item)),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    public sealed class ReassignQueueItemRequest
    {
        [Required]
        public string? TargetMailboxAddress { get; set; }
    }

    [HttpPost("{id:guid}/reassign")]
    [ProducesResponseType(typeof(ApiResponse<QueueItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> Reassign(
        Guid id,
        [FromBody] ReassignQueueItemRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetMailboxAddress))
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "validation_failed",
                "Request body must include TargetMailboxAddress.",
                new Dictionary<string, string[]>
                {
                    ["TargetMailboxAddress"] = [$"Received '{request.TargetMailboxAddress}'."],
                });
        }

        var item = await _queueService.ReassignAsync(
            _contextAccessor.CurrentTenantId,
            _contextAccessor.CurrentUserId,
            id,
            request.TargetMailboxAddress!,
            cancellationToken);

        return Results.Json(
            new ApiResponse<QueueItemDto>(ToDto(item)),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _queueService.DeleteAsync(
            _contextAccessor.CurrentTenantId,
            _contextAccessor.CurrentUserId,
            id,
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status204NoContent);
    }

    private static bool TryParseDate(string? value, out DateTimeOffset? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(value, out var dto))
        {
            return false;
        }

        parsed = dto.ToUniversalTime();
        return true;
    }

    private static QueueItemDto ToDto(SmtpQueueItem item) => new(
        item.Id.ToString(),
        item.Id.ToString(),
        item.Sender,
        item.Recipient,
        System.Text.Encoding.UTF8.GetByteCount(item.RawMessage ?? string.Empty),
        ToWireStatus(item.Status),
        item.Attempts,
        item.NextAttemptAt,
        item.CreatedAt,
        item.LastError);

    private static string ToWireStatus(string domainStatus) => domainStatus switch
    {
        "Pending" => "queued",
        "Failed" or "Retrying" => "retrying",
        "DeadLetter" => "dead_letter",
        "Delivered" => "delivered",
        _ => "queued",
    };
}
