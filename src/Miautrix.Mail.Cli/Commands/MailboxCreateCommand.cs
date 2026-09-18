using System.ComponentModel;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli.Commands;

public sealed class MailboxCreateSettings : CommandSettings
{
    [CommandOption("-t|--tenant <SLUG>")]
    [Description("The tenant slug to create the mailbox in")]
    public string Tenant { get; set; } = string.Empty;

    [CommandOption("-a|--address <ADDRESS>")]
    [Description("The email address for the new mailbox")]
    public string Address { get; set; } = string.Empty;

    [CommandOption("-q|--quota <BYTES>")]
    [Description("Mailbox quota in bytes (default: 10GB)")]
    public long QuotaBytes { get; set; } = 10L * 1024 * 1024 * 1024;

    [CommandOption("-u|--user <USER_ID>")]
    [Description("Operator user ID")]
    public Guid? UserId { get; set; }
}

public sealed class MailboxCreateCommand : AsyncCommand<MailboxCreateSettings>
{
    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _authHelper;

    public MailboxCreateCommand(AppDbContext db, ITenantAuthorizationHelper authHelper)
    {
        _db = db;
        _authHelper = authHelper;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, MailboxCreateSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Tenant) || string.IsNullOrWhiteSpace(settings.Address))
        {
            AnsiConsole.MarkupLine("[bold red]Error:[/] Both --tenant and --address parameters are required.");
            return 1;
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == settings.Tenant, cancellationToken);
        if (tenant == null)
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] Tenant '{Markup.Escape(settings.Tenant)}' not found.");
            return 1;
        }

        if (settings.UserId.HasValue)
        {
            try
            {
                _authHelper.AssertPermission(tenant.Id, settings.UserId.Value, "mailbox.create");
            }
            catch (Exception)
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] Access denied: insufficient permissions to create mailbox.");
                return 1;
            }
        }

        var exists = await _db.Mailboxes.AnyAsync(m => m.TenantId == tenant.Id && m.Address == settings.Address, cancellationToken);
        if (exists)
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] Mailbox '{Markup.Escape(settings.Address)}' already exists.");
            return 1;
        }

        var mailbox = new Mailbox
        {
            TenantId = tenant.Id,
            Address = settings.Address,
            QuotaBytes = settings.QuotaBytes,
            UsedBytes = 0,
            IsActive = true
        };

        _db.Mailboxes.Add(mailbox);
        await _db.SaveChangesAsync(cancellationToken);

        AnsiConsole.MarkupLine($"[bold green]Success:[/] Mailbox '{Markup.Escape(settings.Address)}' created for tenant '{Markup.Escape(tenant.Slug)}'.");
        return 0;
    }
}
