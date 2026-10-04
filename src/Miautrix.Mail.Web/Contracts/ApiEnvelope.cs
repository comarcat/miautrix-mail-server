namespace Miautrix.Mail.Web.Contracts;

/// <summary>
/// Success envelope. Every successful API response is wrapped in this shape so
/// clients never have to branch on a <c>success</c> flag inside a 200 body.
/// </summary>
public sealed record ApiResponse<T>(
    T Data,
    PaginationMeta? Meta = null);

/// <summary>
/// Cursor pagination metadata, present on list endpoints only.
/// </summary>
public sealed record PaginationMeta(
    string? NextCursor,
    bool HasMore,
    int? TotalCount = null);
