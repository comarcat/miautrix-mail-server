using System.ComponentModel;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli.Commands;

public sealed class QueueListSettings : CommandSettings
{
    [CommandOption("-t|--tenant <SLUG>")]
    [Description("Optional tenant slug to filter queue items")]
    public string? Tenant { get; set; }

    [CommandOption("-s|--status <STATUS>")]
    [Description("Filter by queue status (Pending, InFlight, Delivered, Failed)")]
    public string? Status { get; set; }
}

public sealed class QueueListCommand : AsyncCommand<QueueListSettings>
{
    private readonly AppDbContext _db;

    public QueueListCommand(AppDbContext db)
    {
        _db = db;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, QueueListSettings settings, CancellationToken cancellationToken)
    {
        var query = _db.SmtpQueue.AsQueryable();

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

        if (!string.IsNullOrWhiteSpace(settings.Status))
        {
            query = query.Where(q => q.Status.ToLower() == settings.Status.ToLower());
        }

        var items = await query.OrderByDescending(q => q.CreatedAt).Take(50).ToListAsync(cancellationToken);

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[bold]Queue ID[/]"));
        table.AddColumn(new TableColumn("[bold]Sender[/]"));
        table.AddColumn(new TableColumn("[bold]Recipient[/]"));
        table.AddColumn(new TableColumn("[bold]Status[/]"));
        table.AddColumn(new TableColumn("[bold]Attempts[/]"));
        table.AddColumn(new TableColumn("[bold]Next Attempt[/]"));

        foreach (var item in items)
        {
            var statusStyle = item.Status switch
            {
                "Delivered" => "[green]Delivered[/]",
                "Pending" => "[yellow]Pending[/]",
                "InFlight" => "[blue]InFlight[/]",
                "Failed" => "[red]Failed[/]",
                _ => item.Status
            };

            table.AddRow(
                Markup.Escape(item.Id.ToString()),
                Markup.Escape(item.Sender),
                Markup.Escape(item.Recipient),
                statusStyle,
                item.Attempts.ToString(),
                item.NextAttemptAt.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }

        AnsiConsole.MarkupLine($"[bold blue]SMTP Outbound Queue[/] ({items.Count} items displayed)");
        AnsiConsole.Write(table);

        return 0;
    }
}
