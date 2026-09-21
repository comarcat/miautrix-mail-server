using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Miautrix.Mail.Application.Transport;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Protocols.Smtp;
using Miautrix.Mail.Protocols.Imap;
using Miautrix.Mail.Queue;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Worker;

// ── Environment ────────────────────────────────────────────────────────────────

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                  ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                  ?? "Production";

var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);

// ── Required configuration — crash on missing in production ───────────────────

var connectionString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    if (isDevelopment)
    {
        connectionString = "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";
        Console.WriteLine("[Worker] MIAUTRIX_DB_CONNECTION not set — using development default.");
    }
    else
    {
        Console.Error.WriteLine("[Worker] FATAL: MIAUTRIX_DB_CONNECTION is required in production.");
        return 1;
    }
}

var storageDirectory = Environment.GetEnvironmentVariable("MIAUTRIX_STORAGE_DIR");
if (string.IsNullOrWhiteSpace(storageDirectory))
{
    storageDirectory = Path.Combine(AppContext.BaseDirectory, "mail_data");
    Console.WriteLine($"[Worker] MIAUTRIX_STORAGE_DIR not set — defaulting to {storageDirectory}");
}

var hostname = Environment.GetEnvironmentVariable("MIAUTRIX_HOSTNAME");
if (string.IsNullOrWhiteSpace(hostname))
{
    hostname = "mail.miautrix.tech";
}

// ── TLS certificate ───────────────────────────────────────────────────────────

X509Certificate2? tlsCert = null;

var tlsCertPath = Environment.GetEnvironmentVariable("MIAUTRIX_TLS_CERT_PATH");
var tlsKeyPath = Environment.GetEnvironmentVariable("MIAUTRIX_TLS_KEY_PATH");

if (!string.IsNullOrWhiteSpace(tlsCertPath) && !string.IsNullOrWhiteSpace(tlsKeyPath))
{
    try
    {
        tlsCert = X509Certificate2.CreateFromPemFile(tlsCertPath, tlsKeyPath);
        Console.WriteLine($"[Worker] TLS certificate loaded from {tlsCertPath}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Worker] Failed to load TLS certificate: {ex.Message}");
        if (!isDevelopment)
        {
            return 1;
        }
    }
}
else if (!isDevelopment)
{
    Console.Error.WriteLine(
        "[Worker] FATAL: MIAUTRIX_TLS_CERT_PATH and MIAUTRIX_TLS_KEY_PATH are required in production.");
    return 1;
}
else
{
    Console.WriteLine("[Worker] TLS cert not configured — SMTP/IMAP TLS listeners will be skipped.");
}

// ── Host ──────────────────────────────────────────────────────────────────────

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        if (isDevelopment)
        {
            logging.SetMinimumLevel(LogLevel.Debug);
        }
        else
        {
            logging.SetMinimumLevel(LogLevel.Information);
        }
    })
    .ConfigureServices(services =>
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString), ServiceLifetime.Scoped);

        // Storage
        services.AddSingleton<IMailStorage>(new FileSystemMailStorage(storageDirectory));

        // Queue + retry policy
        services.AddSingleton<IRetryPolicy, ExponentialBackoffWithJitterRetryPolicy>();
        services.AddScoped<ISmtpQueueManager, SmtpQueueManager>();

        // Outbound transports. Registered as a set and selected by Mode, so adding a provider is a
        // registration rather than a branch inside the dispatcher. Only "cloudflare" is present:
        // "local" has no delivery path yet, and its queued mail must keep behaving as it does today.
        services.AddHttpClient();
        services.AddSingleton(CloudflareEmailOptions.FromEnvironment());
        services.AddSingleton<CloudflareApiMailTransport>();
        services.AddSingleton<IOutboundMailTransport>(
            sp => sp.GetRequiredService<CloudflareApiMailTransport>());

        // Protocol infrastructure (backed by the DB)
        services.AddScoped<ISmtpDomainValidator, SmtpDomainValidator>();
        services.AddScoped<ISmtpAuthenticator, SmtpAuthenticator>();
        services.AddScoped<ISmtpSubmissionHandler, SmtpSubmissionHandler>();
        services.AddScoped<ISmtpInboundHandler, SmtpInboundHandler>();
        services.AddScoped<IImapAuthenticator, ImapAuthenticator>();

        // Identity
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();

        // Shared options
        services.AddSingleton(new WorkerHostOptions { Hostname = hostname });
        services.AddSingleton(new TlsCertificateProvider(tlsCert));

        // Hosted services — SMTP (three listener types)
        services.AddHostedService<SmtpSubmissionStartTlsListener>();
        services.AddHostedService<SmtpSubmissionImplicitTlsListener>();
        services.AddHostedService<SmtpInboundMxListener>();

        // Hosted services — IMAP
        services.AddHostedService<ImapListenerService>();

        // Hosted service — outbound delivery for domains on a non-local transport
        services.AddHostedService<OutboundQueueDispatcher>();
    })
    .Build();

await host.RunAsync();
return 0;
