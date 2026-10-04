using System.ComponentModel;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;
using DomainEntity = Miautrix.Mail.Domain.Domain;

namespace Miautrix.Mail.Cli.Commands;

public sealed class DomainListSettings : CommandSettings
{
    [CommandOption("-t|--tenant <SLUG>")]
    [Description("The tenant slug to list domains for")]
    public string Tenant { get; set; } = string.Empty;

    [CommandOption("-u|--user <USER_ID>")]
    [Description("Operator user ID")]
    public Guid? UserId { get; set; }
}

public sealed class DomainListCommand : AsyncCommand<DomainListSettings>
{
    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _authHelper;

    public DomainListCommand(AppDbContext db, ITenantAuthorizationHelper authHelper)
    {
        _db = db;
        _authHelper = authHelper;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, DomainListSettings settings, CancellationToken cancellationToken)
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

        if (settings.UserId.HasValue)
        {
            try
            {
                _authHelper.AssertPermission(tenant.Id, settings.UserId.Value, "domain.read");
            }
            catch (Exception)
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] Access denied: insufficient permissions to list domains.");
                return 1;
            }
        }

        var domains = await _db.Domains
            .Where(d => d.TenantId == tenant.Id)
            .ToListAsync(cancellationToken);

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[bold]Domain ID[/]"));
        table.AddColumn(new TableColumn("[bold]Created At[/]"));

        foreach (var domain in domains)
        {
            table.AddRow(
                Markup.Escape(domain.Id.ToString()),
                domain.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }

        AnsiConsole.MarkupLine($"[bold blue]Domains for tenant:[/] [green]{Markup.Escape(tenant.Slug)}[/] ({domains.Count} total)");
        AnsiConsole.Write(table);

        return 0;
    }
}
