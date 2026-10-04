using System.Runtime.InteropServices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Miautrix.Mail.Cli.Commands;

public sealed class SystemInfoSettings : CommandSettings
{
}

public sealed class SystemInfoCommand : Command<SystemInfoSettings>
{
    protected override int Execute(CommandContext context, SystemInfoSettings settings, CancellationToken cancellationToken)
    {
        var panel = new Panel(
            new Rows(
                new Markup("[bold blue]Miautrix Mail Server CLI[/] - Administration & Management Tool"),
                new Text(""),
                new Markup($"[bold]Version:[/] 1.0.0-rc1"),
                new Markup($"[bold]Runtime:[/] .NET {Environment.Version}"),
                new Markup($"[bold]OS Platform:[/] {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})"),
                new Markup($"[bold]Machine Name:[/] {Environment.MachineName}"),
                new Markup($"[bold]UTC Time:[/] {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")
            )
        )
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader("[bold green]Miautrix Mail System Info[/]")
        };

        AnsiConsole.Write(panel);
        return 0;
    }
}
