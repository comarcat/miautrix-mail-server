using System.ComponentModel.DataAnnotations;

namespace Miautrix.Mail.Web.Contracts;

/// <summary>
/// Payload for the mail-flow rule simulator. The sample message's sender, recipient
/// and subject are required: a request that omits any of them must be rejected with
/// 422 and field-level details, never a 200 carrying a failure.
/// </summary>
public sealed class SimulateRuleRequest
{
    [Required]
    public SimulateSampleMessage? SampleMessage { get; set; }
}

public sealed class SimulateSampleMessage
{
    [Required]
    public string? Sender { get; set; }

    [Required]
    public string? Recipient { get; set; }

    [Required]
    public string? Subject { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new();
    public bool HasAttachment { get; set; }
    public double? SpamScore { get; set; }
}
