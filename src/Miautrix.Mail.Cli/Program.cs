using Miautrix.Mail.Cli.Commands;
using Miautrix.Mail.Cli.Infrastructure;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Infrastructure.Backup;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();

        var connectionString = Environment.GetEnvironmentVariable("MIAUTRIX_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=10.11.1.52;Port=5432;Database=miautrix-mail-dev;Username=mmdb-user;Password=Mi@usito#2026!";
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddSingleton<ISecurityEventSink, InMemorySecurityEventSink>();
        services.AddScoped<IPermissionRepository, EfPermissionRepository>();
        services.AddScoped<ITenantAuthorizationHelper, TenantAuthorizationHelper>();
        services.AddScoped<IBackupService, BackupService>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("miautrix-mail");

            // Root level backup and restore commands
            config.AddCommand<BackupCommand>("backup")
                .WithDescription("Create a full system backup archive");
            config.AddCommand<RestoreCommand>("restore")
                .WithDescription("Restore system state from a backup archive");

            config.AddBranch("mailbox", mailbox =>
            {
                mailbox.SetDescription("Manage mailboxes");
                mailbox.AddCommand<MailboxListCommand>("list")
                    .WithDescription("List mailboxes for a tenant");
                mailbox.AddCommand<MailboxCreateCommand>("create")
                    .WithDescription("Create a new mailbox for a tenant");
                mailbox.AddCommand<MailboxDeleteCommand>("delete")
                    .WithDescription("Delete a mailbox for a tenant");
            });

            config.AddBranch("domain", domain =>
            {
                domain.SetDescription("Manage domains");
                domain.AddCommand<DomainListCommand>("list")
                    .WithDescription("List domains for a tenant");
            });

            config.AddBranch("queue", queue =>
            {
                queue.SetDescription("Manage outbound mail queue");
                queue.AddCommand<QueueListCommand>("list")
                    .WithDescription("List items in the outbound SMTP queue");
                queue.AddCommand<QueueRetryCommand>("retry")
                    .WithDescription("Retry delivering a queue item");
            });

            config.AddBranch("quarantine", quarantine =>
            {
                quarantine.SetDescription("Manage quarantined messages");
                quarantine.AddCommand<QuarantineListCommand>("list")
                    .WithDescription("List quarantined messages");
                quarantine.AddCommand<QuarantineReleaseCommand>("release")
                    .WithDescription("Release a quarantined message for delivery");
            });

            config.AddBranch("system", sys =>
            {
                sys.SetDescription("System management and diagnostics");
                sys.AddCommand<SystemInfoCommand>("info")
                    .WithDescription("Show system information and version");
                sys.AddCommand<BackupCommand>("backup")
                    .WithDescription("Create a full system backup archive");
                sys.AddCommand<RestoreCommand>("restore")
                    .WithDescription("Restore system state from a backup archive");
            });
        });

        return await app.RunAsync(args);
    }
}
