using Spectre.Console;

namespace Gondwana.Cli.Commands.New;

internal static class NewSolutionHelper
{
    public static void TryCreateHoldingSolution(string projectName, string projectDirectory)
    {
        var fullProjectDirectory = Path.GetFullPath(projectDirectory);
        var projectPath = Path.Combine(fullProjectDirectory, $"{projectName}.csproj");
        var defaultSolutionPath = Path.Combine(fullProjectDirectory, $"{projectName}.sln");
        var createdNewSolution = false;

        if (!Directory.Exists(fullProjectDirectory))
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Expected project directory [dim]{Markup.Escape(fullProjectDirectory)}[/] was not found.");
            return;
        }

        var existingSolutions = Directory.GetFiles(fullProjectDirectory, "*.sln", SearchOption.TopDirectoryOnly);
        var solutionPath = existingSolutions.Length switch
        {
            0 => defaultSolutionPath,
            1 => existingSolutions[0],
            _ => existingSolutions
                .FirstOrDefault(path => string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    projectName,
                    StringComparison.OrdinalIgnoreCase))
                ?? defaultSolutionPath,
        };

        if (!File.Exists(solutionPath))
        {
            var slnCreateExit = ProcessHelper.RunLive("dotnet", ["new", "sln", "-n", projectName, "-o", fullProjectDirectory]);
            if (slnCreateExit != 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Created project, but could not create solution file [dim]{Markup.Escape(solutionPath)}[/].");
                return;
            }

            createdNewSolution = true;
        }

        var slnAddExit = ProcessHelper.RunLive("dotnet", ["sln", solutionPath, "add", projectPath]);
        if (slnAddExit != 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not add project to solution [dim]{Markup.Escape(solutionPath)}[/] automatically.");
            return;
        }

        if (createdNewSolution)
            AnsiConsole.MarkupLine($"[green]Solution '{Markup.Escape(Path.GetFileName(solutionPath))}' created and updated successfully.[/]");
        else
            AnsiConsole.MarkupLine($"[green]Project added to existing solution '{Markup.Escape(Path.GetFileName(solutionPath))}'.[/]");
    }
}
