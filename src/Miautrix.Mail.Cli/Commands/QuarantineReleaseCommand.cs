using System.ComponentModel;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli.Commands;

public sealed class QuarantineReleaseSettings : CommandSettings
{
    [CommandOption("-i|--id <ID>")]
    [Description("The quarantine item ID to release and deliver")]
    public Guid Id { get; set; }
}

public sealed class QuarantineReleaseCommand : AsyncCommand<QuarantineReleaseSettings>
{
    private readonly AppDbContext _db;

    public QuarantineReleaseCommand(AppDbContext db)
    {
        _db = db;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, QuarantineReleaseSettings settings, CancellationToken cancellationToken)
    {
        if (settings.Id == Guid.Empty)
        {
            AnsiConsole.MarkupLine("[bold red]Error:[/] --id parameter is required.");
            return 1;
        }

        var item = await _db.Quarantine.FirstOrDefaultAsync(q => q.Id == settings.Id, cancellationToken);
        if (item == null)
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] Quarantined message '{settings.Id}' not found.");
            return 1;
        }

        item.IsDelivered = true;
        item.ReleasedAt = DateTimeOffset.UtcNow;
        item.Status = "Released";
        await _db.SaveChangesAsync(cancellationToken);

        AnsiConsole.MarkupLine($"[bold green]Success:[/] Quarantined message '{settings.Id}' released for delivery.");
        return 0;
    }
}
