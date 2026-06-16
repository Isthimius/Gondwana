using System.IO.Compression;
using System.Xml.Linq;

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

    public static bool TargetsFramework(string csprojPath, string targetFramework)
        => GetTargetFrameworks(csprojPath).Contains(targetFramework, StringComparer.OrdinalIgnoreCase);

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

    public static string? TryLocateAppBundle(string csprojPath, string configuration)
    {
        var projectDir = Path.GetDirectoryName(csprojPath)!;
        var appBundle = Path.Combine(projectDir, "bin", configuration, "net8.0-browser", "browser-wasm", "AppBundle");

        if (Directory.Exists(appBundle))
            return appBundle;

        var binDir = Path.Combine(projectDir, "bin");
        if (!Directory.Exists(binDir))
            return null;

        return Directory.GetDirectories(binDir, "AppBundle", SearchOption.AllDirectories)
            .OrderByDescending(path => Directory.GetLastWriteTimeUtc(path))
            .FirstOrDefault();
    }

    /// <summary>
    /// Locates the directory that should be the root for <c>dotnet-serve</c> after a
    /// <c>net8.0-browser</c> publish. Tries several layouts in order:
    /// <list type="number">
    ///   <item><description>Avalonia Browser 11.x publish: <c>index.html</c> at root of <c>bin/&lt;cfg&gt;/net8.0-browser/browser-wasm/publish/</c></description></item>
    ///   <item><description>Avalonia Browser 11.x publish (wwwroot sub-layout): <c>index.html</c> inside <c>browser-wasm/publish/wwwroot/</c> — serve root is still <c>publish/</c></description></item>
    ///   <item><description>Classic AppBundle: <c>bin/&lt;cfg&gt;/net8.0-browser/browser-wasm/AppBundle</c> (must contain <c>index.html</c>)</description></item>
    ///   <item><description>AppBundle with nested web root: <c>AppBundle/wwwroot</c> (must contain <c>index.html</c>)</description></item>
    ///   <item><description>SDK publish at <c>net8.0-browser/publish/</c>: <c>index.html</c> at root or one level inside</description></item>
    ///   <item><description>Fallback: any <c>AppBundle</c> directory under <c>bin/</c> that contains <c>index.html</c> (most recently written first)</description></item>
    ///   <item><description>Fallback: any directory under <c>bin/</c> that contains <c>index.html</c> (most recently written first)</description></item>
    /// </list>
    /// Returns <see langword="null"/> when nothing is found.
    /// </summary>
    public static string? TryLocateWasmServeRoot(string csprojPath, string configuration)
    {
        var projectDir = Path.GetDirectoryName(csprojPath)!;

        // 0. Avalonia Browser 11.x: dotnet.js lives in _framework/, not at the publish root.
        //    index.html is generated at the root of browser-wasm/publish/.
        var browserPublishDir = Path.Combine(projectDir, "bin", configuration, "net8.0-browser", "browser-wasm", "publish");
        if (Directory.Exists(browserPublishDir))
        {
            // (a) index.html at the publish root (Avalonia Browser 11.x default)
            if (File.Exists(Path.Combine(browserPublishDir, "index.html")))
                return browserPublishDir;

            // (b) index.html inside publish/wwwroot/ (custom/legacy layout); serve the
            //     publish root so that _framework/ and other siblings are reachable.
            if (File.Exists(Path.Combine(browserPublishDir, "wwwroot", "index.html")))
                return browserPublishDir;
        }

        // 1. Classic AppBundle layout
        var appBundle = Path.Combine(projectDir, "bin", configuration, "net8.0-browser", "browser-wasm", "AppBundle");
        if (Directory.Exists(appBundle) && File.Exists(Path.Combine(appBundle, "index.html")))
            return appBundle;

        // 1b. AppBundle with nested web root
        var appBundleWwwRoot = Path.Combine(appBundle, "wwwroot");
        if (Directory.Exists(appBundleWwwRoot) && File.Exists(Path.Combine(appBundleWwwRoot, "index.html")))
            return appBundleWwwRoot;

        // 2 + 3. Alternate SDK publish layout without browser-wasm/ intermediate dir
        var publishDir = Path.Combine(projectDir, "bin", configuration, "net8.0-browser", "publish");
        if (Directory.Exists(publishDir))
        {
            // 2. index.html at publish root
            if (File.Exists(Path.Combine(publishDir, "index.html")))
                return publishDir;

            // 3. index.html one level deeper (e.g. publish/wwwroot/)
            var nested = Directory.GetDirectories(publishDir)
                .FirstOrDefault(d => File.Exists(Path.Combine(d, "index.html")));
            if (nested is not null)
                return nested;
        }

        // 4. Broadest fallback: any AppBundle dir under bin/
        var binDir = Path.Combine(projectDir, "bin");
        if (!Directory.Exists(binDir))
            return null;

        var anyAppBundle = Directory.GetDirectories(binDir, "AppBundle", SearchOption.AllDirectories)
            .Where(d => File.Exists(Path.Combine(d, "index.html")))
            .OrderByDescending(d => File.GetLastWriteTimeUtc(Path.Combine(d, "index.html")))
            .FirstOrDefault();
        if (anyAppBundle is not null)
            return anyAppBundle;

        // 5. Any directory under bin/ that contains index.html (most recently written first)
        return Directory.GetFiles(binDir, "index.html", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Select(Path.GetDirectoryName)
            .OfType<string>()
            .FirstOrDefault();
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

    internal static string? TryLocateBlazorPublishRoot(string csprojPath, string configuration)
    {
        var projectDir = Path.GetDirectoryName(csprojPath)!;
        var wwwroot = Path.Combine(projectDir, "bin", configuration, "net8.0", "publish", "wwwroot");
        
        if (Directory.Exists(wwwroot) && File.Exists(Path.Combine(wwwroot, "index.html")))
            return wwwroot;
        
        // Fallback: search for wwwroot directories
        var binDir = Path.Combine(projectDir, "bin");
        if (Directory.Exists(binDir))
        {
            var candidates = Directory.GetDirectories(binDir, "wwwroot", SearchOption.AllDirectories)
                                      .Where(d => File.Exists(Path.Combine(d, "index.html")))
                                      .ToList();
            if (candidates.Count > 0)
                return candidates[0];
        }
        
        return null;
    }
}
