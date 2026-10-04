using System.ComponentModel;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli.Commands;

public sealed class MailboxListSettings : CommandSettings
{
    [CommandOption("-t|--tenant <SLUG>")]
    [Description("The tenant slug to list mailboxes for")]
    public string Tenant { get; set; } = string.Empty;

    [CommandOption("-u|--user <USER_ID>")]
    [Description("Operator user ID (defaults to system/operator context)")]
    public Guid? UserId { get; set; }
}

public sealed class MailboxListCommand : AsyncCommand<MailboxListSettings>
{
    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _authHelper;

    public MailboxListCommand(AppDbContext db, ITenantAuthorizationHelper authHelper)
    {
        _db = db;
        _authHelper = authHelper;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, MailboxListSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Tenant))
        {
            AnsiConsole.MarkupLine("[bold red]Error:[/] --tenant parameter is required.");
            return 1;
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == settings.Tenant, cancellationToken);
        if (tenant == null)
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] Tenant '{Markup.Escape(settings.Tenant)}' not found.");
            return 1;
        }

        // Check permissions if an operator user is specified
        if (settings.UserId.HasValue)
        {
            try
            {
                _authHelper.AssertPermission(tenant.Id, settings.UserId.Value, "mailbox.read");
            }
            catch (Exception)
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] Access denied: insufficient permissions to list mailboxes.");
                return 1;
            }
        }

        var mailboxes = await _db.Mailboxes
            .Where(m => m.TenantId == tenant.Id)
            .OrderBy(m => m.Address)
            .ToListAsync(cancellationToken);

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[bold]Address[/]"));
        table.AddColumn(new TableColumn("[bold]Status[/]"));
        table.AddColumn(new TableColumn("[bold]Used Space[/]"));
        table.AddColumn(new TableColumn("[bold]Quota[/]"));
        table.AddColumn(new TableColumn("[bold]Created At[/]"));

        foreach (var mbx in mailboxes)
        {
            var usedMb = (mbx.UsedBytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
            var quotaMb = (mbx.QuotaBytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
            var status = mbx.IsActive ? "[green]Active[/]" : "[red]Disabled[/]";

            table.AddRow(
                Markup.Escape(mbx.Address),
                status,
                usedMb,
                quotaMb,
                mbx.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }

        AnsiConsole.MarkupLine($"[bold blue]Mailboxes for tenant:[/] [green]{Markup.Escape(tenant.Slug)}[/] ({mailboxes.Count} total)");
        AnsiConsole.Write(table);

        return 0;
    }
}
