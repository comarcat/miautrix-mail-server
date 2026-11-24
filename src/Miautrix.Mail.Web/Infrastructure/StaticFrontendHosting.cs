using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Miautrix.Mail.Web.Infrastructure;

/// <summary>Optional equivalent of the nginx /admin and webmail-root static locations.</summary>
internal static class StaticFrontendHosting
{
    public static string? ValidateOptionalDirectory(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value)) return null;
        var fullPath = Path.GetFullPath(value);
        if (!Directory.Exists(fullPath))
            throw new InvalidOperationException($"{variableName} must name an existing directory.");
        return fullPath;
    }

    public static void Configure(WebApplication app, string? adminDirectory, string? webmailDirectory)
    {
        if (adminDirectory is not null)
        {
            var provider = new PhysicalFileProvider(adminDirectory);
            app.Map("/admin", branch =>
            {
                branch.Use(async (context, next) =>
                {
                    if (!context.Request.Path.HasValue)
                    {
                        context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
                        context.Response.Headers.Location = "/admin/";
                        return;
                    }
                    await next();
                });
                branch.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
                branch.UseStaticFiles(Options(provider));
                branch.Run(context => ServeSpaFallback(context, provider));
            });
        }

        if (webmailDirectory is not null)
        {
            var provider = new PhysicalFileProvider(webmailDirectory);
            app.MapWhen(context => !context.Request.Path.StartsWithSegments("/api") &&
                                   !context.Request.Path.StartsWithSegments("/openapi") &&
                                   !context.Request.Path.StartsWithSegments("/admin"), branch =>
            {
                branch.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
                branch.UseStaticFiles(Options(provider));
                branch.Run(context => ServeSpaFallback(context, provider));
            });
        }
    }

    private static StaticFileOptions Options(IFileProvider provider, string? requestPath = null) => new()
    {
        FileProvider = provider,
        RequestPath = requestPath ?? string.Empty,
        OnPrepareResponse = context =>
        {
            ApplySecurityHeaders(context.Context.Response);
            var file = context.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase);
            var hashed = System.Text.RegularExpressions.Regex.IsMatch(context.File.Name, "[.-][A-Za-z0-9_-]{8,}[.-]");
            context.Context.Response.Headers.CacheControl = file ? "no-cache" : hashed ? "public, max-age=31536000, immutable" : "public, max-age=3600";
        }
    };

    private static async Task ServeSpaFallback(HttpContext context, IFileProvider provider)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        var index = provider.GetFileInfo("index.html");
        if (!index.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplySecurityHeaders(context.Response);
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.ContentLength = index.Length;
        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await using var stream = index.CreateReadStream();
            await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
    }

    private static void ApplySecurityHeaders(HttpResponse response)
    {
        response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        response.Headers.TryAdd("X-Frame-Options", "DENY");
        response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        response.Headers.TryAdd("Content-Security-Policy",
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob: https:; font-src 'self' data:; connect-src 'self' https:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'");
    }
}
