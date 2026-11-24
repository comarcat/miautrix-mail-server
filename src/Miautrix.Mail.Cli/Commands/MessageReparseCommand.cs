using System.ComponentModel;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli.Commands;

public sealed class MessageReparseSettings : CommandSettings
{
    [CommandOption("--tenant <TENANT>")]
    [Description("Tenant GUID or 'all' (all-tenant mode cannot be combined with --user)")]
    public string Tenant { get; set; } = string.Empty;

    [CommandOption("--since <DATE>")]
    [Description("Only messages on or after this ISO date/time")]
    public DateTimeOffset? Since { get; set; }

    [CommandOption("--dry-run")]
    [Description("Report changes without rewriting message rows")]
    public bool DryRun { get; set; }

    [CommandOption("--user <USER_ID>")]
    [Description("Optional user GUID checked for mailbox.update permission; omit to trust the local host operator")]
    public Guid? UserId { get; set; }
}

public sealed class MessageReparseCommand : AsyncCommand<MessageReparseSettings>
{
    private readonly AppDbContext _db;
    private readonly MessageReparseService _service;

    public MessageReparseCommand(AppDbContext db, MessageReparseService service) { _db = db; _service = service; }

    protected override async Task<int> ExecuteAsync(CommandContext context, MessageReparseSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Tenant))
        {
            AnsiConsole.MarkupLine("[red]--tenant is required.[/]");
            return 1;
        }
        if (settings.Tenant.Equals("all", StringComparison.OrdinalIgnoreCase) && settings.UserId.HasValue)
        {
            AnsiConsole.MarkupLine("[red]--tenant all cannot be combined with --user.[/]");
            return 1;
        }

        var tenantIds = settings.Tenant.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(cancellationToken)
            : Guid.TryParse(settings.Tenant, out var tenantId) ? [tenantId] : [];
        if (tenantIds.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No matching tenant found; --tenant must be a tenant GUID or all.[/]");
            return 1;
        }

        foreach (var id in tenantIds)
        {
            try
            {
                var result = await _service.ReparseAsync(id, settings.UserId, settings.Since, settings.DryRun, cancellationToken);
                AnsiConsole.MarkupLine($"Tenant {id}: scanned={result.Scanned}, updated={result.Updated}, skipped={result.Skipped}, failed={result.Failed}, dry-run={result.DryRun}");
            }
            catch (Exception)
            {
                AnsiConsole.MarkupLine($"[red]Tenant {id}: authorization or reparse failed.[/]");
                return 1;
            }
        }
        return 0;
    }
}
