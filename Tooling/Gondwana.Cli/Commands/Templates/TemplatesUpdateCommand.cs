using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Templates;

internal sealed class TemplatesUpdateCommand : Command
{
    /// <inheritdoc/>
    public override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        return TemplatePackageHelper.UpdateInstalledTemplates();
    }
}
