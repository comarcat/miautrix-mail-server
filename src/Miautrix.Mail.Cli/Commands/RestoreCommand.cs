using System.ComponentModel;
using Miautrix.Mail.Infrastructure.Backup;
using Miautrix.Mail.Persistence;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli.Commands;

public sealed class RestoreSettings : CommandSettings
{
    [CommandOption("-a|--archive <PATH>")]
    [Description("Source backup archive (.zip) to restore from")]
    public string ArchivePath { get; set; } = string.Empty;
}

public sealed class RestoreCommand : AsyncCommand<RestoreSettings>
{
    private readonly AppDbContext _db;
    private readonly IBackupService _backupService;

    public RestoreCommand(AppDbContext db, IBackupService backupService)
    {
        _db = db;
        _backupService = backupService;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, RestoreSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ArchivePath))
        {
            AnsiConsole.MarkupLine("[bold red]Error:[/] --archive parameter is required.");
            return 1;
        }

        if (!File.Exists(settings.ArchivePath))
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] Backup archive '{Markup.Escape(settings.ArchivePath)}' not found.");
            return 1;
        }

        AnsiConsole.MarkupLine($"[bold blue]Starting Miautrix Mail Server restore...[/]");
        AnsiConsole.MarkupLine($"[grey]Source archive:[/] {Markup.Escape(settings.ArchivePath)}");

        try
        {
            var manifest = await _backupService.RestoreBackupAsync(settings.ArchivePath, _db, cancellationToken);

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn(new TableColumn("[bold]Table[/]"));
            table.AddColumn(new TableColumn("[bold]Restored Rows (Expected)[/]"));

            foreach (var (tableKey, count) in manifest.TableRowCounts)
            {
                table.AddRow(tableKey, count.ToString());
            }

            AnsiConsole.MarkupLine("[bold green]Restore completed successfully![/]");
            AnsiConsole.Write(table);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]Restore failed:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}
