using System.ComponentModel;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli.Commands;

public sealed class QueueRetrySettings : CommandSettings
{
    [CommandOption("-i|--id <ID>")]
    [Description("The queue item ID to retry immediately")]
    public Guid Id { get; set; }
}

public sealed class QueueRetryCommand : AsyncCommand<QueueRetrySettings>
{
    private readonly AppDbContext _db;

    public QueueRetryCommand(AppDbContext db)
    {
        _db = db;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, QueueRetrySettings settings, CancellationToken cancellationToken)
    {
        if (settings.Id == Guid.Empty)
        {
            AnsiConsole.MarkupLine("[bold red]Error:[/] --id parameter is required.");
            return 1;
        }

        var item = await _db.SmtpQueue.FirstOrDefaultAsync(q => q.Id == settings.Id, cancellationToken);
        if (item == null)
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] Queue item '{settings.Id}' not found.");
            return 1;
        }

        item.Status = "Pending";
        item.NextAttemptAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        AnsiConsole.MarkupLine($"[bold green]Success:[/] Queue item '{settings.Id}' scheduled for immediate retry.");
        return 0;
    }
}
