using System.ComponentModel;
using Miautrix.Mail.Infrastructure.Backup;
using Miautrix.Mail.Persistence;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli.Commands;

public sealed class BackupSettings : CommandSettings
{
    [CommandOption("-o|--output <PATH>")]
    [Description("Destination file path for the backup archive (.zip)")]
    public string? OutputPath { get; set; }
}

public sealed class BackupCommand : AsyncCommand<BackupSettings>
{
    private readonly AppDbContext _db;
    private readonly IBackupService _backupService;

    public BackupCommand(AppDbContext db, IBackupService backupService)
    {
        _db = db;
        _backupService = backupService;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, BackupSettings settings, CancellationToken cancellationToken)
    {
        var output = string.IsNullOrWhiteSpace(settings.OutputPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), $"miautrix_backup_{DateTime.UtcNow:yyyyMMddHHmmss}.zip")
            : settings.OutputPath;

        AnsiConsole.MarkupLine($"[bold blue]Starting Miautrix Mail Server backup...[/]");
        AnsiConsole.MarkupLine($"[grey]Target archive:[/] {Markup.Escape(output)}");

        try
        {
            var manifest = await _backupService.CreateBackupAsync(_db, output, cancellationToken);

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn(new TableColumn("[bold]Table[/]"));
            table.AddColumn(new TableColumn("[bold]Exported Rows[/]"));

            foreach (var (tableKey, count) in manifest.TableRowCounts)
            {
                table.AddRow(tableKey, count.ToString());
            }

            AnsiConsole.MarkupLine("[bold green]Backup completed successfully![/]");
            AnsiConsole.Write(table);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]Backup failed:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}
