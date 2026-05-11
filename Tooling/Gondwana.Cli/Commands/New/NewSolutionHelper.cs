using Spectre.Console;

namespace Gondwana.Cli.Commands.New;

internal static class NewSolutionHelper
{
    public static void TryCreateHoldingSolution(string projectName, string projectDirectory)
    {
        var fullProjectDirectory = Path.GetFullPath(projectDirectory);
        var solutionPath = Path.Combine(fullProjectDirectory, $"{projectName}.sln");
        var projectPath = Path.Combine(fullProjectDirectory, $"{projectName}.csproj");

        var slnCreateExit = ProcessHelper.RunLive("dotnet", ["new", "sln", "-n", projectName, "-o", fullProjectDirectory]);
        if (slnCreateExit != 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Created project, but could not create solution file [dim]{Markup.Escape(solutionPath)}[/].");
            return;
        }

        var slnAddExit = ProcessHelper.RunLive("dotnet", ["sln", solutionPath, "add", projectPath]);
        if (slnAddExit != 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Solution created at [dim]{Markup.Escape(solutionPath)}[/], but project could not be added automatically.");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Solution '{Markup.Escape($"{projectName}.sln")}' created successfully.[/]");
    }
}
