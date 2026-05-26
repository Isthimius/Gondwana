using Gondwana.Cli.Commands;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Templates;

internal sealed class TemplatesInstallCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        return TemplatePackageHelper.EnsureInstalledOrUpdated();
    }
}
