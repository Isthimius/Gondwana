using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Assets;

internal sealed class AssetsValidateCommand : Command<AssetsListCommand.Settings>
{
    protected override int Execute(CommandContext context, AssetsListCommand.Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            if (settings.TypeFilter is not null) throw new ArgumentException("assets validate checks the entire bundle; --type is supported only by list, inspect, and unpack.");
            var errors = BundleHelper.ValidateContents(settings.File, settings.Password);
            foreach (var error in errors) AnsiConsole.MarkupLine($"[red]Fail:[/] {Markup.Escape(error)}");
            if (errors.Count == 0) AnsiConsole.WriteLine("OK: Bundle structure, integrity, paths, and tilesheet definitions validated.");
            return errors.Count == 0 ? 0 : 1;
        }
        catch (Exception ex) { return ProjectCommand.Fail(ex); }
    }
}

internal sealed class AssetsInspectCommand : Command<AssetsListCommand.Settings>
{
    protected override int Execute(CommandContext context, AssetsListCommand.Settings settings, CancellationToken cancellationToken) => AssetsListCommand.Display(settings, summary: true);
}
