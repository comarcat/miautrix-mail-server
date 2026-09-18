using System.ComponentModel.DataAnnotations;

namespace Miautrix.Mail.Web.Contracts;

/// <summary>
/// Body for the queue retry endpoint. <see cref="Reason"/> is required so that a
/// mutating request with a missing field exercises the 422 field-level contract.
/// </summary>
public sealed class RetryQueueItemRequest
{
    [Required]
    public string? Reason { get; set; }
}
