using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Templates;

internal sealed class TemplatesUpdateCommand : Command
{
    /// <inheritdoc/>
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        return TemplatePackageHelper.UpdateInstalledTemplates();
    }
}
