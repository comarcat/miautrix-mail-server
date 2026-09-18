using Miautrix.Mail.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Miautrix.Mail.Web.Infrastructure;

/// <summary>
/// Global exception-to-error-envelope mapper. Sits early in the pipeline so that any
/// exception thrown downstream is translated into a JSON ApiError with the request id.
/// </summary>
public sealed class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ResourceNotFoundException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found", ex.Message);
        }
        catch (LastOwnerDemotionException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, "last_owner", ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation during request {RequestId}", context.TraceIdentifier);
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "bad_request", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during request {RequestId}", context.TraceIdentifier);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "server_error", "An unexpected error occurred.");
        }
    }

    private static Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        return ApiResults.Error(context, statusCode, code, message).ExecuteAsync(context);
    }
}
