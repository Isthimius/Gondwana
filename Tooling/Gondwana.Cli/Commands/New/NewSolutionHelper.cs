using Gondwana.Cli.Commands;
using Spectre.Console;

namespace Gondwana.Cli.Commands.New;

internal static class NewSolutionHelper
{
    public static void CreateHoldingSolution(string projectName, string projectDirectory)
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

        if (!File.Exists(projectPath))
        {
            var existingProjects = Directory.GetFiles(fullProjectDirectory, "*.csproj", SearchOption.TopDirectoryOnly);
            projectPath = existingProjects.Length switch
            {
                1 => existingProjects[0],
                _ => existingProjects
                    .FirstOrDefault(path => string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        projectName,
                        StringComparison.OrdinalIgnoreCase))
                    ?? string.Empty,
            };

            if (string.IsNullOrEmpty(projectPath))
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not locate project file to add in [dim]{Markup.Escape(fullProjectDirectory)}[/].");
                return;
            }
        }

        var existingSolutions = Directory
            .GetFiles(fullProjectDirectory, "*.sln", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName)
            .ToArray();

        string solutionPath;
        if (existingSolutions.Length == 0)
        {
            solutionPath = defaultSolutionPath;
        }
        else if (existingSolutions.Length == 1)
        {
            solutionPath = existingSolutions[0];
        }
        else
        {
            solutionPath = existingSolutions.FirstOrDefault(path => string.Equals(
                Path.GetFileNameWithoutExtension(path),
                projectName,
                StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

            if (string.IsNullOrEmpty(solutionPath))
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]Warning:[/] Multiple solution files were found in [dim]{Markup.Escape(fullProjectDirectory)}[/], but none matched [dim]{Markup.Escape(projectName)}[/]. Skipping automatic solution update.");
                return;
            }
        }

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
