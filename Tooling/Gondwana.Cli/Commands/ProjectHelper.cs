using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Spectre.Console;

namespace Gondwana.Cli.Commands;

internal static class ProjectHelper
{
    public static bool TryResolveProject(string? projectOption, out string? csprojPath, out string? error)
    {
        var projectPath = projectOption ?? Directory.GetCurrentDirectory();

        if (File.Exists(projectPath) && projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            csprojPath = Path.GetFullPath(projectPath);
            error = null;
            return true;
        }

        if (Directory.Exists(projectPath))
        {
            var found = Directory.GetFiles(projectPath, "*.csproj");
            if (found.Length == 0)
            {
                csprojPath = null;
                error = "No .csproj found in the specified directory.";
                return false;
            }

            if (found.Length > 1)
            {
                csprojPath = null;
                error = "Multiple .csproj files found. Pass -p|--project <path-to.csproj> to specify one.";
                return false;
            }

            csprojPath = Path.GetFullPath(found[0]);
            error = null;
            return true;
        }

        csprojPath = null;
        error = $"Project path not found: {projectPath}";
        return false;
    }

    public static IReadOnlyList<string> GetTargetFrameworks(string csprojPath)
    {
        try
        {
            var document = XDocument.Load(csprojPath);
            var targetFramework = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetFramework")?.Value;
            var targetFrameworks = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetFrameworks")?.Value;
            var combined = string.Join(";", new[] { targetFramework, targetFrameworks }.Where(v => !string.IsNullOrWhiteSpace(v)));

            return combined.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch
        {
            return [];
        }
    }

    public static bool IsBlazorWebAssemblyProject(string csprojPath)
    {
        try
        {
            var document = XDocument.Load(csprojPath);
            var sdk = document.Root?.Attribute("Sdk")?.Value ?? string.Empty;
            if (sdk.Contains("BlazorWebAssembly", StringComparison.OrdinalIgnoreCase))
                return true;

            var packageReferences = document.Descendants()
                .Where(e => e.Name.LocalName == "PackageReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v));

            if (packageReferences.Any(v => string.Equals(v, "Gondwana.Blazor", StringComparison.OrdinalIgnoreCase)))
                return true;

            var projectReferences = document.Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => Path.GetFileNameWithoutExtension(v));

            return projectReferences.Any(v => string.Equals(v, "Gondwana.Blazor", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public static bool IsCurrentDirectoryBlazorWebAssemblyProject()
    {
        try
        {
            var projects = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
            return projects.Length == 1 && IsBlazorWebAssemblyProject(projects[0]);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryResolveBrowserFramework(string csprojPath, string? requestedFramework, out string? framework, out string? error)
    {
        var frameworks = GetTargetFrameworks(csprojPath);

        if (!string.IsNullOrWhiteSpace(requestedFramework))
        {
            if (frameworks.Count > 0 && !frameworks.Contains(requestedFramework, StringComparer.OrdinalIgnoreCase))
            {
                framework = null;
                error = $"Target framework '{requestedFramework}' is not declared by the project.";
                return false;
            }

            framework = requestedFramework;
            error = null;
            return true;
        }

        var browserFrameworks = frameworks
            .Where(f => f.Contains("browser", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (browserFrameworks.Length == 1)
        {
            framework = browserFrameworks[0];
            error = null;
            return true;
        }

        if (browserFrameworks.Length > 1)
        {
            framework = null;
            error = "Multiple browser target frameworks found. Pass -f|--framework to specify one.";
            return false;
        }

        if (IsBlazorWebAssemblyProject(csprojPath) && frameworks.Count == 1)
        {
            framework = frameworks[0];
            error = null;
            return true;
        }

        framework = null;
        error = "No browser target framework found. Use a Blazor WebAssembly project or pass -f|--framework explicitly.";
        return false;
    }

    public static bool TryResolveDesktopFramework(string csprojPath, string? requestedFramework, out string? framework, out string? error)
    {
        if (!string.IsNullOrWhiteSpace(requestedFramework))
        {
            if (requestedFramework.Contains("browser", StringComparison.OrdinalIgnoreCase))
            {
                framework = null;
                error = "The browser target should be published with 'gondwana publish blazor'.";
                return false;
            }

            framework = requestedFramework;
            error = null;
            return true;
        }

        var desktopFrameworks = GetTargetFrameworks(csprojPath)
            .Where(f => !f.Contains("browser", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (desktopFrameworks.Length == 0)
        {
            framework = null;
            error = "No desktop target framework found. Use 'gondwana publish blazor' for browser-only projects.";
            return false;
        }

        if (desktopFrameworks.Length > 1)
        {
            framework = null;
            error = "Multiple desktop target frameworks found. Pass -f|--framework to specify one.";
            return false;
        }

        framework = desktopFrameworks[0];
        error = null;
        return true;
    }

    public static string? TryLocatePublishDirectory(string csprojPath, string configuration, string framework, string? runtime)
    {
        var projectDir = Path.GetDirectoryName(csprojPath)!;
        var directPath = string.IsNullOrWhiteSpace(runtime)
            ? Path.Combine(projectDir, "bin", configuration, framework, "publish")
            : Path.Combine(projectDir, "bin", configuration, framework, runtime, "publish");

        if (Directory.Exists(directPath))
            return directPath;

        var binDir = Path.Combine(projectDir, "bin");
        if (!Directory.Exists(binDir))
            return null;

        return Directory.GetDirectories(binDir, "publish", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}{configuration}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}{framework}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => string.IsNullOrWhiteSpace(runtime) || path.Contains($"{Path.DirectorySeparatorChar}{runtime}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(path => Directory.GetLastWriteTimeUtc(path))
            .FirstOrDefault();
    }

    public static string CreateZipFromDirectoryContents(string sourceDirectory, string zipPath)
    {
        var fullSource = Path.GetFullPath(sourceDirectory);
        var fullZip = Path.GetFullPath(zipPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullZip)!);

        if (File.Exists(fullZip))
            File.Delete(fullZip);

        using var archive = ZipFile.Open(fullZip, ZipArchiveMode.Create);
        foreach (var file in Directory.GetFiles(fullSource, "*", SearchOption.AllDirectories))
        {
            var entryName = Path.GetRelativePath(fullSource, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
        }

        return fullZip;
    }

    public static bool TryGetInstalledWorkloadVersion(string workloadId, out string? version, out string? error)
    {
        var output = ProcessHelper.Run("dotnet", "workload list", out int exitCode);
        if (exitCode != 0)
        {
            version = null;
            error = $"dotnet workload list failed (exit {exitCode}).";
            return false;
        }

        version = GetWorkloadManifestVersion(output, workloadId);
        error = null;
        return true;
    }

    public static int EnsureBlazorWasmToolsInstalled(bool skipWorkload)
    {
        if (skipWorkload)
            return 0;

        if (!TryGetInstalledWorkloadVersion("wasm-tools", out var version, out var error))
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(error!)}[/]");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(version))
        {
            AnsiConsole.MarkupLine($"[dim]wasm-tools already installed ({Markup.Escape(version)}).[/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[dim]wasm-tools is not installed; installing workload...[/]");
        var workloadExit = ProcessHelper.RunLive("dotnet", "workload install wasm-tools");
        if (workloadExit != 0)
            AnsiConsole.MarkupLine("[red]dotnet workload install wasm-tools failed.[/]");

        return workloadExit;
    }

    public static int PublishBlazorProject(
        string csprojPath,
        string configuration,
        string framework,
        bool skipWorkload,
        string? baseHref,
        out string? wwwroot)
    {
        wwwroot = null;

        var workloadExit = EnsureBlazorWasmToolsInstalled(skipWorkload);
        if (workloadExit != 0)
            return workloadExit;

        AnsiConsole.MarkupLine($"[dim]Publishing {framework} in {configuration} configuration...[/]");
        var publishExit = ProcessHelper.RunLive("dotnet", ["publish", csprojPath, "-c", configuration, "-f", framework]);
        if (publishExit != 0)
        {
            AnsiConsole.MarkupLine("[red]dotnet publish failed.[/]");
            return publishExit;
        }

        wwwroot = TryLocateBlazorPublishRoot(csprojPath, configuration, framework);
        if (wwwroot is not null && !TryApplyBlazorBaseHref(wwwroot, baseHref, out var baseHrefError))
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(baseHrefError!)}[/]");
            return 1;
        }

        return 0;
    }

    public static string? TryGetBlazorPublishRoot(
        string csprojPath,
        string configuration,
        string framework,
        bool skipBuild,
        bool skipWorkload,
        string? baseHref,
        out int exitCode)
    {
        exitCode = 0;

        if (!skipBuild)
        {
            var publishExit = PublishBlazorProject(csprojPath, configuration, framework, skipWorkload, baseHref, out var wwwroot);
            exitCode = publishExit;
            return publishExit == 0 ? wwwroot : null;
        }

        var existing = TryLocateBlazorPublishRoot(csprojPath, configuration, framework);
        if (existing is not null && !TryApplyBlazorBaseHref(existing, baseHref, out var error))
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(error!)}[/]");
            exitCode = 1;
            return null;
        }

        return existing;
    }

    public static string? CreateBlazorItchPackage(
        string csprojPath,
        string configuration,
        string framework,
        bool skipBuild,
        bool skipWorkload,
        string? baseHref,
        string? outputPath,
        out int exitCode)
    {
        var wwwroot = TryGetBlazorPublishRoot(csprojPath, configuration, framework, skipBuild, skipWorkload, baseHref, out exitCode);
        if (exitCode != 0)
            return null;

        if (wwwroot is null || !Directory.Exists(wwwroot))
            return null;

        var projectName = Path.GetFileNameWithoutExtension(csprojPath);
        var defaultZipPath = Path.Combine(Path.GetDirectoryName(wwwroot)!, $"{projectName}-itch.zip");
        return CreateZipFromDirectoryContents(wwwroot, outputPath ?? defaultZipPath);
    }

    public static string NormalizeBaseHref(string baseHref)
    {
        var normalized = baseHref.Trim();
        if (normalized.Length == 0)
            return normalized;

        return normalized.EndsWith("/", StringComparison.Ordinal)
            ? normalized
            : normalized + "/";
    }

    public static bool TryApplyBlazorBaseHref(string wwwroot, string? baseHref, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(baseHref))
            return true;

        var normalized = NormalizeBaseHref(baseHref);
        if (normalized.Contains('"') || normalized.Contains('\''))
        {
            error = "--base-href cannot contain quote characters.";
            return false;
        }

        var indexPath = Path.Combine(wwwroot, "index.html");
        if (!File.Exists(indexPath))
        {
            error = $"Cannot apply --base-href because index.html was not found in '{wwwroot}'.";
            return false;
        }

        var html = File.ReadAllText(indexPath);
        var basePattern = new Regex("(<base\\s+href\\s*=\\s*[\\\"'])([^\\\"']*)([\\\"'])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!basePattern.IsMatch(html))
        {
            error = "Cannot apply --base-href because the published index.html does not contain a <base href=...> element.";
            return false;
        }

        html = basePattern.Replace(html, match => $"{match.Groups[1].Value}{normalized}{match.Groups[3].Value}", 1);
        File.WriteAllText(indexPath, html);
        AnsiConsole.MarkupLine($"[dim]Published base href: {Markup.Escape(normalized)}[/]");
        return true;
    }

    public static void MirrorDirectory(string sourceDirectory, string destinationDirectory)
    {
        var source = Path.GetFullPath(sourceDirectory);
        var destination = Path.GetFullPath(destinationDirectory);

        Directory.CreateDirectory(destination);

        foreach (var destFile in Directory.GetFiles(destination, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(destination, destFile);
            var sourceFile = Path.Combine(source, relative);
            if (!File.Exists(sourceFile))
                File.Delete(destFile);
        }

        foreach (var destDir in Directory.GetDirectories(destination, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            var relative = Path.GetRelativePath(destination, destDir);
            var sourceDir = Path.Combine(source, relative);
            if (!Directory.Exists(sourceDir))
                Directory.Delete(destDir, recursive: true);
        }

        foreach (var sourceDir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, sourceDir);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var sourceFile in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, sourceFile);
            var destinationFile = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(sourceFile, destinationFile, overwrite: true);
        }
    }

    internal static string? TryLocateBlazorPublishRoot(string csprojPath, string configuration, string? framework = null)
    {
        if (string.IsNullOrWhiteSpace(framework) &&
            !TryResolveBrowserFramework(csprojPath, null, out framework, out _))
        {
            return null;
        }

        var projectDir = Path.GetDirectoryName(csprojPath)!;
        var wwwroot = Path.Combine(projectDir, "bin", configuration, framework!, "publish", "wwwroot");
        if (Directory.Exists(wwwroot) && File.Exists(Path.Combine(wwwroot, "index.html")))
            return wwwroot;

        var binDir = Path.Combine(projectDir, "bin");
        if (!Directory.Exists(binDir))
            return null;

        return Directory.GetDirectories(binDir, "wwwroot", SearchOption.AllDirectories)
            .Where(d => File.Exists(Path.Combine(d, "index.html")))
            .Where(d => d.Contains($"{Path.DirectorySeparatorChar}{configuration}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(d => d.Contains($"{Path.DirectorySeparatorChar}{framework}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string? GetWorkloadManifestVersion(string workloadListOutput, string workloadId)
    {
        foreach (var rawLine in workloadListOutput.Replace("\r", string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            if (string.Equals(parts[0], workloadId, StringComparison.OrdinalIgnoreCase))
                return parts[1];
        }

        return null;
    }
}
