using System.Xml.Linq;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands;

internal sealed class InfoCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        var csproj = FindCsproj(Directory.GetCurrentDirectory());

        if (csproj is null)
        {
            AnsiConsole.MarkupLine("[red]No .csproj file found in the current directory.[/]");
            return 1;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Load(csproj);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to read project file: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        var projectName = Path.GetFileNameWithoutExtension(csproj);
        var framework   = GetProperty(doc, "TargetFramework") ?? GetProperty(doc, "TargetFrameworks") ?? "unknown";
        var gondwanaVer = GetGondwanaVersion(doc);
        var adapters    = GetGondwanaAdapters(doc);
        var assetFiles  = FindAssetFiles(Path.GetDirectoryName(csproj)!);
        var host        = DetectHost(adapters);

        AnsiConsole.MarkupLine($"[bold]Project:[/]    {Markup.Escape(projectName)}");
        AnsiConsole.MarkupLine($"[bold]Framework:[/]  {Markup.Escape(framework)}");

        if (!string.IsNullOrWhiteSpace(host))
            AnsiConsole.MarkupLine($"[bold]Host:[/]       {Markup.Escape(host)}");

        AnsiConsole.MarkupLine($"[bold]Gondwana:[/]   {(gondwanaVer is not null ? Markup.Escape(gondwanaVer) : "[dim]not referenced[/]")}");

        if (adapters.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Adapters:[/]");
            foreach (var adapter in adapters)
                AnsiConsole.MarkupLine($"  [dim]-[/] {Markup.Escape(adapter)}");
        }

        if (assetFiles.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Assets:[/]");
            var projectDir = Path.GetDirectoryName(csproj)!;
            foreach (var assetFile in assetFiles)
            {
                var rel = Path.GetRelativePath(projectDir, assetFile);
                AnsiConsole.MarkupLine($"  [dim]-[/] {Markup.Escape(rel)}");
            }
        }

        return 0;
    }

    private static string? FindCsproj(string directory)
    {
        var files = Directory.GetFiles(directory, "*.csproj")
                             .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                             .ToArray();

        if (files.Length == 0)
            return null;

        if (files.Length == 1)
            return files[0];

        AnsiConsole.MarkupLine("[yellow]Multiple .csproj files found in the current directory; using the first one:[/]");
        foreach (var file in files)
        {
            AnsiConsole.MarkupLine($"  [dim]-[/] {Markup.Escape(Path.GetFileName(file))}");
        }

        return files[0];
    }

    private static string? GetProperty(XDocument doc, string name)
    {
        return doc.Descendants(name).FirstOrDefault()?.Value.Trim();
    }

    private static string? GetGondwanaVersion(XDocument doc)
    {
        // Look for a PackageReference to Gondwana (exact match, not sub-packages).
        return doc.Descendants("PackageReference")
                  .FirstOrDefault(e =>
                      string.Equals(e.Attribute("Include")?.Value, "Gondwana",
                                    StringComparison.OrdinalIgnoreCase))
                  ?.Attribute("Version")?.Value;
    }

    private static List<string> GetGondwanaAdapters(XDocument doc)
    {
        return doc.Descendants("PackageReference")
                  .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
                  .Where(name => name.StartsWith("Gondwana.", StringComparison.OrdinalIgnoreCase))
                  .OrderBy(name => name)
                  .ToList();
    }

    private static List<string> FindAssetFiles(string directory)
    {
        try
        {
            return Directory.GetFiles(directory, "*.gaf", SearchOption.AllDirectories)
                            .Concat(Directory.GetFiles(directory, "*.assets", SearchOption.AllDirectories))
                            .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string? DetectHost(List<string> adapters)
    {
        if (adapters.Any(a => a.Equals("Gondwana.WinForms", StringComparison.OrdinalIgnoreCase) ||
                               a.Equals("Gondwana.WinForms.Hosting", StringComparison.OrdinalIgnoreCase)))
            return "WinForms";

        if (adapters.Any(a => a.StartsWith("Gondwana.Avalonia", StringComparison.OrdinalIgnoreCase)))
            return "Avalonia";

        return null;
    }
}
