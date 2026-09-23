using System.Text.Json;
using Miautrix.Mail.Application.Admin;
using Miautrix.Mail.Application.Auth;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Application.Queue;
using Miautrix.Mail.Application.Transport;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Infrastructure.Backup;
using Miautrix.Mail.Infrastructure.MailboxArchiving;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Smtp;
using Miautrix.Mail.Queue;
using Miautrix.Mail.Security;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Web;

/// <summary>
/// Entry point for the REST API host. Public so the integration test project can use
/// <c>WebApplicationFactory&lt;Program&gt;</c> to host the same pipeline against the
/// real PostgreSQL used by the rest of the test suite.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";
        }

        builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        var backupDirectory = Environment.GetEnvironmentVariable("MIAUTRIX_BACKUP_DIR");
        if (string.IsNullOrWhiteSpace(backupDirectory))
        {
            backupDirectory = "/opt/miautrix-mail/backups";
        }

        // Content-addressed blob store. Must resolve to the same directory the IMAP/SMTP
        // pipeline writes to, or exports and blob cleanup operate on the wrong files.
        var storageDirectory = Environment.GetEnvironmentVariable("MIAUTRIX_STORAGE_DIR");
        if (string.IsNullOrWhiteSpace(storageDirectory))
        {
            storageDirectory = Path.Combine(AppContext.BaseDirectory, "mail_data");
        }

        builder.Services.AddSingleton<ISecurityEventSink, InMemorySecurityEventSink>();
        builder.Services.AddSingleton<IBackupService, BackupService>();
        builder.Services.AddSingleton(new BackupOptions(backupDirectory));
        builder.Services.AddSingleton<IMailStorage>(new FileSystemMailStorage(storageDirectory));
        builder.Services.AddSingleton<IMailboxArchiveService, MailboxArchiveService>();

        // Inbound webhook shared secret. Deliberately not fatal when absent: the controller fails
        // closed (rejects every request), which keeps a deployment that has not set up Cloudflare
        // serving the REST API and port 25 as before.
        var inboundToken = Environment.GetEnvironmentVariable("MIAUTRIX_INBOUND_TOKEN");
        builder.Services.AddSingleton(new InboundWebhookOptions { Token = inboundToken });

        // The inbound webhook feeds the same handler chain the SMTP listeners use, so a
        // Cloudflare-relayed message is validated, queued and stored identically.
        builder.Services.AddSingleton<IRetryPolicy, ExponentialBackoffWithJitterRetryPolicy>();
        builder.Services.AddScoped<ISmtpQueueManager, SmtpQueueManager>();
        builder.Services.AddScoped<ISmtpDomainValidator, SmtpDomainValidator>();
        builder.Services.AddScoped<ISmtpInboundHandler, SmtpInboundHandler>();

        builder.Services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        builder.Services.AddSingleton<ITotpService, TotpService>();
        builder.Services.AddSingleton<ISessionManager, SessionManager>();
        builder.Services.AddSingleton<LockoutOptions>();
        builder.Services.AddScoped<IPermissionRepository, EfPermissionRepository>();
        builder.Services.AddScoped<ITenantAuthorizationHelper, TenantAuthorizationHelper>();
        builder.Services.AddScoped<IMailQueueService, MailQueueService>();
        builder.Services.AddScoped<IMailboxService, MailboxService>();
        builder.Services.AddScoped<IMessageService, MessageService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IAdminService, AdminService>();

        // Cloudflare verify probe. The web app never dispatches queued mail, so it registers only
        // the probe side of the transport, not IOutboundMailTransport.
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton(CloudflareEmailOptions.FromEnvironment());
        builder.Services.AddSingleton<CloudflareApiMailTransport>();
        builder.Services.AddSingleton<ICloudflareTransport>(
            sp => sp.GetRequiredService<CloudflareApiMailTransport>());

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IRequestContextAccessor, HeaderRequestContextAccessor>();
        builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                var json = options.JsonSerializerOptions;
                json.PropertyNamingPolicy = ApiJson.Options.PropertyNamingPolicy;
                json.DictionaryKeyPolicy = ApiJson.Options.DictionaryKeyPolicy;
                json.DefaultIgnoreCondition = ApiJson.Options.DefaultIgnoreCondition;
                json.PropertyNameCaseInsensitive = ApiJson.Options.PropertyNameCaseInsensitive;
                json.UnmappedMemberHandling = ApiJson.Options.UnmappedMemberHandling;
                json.Converters.Clear();
                foreach (var converter in ApiJson.Options.Converters)
                {
                    json.Converters.Add(converter);
                }
            });

        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var details = context.ModelState
                    .Where(kv => kv.Value is not null && kv.Value.Errors.Count > 0)
                    .ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

                var error = new ApiError(
                    "validation_failed",
                    "One or more required fields are missing or invalid.",
                    details,
                    context.HttpContext.TraceIdentifier);

                return new JsonResult(
                    new Dictionary<string, object> { ["error"] = error },
                    ApiJson.Options)
                {
                    StatusCode = StatusCodes.Status422UnprocessableEntity,
                };
            };
        });

        builder.Services.AddOpenApi("v1");

        var app = builder.Build();

        app.UseMiddleware<ApiExceptionMiddleware>();
        app.UseMiddleware<RequestIdMiddleware>();
        app.UseMiddleware<IdempotencyMiddleware>();

        app.MapControllers();
        app.MapOpenApi("/openapi/v1.json");

        app.Run();
    }
}
