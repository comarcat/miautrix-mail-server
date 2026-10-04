using System.ComponentModel;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli.Commands;

public sealed class MailboxDeleteSettings : CommandSettings
{
    [CommandOption("-t|--tenant <SLUG>")]
    [Description("The tenant slug containing the mailbox")]
    public string Tenant { get; set; } = string.Empty;

    [CommandOption("-a|--address <ADDRESS>")]
    [Description("The email address of the mailbox to delete")]
    public string Address { get; set; } = string.Empty;

    [CommandOption("-u|--user <USER_ID>")]
    [Description("Operator user ID")]
    public Guid? UserId { get; set; }
}

public sealed class MailboxDeleteCommand : AsyncCommand<MailboxDeleteSettings>
{
    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _authHelper;

    public MailboxDeleteCommand(AppDbContext db, ITenantAuthorizationHelper authHelper)
    {
        _db = db;
        _authHelper = authHelper;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, MailboxDeleteSettings settings, CancellationToken cancellationToken)
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
                _authHelper.AssertPermission(tenant.Id, settings.UserId.Value, "mailbox.delete");
            }
            catch (Exception)
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] Access denied: insufficient permissions to delete mailbox.");
                return 1;
            }
        }

        var mailbox = await _db.Mailboxes.FirstOrDefaultAsync(m => m.TenantId == tenant.Id && m.Address == settings.Address, cancellationToken);
        if (mailbox == null)
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] Mailbox '{Markup.Escape(settings.Address)}' not found.");
            return 1;
        }

        _db.Mailboxes.Remove(mailbox);
        await _db.SaveChangesAsync(cancellationToken);

        AnsiConsole.MarkupLine($"[bold green]Success:[/] Mailbox '{Markup.Escape(settings.Address)}' deleted.");
        return 0;
    }
}
