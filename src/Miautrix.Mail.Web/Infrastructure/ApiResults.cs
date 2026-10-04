using Miautrix.Mail.Web.Contracts;

namespace Miautrix.Mail.Web.Infrastructure;

/// <summary>
/// Error envelope writer. Produces the <c>{ "error": { ... } }</c> shape with the
/// request id stamped into every failure so a client can quote it back to ops.
/// </summary>
public static class ApiResults
{
    public static IResult Error(
        HttpContext httpContext,
        int statusCode,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? details = null)
    {
        var error = new ApiError(
            code,
            message,
            details,
            RequestId: httpContext.TraceIdentifier);

        return Results.Json(
            new Dictionary<string, object> { ["error"] = error },
            ApiJson.Options,
            statusCode: statusCode);
    }
}
