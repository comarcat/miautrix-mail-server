namespace Miautrix.Mail.Web.Contracts;

/// <summary>
/// Error envelope. A failing request gets a failing status code and a body of
/// this shape — never a 200 carrying <c>{"success": false}</c>.
/// </summary>
public sealed record ApiError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null,
    string? RequestId = null);
