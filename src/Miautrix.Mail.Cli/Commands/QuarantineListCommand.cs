using System.ComponentModel;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli.Commands;

public sealed class QuarantineListSettings : CommandSettings
{
    [CommandOption("-t|--tenant <SLUG>")]
    [Description("Optional tenant slug to filter quarantined items")]
    public string? Tenant { get; set; }
}

public sealed class QuarantineListCommand : AsyncCommand<QuarantineListSettings>
{
    private readonly AppDbContext _db;

    public QuarantineListCommand(AppDbContext db)
    {
        _db = db;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, QuarantineListSettings settings, CancellationToken cancellationToken)
    {
        var query = _db.Quarantine.AsQueryable();

        if (!string.IsNullOrWhiteSpace(settings.Tenant))
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == settings.Tenant, cancellationToken);
            if (tenant == null)
            {
                AnsiConsole.MarkupLine($"[bold red]Error:[/] Tenant '{Markup.Escape(settings.Tenant)}' not found.");
                return 1;
            }
            query = query.Where(q => q.TenantId == tenant.Id);
        }

        var items = await query.OrderByDescending(q => q.QuarantinedAt).Take(50).ToListAsync(cancellationToken);

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[bold]ID[/]"));
        table.AddColumn(new TableColumn("[bold]Sender[/]"));
        table.AddColumn(new TableColumn("[bold]Recipient[/]"));
        table.AddColumn(new TableColumn("[bold]Score/Threshold[/]"));
        table.AddColumn(new TableColumn("[bold]Status[/]"));
        table.AddColumn(new TableColumn("[bold]Quarantined At[/]"));

        foreach (var item in items)
        {
            table.AddRow(
                Markup.Escape(item.Id.ToString()),
                Markup.Escape(item.Sender),
                Markup.Escape(item.Recipient),
                $"{item.SpamScore:F1} / {item.Threshold:F1}",
                item.IsDelivered ? "[green]Released[/]" : "[red]Quarantined[/]",
                item.QuarantinedAt.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }

        AnsiConsole.MarkupLine($"[bold red]Quarantined Messages[/] ({items.Count} items displayed)");
        AnsiConsole.Write(table);

        return 0;
    }
}
