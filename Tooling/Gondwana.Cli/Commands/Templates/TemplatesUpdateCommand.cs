using Gondwana.Cli.Commands;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Templates;

internal sealed class TemplatesUpdateCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        return TemplatePackageHelper.UpdateInstalledTemplates();
    }
}
