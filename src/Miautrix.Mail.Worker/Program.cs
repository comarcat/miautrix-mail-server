using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Miautrix.Mail.AntiMalware;
using Miautrix.Mail.AntiSpam;
using Miautrix.Mail.Application.Transport;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Protocols.Smtp;
using Miautrix.Mail.Protocols.Imap;
using Miautrix.Mail.Queue;
using Miautrix.Mail.Storage;
using Miautrix.Mail.Application.Mail.Mime;
using Miautrix.Mail.Infrastructure.Mail;
using Miautrix.Mail.Infrastructure.Storage;
using Miautrix.Mail.Worker;

// ── Environment ────────────────────────────────────────────────────────────────

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                  ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                  ?? "Production";

var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
var antiMalwareOptions = AntiMalwareOptions.FromEnvironment(isDevelopment);

// ── Required configuration — crash on missing in production ───────────────────

var connectionString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("[Worker] FATAL: MIAUTRIX_DB_CONNECTION environment variable is required.");
    return 1;
}

var storageDirectory = StorageDirectoryResolver.Resolve(
    Environment.GetEnvironmentVariable("MIAUTRIX_STORAGE_DIR"), isDevelopment, AppContext.BaseDirectory);

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
    .UseWindowsService()
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        if (OperatingSystem.IsWindows())
        {
            logging.AddEventLog();
        }
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
        services.AddSingleton<ISecurityEventSink, InMemorySecurityEventSink>();

        // Storage
        services.AddSingleton<IMailStorage>(new FileSystemMailStorage(storageDirectory));
        services.AddSingleton<IMimeMessageParser, MimeKitMimeMessageParser>();
        services.AddSingleton<IImapMimeFormatter, MimeKitImapMimeFormatter>();

        // Queue + retry policy
        services.AddSingleton<IRetryPolicy, ExponentialBackoffWithJitterRetryPolicy>();
        services.AddSingleton(QueueRetryOptions.FromEnvironment());
        services.AddScoped<ISmtpQueueManager, SmtpQueueManager>();

        // Inbound filtering
        services.AddSingleton(antiMalwareOptions);
        services.AddSingleton<IAntiMalwareScanner, ClamAvScanner>();
        services.AddScoped<ISpamProvider, RuleBasedSpamProvider>();
        services.AddScoped<IQuarantineService, QuarantineService>();

        // Outbound transports, selected per sending domain.
        services.AddHttpClient(nameof(CloudflareApiMailTransport), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        services.AddSingleton(CloudflareEmailOptions.FromEnvironment());
        services.AddSingleton<CloudflareApiMailTransport>();
        services.AddSingleton<IOutboundMailTransport>(
            sp => sp.GetRequiredService<CloudflareApiMailTransport>());
        services.AddSingleton(DirectSmtpOptions.FromEnvironment());
        services.AddSingleton<IDnsResolver, DnsClientResolver>();
        services.AddHttpClient(nameof(MtaStsPolicyFetcher), client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<IMtaStsPolicyFetcher, MtaStsPolicyFetcher>();
        services.AddSingleton<IMtaStsPolicyProvider, CachedMtaStsPolicyProvider>();
        services.AddSingleton<DirectSmtpMailTransport>();
        services.AddSingleton<IOutboundMailTransport>(sp => sp.GetRequiredService<DirectSmtpMailTransport>());

        // Protocol infrastructure (backed by the DB)
        services.AddScoped<ISmtpDomainValidator, SmtpDomainValidator>();
        services.AddScoped<ISmtpAuthenticator, SmtpAuthenticator>();
        services.AddScoped<ISmtpSubmissionHandler, SmtpSubmissionHandler>();
        services.AddScoped<ISmtpInboundHandler, SmtpInboundHandler>();
        services.AddScoped<IImapAuthenticator, ImapAuthenticator>();
        services.AddScoped<IPermissionRepository, EfPermissionRepository>();
        services.AddScoped<ITenantAuthorizationHelper, TenantAuthorizationHelper>();
        services.AddScoped<IMailboxService, MailboxService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<ISenderAuthorizationService, SenderAuthorizationService>();
        services.AddScoped<IMessageUidAllocator, MessageUidAllocator>();

        // Identity
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<IDkimService, DkimService>();

        // Shared options
        services.AddSingleton(new WorkerHostOptions { Hostname = hostname });
        services.AddSingleton(new TlsCertificateProvider(tlsCert));

        // Hosted services — SMTP (three listener types)
        services.AddHostedService<SmtpSubmissionStartTlsListener>();
        services.AddHostedService<SmtpSubmissionImplicitTlsListener>();
        services.AddHostedService<SmtpInboundMxListener>();

        // Hosted services — IMAP
        services.AddHostedService<ImapListenerService>();

        // Hosted service — inbound processing for local storage
        services.AddHostedService<InboundQueueDispatcher>();
        // Hosted service — outbound delivery for domains on a non-local transport
        services.AddHostedService<OutboundQueueDispatcher>();
    })
    .Build();

await host.RunAsync();
return 0;
