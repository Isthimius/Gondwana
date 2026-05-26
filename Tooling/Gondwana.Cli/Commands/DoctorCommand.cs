using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands;

internal sealed class DoctorCommand : Command<DoctorCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--fix")]
        [Description("Automatically fix issues that can be resolved without manual steps.")]
        public bool Fix { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold]Gondwana Doctor[/]");
        AnsiConsole.WriteLine();

        var checks = new List<(string Label, Func<CheckResult> Check, Action? Fix)>
        {
            ("Git",                CheckGit,               null),
            (".NET SDK",           CheckDotNetSdk,         null),
            ("nbgv",               CheckNbgv,              null),
            ("Gondwana CLI",       CheckGondwanaCli,       FixGondwanaCli),
            ("Gondwana Templates", CheckGondwanaTemplates, FixGondwanaTemplates),
            ("wasm-tools",         CheckWasmTools,         FixWasmTools),
            ("git-cliff",          CheckGitCliff,          FixGitCliff),
            ("butler",             CheckButler,            FixButler),
            ("SkiaSharp",          CheckSkiaSharp,         null),
            ("SDL2",               CheckSdl2,              null),
            ("LibVLC",             CheckLibVlc,            null),
        };

        int maxLabel = checks.Max(c => c.Label.Length);

        var results = RunChecks(checks);
        PrintResults(results, maxLabel);

        AnsiConsole.WriteLine();

        int exitCode = PrintSummary(results, remaining: false);

        if (!settings.Fix)
            return exitCode;

        // Keep --fix useful even when checks pass by allowing selected tools
        // to be updated to the latest available versions. Some always-fix
        // checks apply cross-platform; others apply on Windows only.
        var alwaysFixLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Gondwana Templates",
        };
        var windowsOnlyAlwaysFixLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "git-cliff",
        };
        bool ShouldAlwaysFix(string label) =>
            alwaysFixLabels.Contains(label) ||
            (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && windowsOnlyAlwaysFixLabels.Contains(label));
        bool hasAlwaysFixes = checks.Any(c => c.Fix != null && ShouldAlwaysFix(c.Label));

        if (exitCode == 0 && !hasAlwaysFixes)
            return exitCode;

        var fixable = results
            .Zip(checks, (r, c) => (r.Label, r.Result, c.Fix))
            .Where(x => x.Fix != null &&
                        (x.Result.Status == CheckStatus.Fail || ShouldAlwaysFix(x.Label)))
            .ToList();

        AnsiConsole.WriteLine();

        if (fixable.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No automatic fixes available for the issues found.[/]");
            return exitCode;
        }

        AnsiConsole.MarkupLine("[bold]Applying fixes...[/]");
        AnsiConsole.WriteLine();

        foreach (var (label, _, fix) in fixable)
        {
            AnsiConsole.MarkupLine($"  Fixing [bold]{Markup.Escape(label)}[/]...");
            AnsiConsole.WriteLine();
            fix!();
            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine("[bold]Re-checking...[/]");
        AnsiConsole.WriteLine();

        var reResults = RunChecks(checks);
        PrintResults(reResults, maxLabel);

        AnsiConsole.WriteLine();

        return PrintSummary(reResults, remaining: true);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static List<(string Label, CheckResult Result)> RunChecks(
        List<(string Label, Func<CheckResult> Check, Action? Fix)> checks)
    {
        var results = new List<(string Label, CheckResult Result)>();

        foreach (var (label, check, _) in checks)
        {
            CheckResult result;
            try
            {
                result = check();
            }
            catch (Exception ex)
            {
                result = CheckResult.Fail($"Unexpected error: {ex.Message}");
            }

            results.Add((label, result));
        }

        return results;
    }

    private static int PrintSummary(List<(string Label, CheckResult Result)> results, bool remaining)
    {
        int issues = results.Count(r => r.Result.Status == CheckStatus.Fail);
        int warnings = results.Count(r => r.Result.Status == CheckStatus.Warning);

        if (issues == 0 && warnings == 0)
        {
            AnsiConsole.MarkupLine("[green]All checks passed.[/]");
            return 0;
        }

        string suffix = remaining ? " remaining." : " found.";

        if (issues > 0)
            AnsiConsole.MarkupLine($"[red]{issues} issue(s){suffix}[/]");

        if (warnings > 0)
            AnsiConsole.MarkupLine($"[yellow]{warnings} warning(s){suffix}[/]");

        return issues > 0 ? 1 : 0;
    }

    private static void PrintResults(List<(string Label, CheckResult Result)> results, int maxLabel)
    {
        foreach (var (label, result) in results)
        {
            var paddedLabel = label.PadRight(maxLabel);

            switch (result.Status)
            {
                case CheckStatus.Ok:
                    AnsiConsole.Markup($"  {paddedLabel}  [green]OK[/]");
                    if (!string.IsNullOrWhiteSpace(result.Detail))
                        AnsiConsole.Markup($"  [dim]{Markup.Escape(result.Detail)}[/]");
                    AnsiConsole.WriteLine();
                    break;

                case CheckStatus.Warning:
                    AnsiConsole.Markup($"  {paddedLabel}  [yellow]Warning[/]");
                    if (!string.IsNullOrWhiteSpace(result.Detail))
                        AnsiConsole.Markup($"  [dim]{Markup.Escape(result.Detail)}[/]");
                    AnsiConsole.WriteLine();
                    break;

                case CheckStatus.Fail:
                    AnsiConsole.Markup($"  {paddedLabel}  [red]Missing[/]");
                    if (!string.IsNullOrWhiteSpace(result.Detail))
                        AnsiConsole.Markup($"  [dim]{Markup.Escape(result.Detail)}[/]");
                    AnsiConsole.WriteLine();
                    break;

                case CheckStatus.Skip:
                    AnsiConsole.Markup($"  {paddedLabel}  [dim]Not checked[/]");
                    AnsiConsole.WriteLine();
                    break;
            }
        }
    }

    // ─── Fixes ────────────────────────────────────────────────────────────────

    private static void FixGondwanaCli()
    {
        ProcessHelper.RunLive("dotnet", ["tool", "install", "--global", "Gondwana.Cli"]);
    }

    private static void FixGondwanaTemplates()
    {
        TemplatePackageHelper.EnsureInstalledOrUpdated();
    }

    private static void FixWasmTools()
    {
        ProcessHelper.RunLive("dotnet", ["workload", "install", "wasm-tools"]);
    }

    private static void FixGitCliff()
    {
        FixViaWinget(
            toolName: "git-cliff",
            packageId: "orhun.git-cliff",
            installUrl: "https://git-cliff.org/",
            versionArgs: "--version");
    }

    private static void FixButler()
    {
        if (TryGetButlerFromKnownLocations(out var existingButlerPath))
        {
            var existingInstallDir = Path.GetDirectoryName(existingButlerPath)!;
            AddDirectoryToProcessPath(existingInstallDir);

            var existingOutput = ProcessHelper.Run(existingButlerPath, "--version", out var existingExitCode);
            var existingVersion = existingOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (existingExitCode == 0 && !string.IsNullOrWhiteSpace(existingVersion))
                AnsiConsole.MarkupLine($"[green]butler already installed: {Markup.Escape(existingVersion)}[/]");
            else
                AnsiConsole.MarkupLine($"[green]butler already installed at {Markup.Escape(existingButlerPath)}[/]");

            AnsiConsole.MarkupLine($"[yellow]Add '{Markup.Escape(existingInstallDir)}' to your PATH to use butler in future terminal sessions.[/]");
            return;
        }

        // Determine the broth CDN platform slug and executable name.
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported CPU architecture for butler installation: {RuntimeInformation.ProcessArchitecture}.")
        };

        string platform;
        string exe;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            platform = $"windows-{architecture}";
            exe = "butler.exe";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            platform = $"darwin-{architecture}";
            exe = "butler";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            platform = $"linux-{architecture}";
            exe = "butler";
        }
        else
        {
            throw new PlatformNotSupportedException(
                $"Unsupported operating system for butler installation: {RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture}).");
        }

        // Install to a user-local directory so no elevated permissions are needed.
        var installDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "itch", "butler")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".itch", "butler");

        var urls = new[]
        {
            $"https://broth.itch.ovh/butler/{platform}/LATEST/archive/default",
            $"https://broth.itch.zone/butler/{platform}/LATEST/archive/default",
        };
        var zipPath = Path.Combine(Path.GetTempPath(), $"butler-install-{Guid.NewGuid():N}.zip");

        try
        {
            Directory.CreateDirectory(installDir);

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

            Exception? lastDownloadError = null;
            foreach (var url in urls)
            {
                AnsiConsole.MarkupLine($"[dim]Downloading butler from {Markup.Escape(url)}...[/]");
                try
                {
                    try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { /* ignore cleanup errors */ }

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    using var response = http.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    using var responseStream = response.Content.ReadAsStreamAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
                        responseStream.CopyToAsync(fs, System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                    lastDownloadError = null;
                    break;
                }
                catch (Exception ex)
                {
                    lastDownloadError = ex;
                    AnsiConsole.MarkupLine($"[yellow]Download failed from {Markup.Escape(url)}: {Markup.Escape(ex.Message)}[/]");
                }
            }

            if (lastDownloadError is not null)
                throw new InvalidOperationException($"Could not download butler from any known source. Last error: {lastDownloadError.Message}", lastDownloadError);

            ZipFile.ExtractToDirectory(zipPath, installDir, overwriteFiles: true);

            var butlerExe = Path.Combine(installDir, exe);

            // Set executable bit on non-Windows platforms.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(butlerExe,
                    UnixFileMode.UserRead    | UnixFileMode.UserWrite  | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead   | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead   | UnixFileMode.OtherExecute);
            }

            // Add the install directory to the current process PATH so the re-check finds butler.
            AddDirectoryToProcessPath(installDir);

            AnsiConsole.MarkupLine($"[green]butler installed to {Markup.Escape(installDir)}.[/]");
            AnsiConsole.MarkupLine($"[yellow]Add '{Markup.Escape(installDir)}' to your PATH to use butler in future terminal sessions.[/]");
            AnsiConsole.MarkupLine("[dim]Run 'butler login' to authenticate with itch.io.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to install butler: {Markup.Escape(ex.Message)}[/]");
            AnsiConsole.MarkupLine("[dim]Install manually from https://itch.io/docs/butler/installing.html[/]");
        }
        finally
        {
            try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { /* ignore cleanup errors */ }
        }
    }

    private static void FixViaWinget(string toolName, string packageId, string installUrl, string versionArgs)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AnsiConsole.MarkupLine($"[yellow]Automatic install/update for {Markup.Escape(toolName)} is currently supported only on Windows via winget.[/]");
            AnsiConsole.MarkupLine($"[dim]Install from {Markup.Escape(installUrl)}.[/]");
            return;
        }

        var wingetOutput = ProcessHelper.Run("winget", "--version", out var wingetExit);
        if (wingetExit != 0 || string.IsNullOrWhiteSpace(wingetOutput))
        {
            AnsiConsole.MarkupLine($"[red]winget not found; cannot auto-install or auto-update {Markup.Escape(toolName)}.[/]");
            AnsiConsole.MarkupLine($"[dim]Install from {Markup.Escape(installUrl)}.[/]");
            return;
        }

        var toolOutput = ProcessHelper.Run(toolName, versionArgs, out var toolExit);
        if (toolExit == 0 && !string.IsNullOrWhiteSpace(toolOutput))
        {
            ProcessHelper.RunLive("winget", ["upgrade", "--id", packageId, "--exact", "--silent", "--accept-source-agreements", "--accept-package-agreements"]);
            return;
        }

        ProcessHelper.RunLive("winget", ["install", "--id", packageId, "--exact", "--silent", "--accept-source-agreements", "--accept-package-agreements"]);

        // Refresh PATH so the newly installed tool is visible to subsequent checks,
        // mirroring Setup-Gondwana-Dev.ps1's $env:PATH refresh after winget installs,
        // while preserving any entries that exist only in the current process.
        var processPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? string.Empty;
        var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? string.Empty;
        var userPath    = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)    ?? string.Empty;

        var mergedPathEntries = new List<string>();
        var seenPathEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pathEntry in new[] { processPath, machinePath, userPath }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .SelectMany(p => p.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            if (seenPathEntries.Add(pathEntry))
                mergedPathEntries.Add(pathEntry);
        }

        var refreshedPath = string.Join(";", mergedPathEntries);
        Environment.SetEnvironmentVariable("PATH", refreshedPath, EnvironmentVariableTarget.Process);
    }

    // ─── Individual checks ────────────────────────────────────────────────────

    private static CheckResult CheckGit()
    {
        var output = ProcessHelper.Run("git", "--version", out int exitCode);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            return CheckResult.Fail("git not found on PATH. Install Git from https://git-scm.com");

        return CheckResult.Ok(output.Trim());
    }

    private static CheckResult CheckNbgv()
    {
        // nbgv is a local .NET tool restored from .config/dotnet-tools.json.
        var output = ProcessHelper.Run("dotnet", "tool list", out int exitCode);
        if (exitCode != 0)
            return CheckResult.Fail($"dotnet tool list failed (exit {exitCode}).");

        if (!output.Contains("nbgv", StringComparison.OrdinalIgnoreCase))
            return CheckResult.Fail("nbgv local tool not found. Run: dotnet tool restore");

        // Extract version from the tool list line, e.g. "nbgv    3.9.50    nbgv"
        var line = output.Split('\n')
            .FirstOrDefault(l => l.Contains("nbgv", StringComparison.OrdinalIgnoreCase));
        var version = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);

        return CheckResult.Ok(version ?? "found");
    }

    private static CheckResult CheckGondwanaCli()
    {
        var output = ProcessHelper.Run("dotnet", "tool list -g", out int exitCode);
        if (exitCode != 0)
            return CheckResult.Fail($"dotnet tool list -g failed (exit {exitCode}).");

        if (!output.Contains("gondwana.cli", StringComparison.OrdinalIgnoreCase))
            return CheckResult.Fail("Gondwana.Cli global tool not installed. Run: dotnet tool install -g Gondwana.Cli");

        var line = output.Split('\n')
            .FirstOrDefault(l => l.Contains("gondwana.cli", StringComparison.OrdinalIgnoreCase));
        var version = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);

        return CheckResult.Ok(version ?? "found");
    }

    private static CheckResult CheckDotNetSdk()
    {
        var output = ProcessHelper.Run("dotnet", "--version", out int exitCode);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            return CheckResult.Fail("dotnet SDK not found on PATH.");

        var version = output.Trim();
        if (!Version.TryParse(version.Split('-')[0], out var parsed))
            return CheckResult.Ok(version);

        return parsed.Major < 8
            ? CheckResult.Warning($"{version} — Gondwana requires .NET 8 or later.")
            : CheckResult.Ok(version);
    }

    private static CheckResult CheckGondwanaTemplates()
    {
        var output = ProcessHelper.Run("dotnet", "new list gondwana", out int exitCode);

        if (exitCode != 0)
            return CheckResult.Fail($"Failed to query installed templates (exit code {exitCode}). Is the .NET SDK installed and functional?");

        bool hasWinForms = output.Contains("gondwana-winforms", StringComparison.OrdinalIgnoreCase);
        bool hasAvalonia = output.Contains("gondwana-avalonia", StringComparison.OrdinalIgnoreCase);
        bool hasWasm     = output.Contains("gondwana-wasm",     StringComparison.OrdinalIgnoreCase);

        const string templateNames = "gondwana-winforms, gondwana-avalonia, gondwana-wasm";
        var installedVersion = TemplatePackageHelper.GetInstalledVersion();

        if (hasWinForms && hasAvalonia && hasWasm)
            return CheckResult.Ok(string.IsNullOrWhiteSpace(installedVersion)
                ? $"{templateNames} found"
                : $"Gondwana.Templates {installedVersion} ({templateNames})");

        var found = new List<string>();
        if (hasWinForms) found.Add("gondwana-winforms");
        if (hasAvalonia) found.Add("gondwana-avalonia");
        if (hasWasm)     found.Add("gondwana-wasm");

        if (found.Count > 0)
            return CheckResult.Ok(string.IsNullOrWhiteSpace(installedVersion)
                ? string.Join(", ", found) + " found"
                : $"Gondwana.Templates {installedVersion} ({string.Join(", ", found)})");

        return CheckResult.Fail("Gondwana templates not installed. Run: gondwana templates install");
    }

    private static CheckResult CheckWasmTools()
    {
        var output = ProcessHelper.Run("dotnet", "workload list", out int exitCode);
        if (exitCode != 0)
            return CheckResult.Fail($"dotnet workload list failed (exit {exitCode}).");

        if (!output.Contains("wasm-tools", StringComparison.OrdinalIgnoreCase))
            return CheckResult.Fail("wasm-tools workload not installed. Run: dotnet workload install wasm-tools");

        var version = GetWorkloadManifestVersion(output, "wasm-tools");
        return CheckResult.Ok(string.IsNullOrWhiteSpace(version)
            ? "wasm-tools installed"
            : version);
    }

    private static CheckResult CheckGitCliff()
    {
        var output = ProcessHelper.Run("git-cliff", "--version", out int exitCode);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            return CheckResult.Fail("git-cliff not found on PATH. Install from https://git-cliff.org/ (Windows: winget install --id orhun.git-cliff).");

        var versionLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        return CheckResult.Ok(versionLine ?? "found");
    }

    private static CheckResult CheckButler()
    {
        var output = ProcessHelper.Run("butler", "--version", out int exitCode);
        if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
        {
            var versionLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            return CheckResult.Ok(versionLine ?? "found");
        }

        if (TryGetButlerFromKnownLocations(out var butlerPath))
        {
            var fileOutput = ProcessHelper.Run(butlerPath, "--version", out int fileExitCode);
            var versionLine = fileOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            return fileExitCode == 0 && !string.IsNullOrWhiteSpace(versionLine)
                ? CheckResult.Ok($"{versionLine} ({butlerPath})")
                : CheckResult.Ok($"installed at {butlerPath}");
        }

        return CheckResult.Fail("butler not found on PATH or standard install directories. Run: gondwana doctor --fix");
    }

    private static CheckResult CheckSkiaSharp()
    {
        // Probe for the native SkiaSharp library by attempting to load it directly.
        string[] candidates;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            candidates = ["libSkiaSharp.dll"];
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            candidates = ["libSkiaSharp.dylib"];
        else
            candidates = ["libSkiaSharp.so", "libSkiaSharp.so.0"];

        foreach (var candidate in candidates)
        {
            if (NativeLibraryProbe.CanLoad(candidate))
            {
                var version = GetLatestNuGetPackageVersion("skiasharp");
                return CheckResult.Ok(string.IsNullOrWhiteSpace(version)
                    ? candidate
                    : $"{version} ({candidate})");
            }
        }

        // SkiaSharp is typically installed via NuGet and bundled into the project output
        // at build time — its native DLL is never placed on the system PATH.
        // Check the NuGet global packages cache so that a NuGet install is recognised.
        var cachedVersion = GetLatestNuGetPackageVersion("skiasharp");
        if (!string.IsNullOrWhiteSpace(cachedVersion))
            return CheckResult.Ok($"{cachedVersion} (NuGet cache)");

        return CheckResult.Fail("SkiaSharp not found. Restore a project that references SkiaSharp, or run: dotnet add package SkiaSharp");
    }

    private static CheckResult CheckSdl2()
    {
        // Try to find SDL2 native library on the system.
        string[] candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["SDL2.dll"]
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? ["libSDL2.dylib", "libSDL2-2.0.dylib", "libSDL2-2.0.0.dylib"]
                : ["libSDL2.so", "libSDL2-2.0.so", "libSDL2-2.0.so.0"];

        foreach (var candidate in candidates)
        {
            if (NativeLibraryProbe.CanLoad(candidate))
            {
                // Gondwana.Input.SDL2 references the ppy.SDL2-CS package.
                var version = GetLatestNuGetPackageVersion("ppy.sdl2-cs");
                return CheckResult.Ok(string.IsNullOrWhiteSpace(version)
                    ? candidate
                    : $"{version} ({candidate})");
            }
        }

        return CheckResult.Fail("SDL2 native library not found. Required by Gondwana.Input.SDL2. Install from https://github.com/libsdl-org/SDL/releases if you need a system-wide runtime.");
    }

    private static CheckResult CheckLibVlc()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Check both the PATH-resolvable name and well-known VLC install directories,
            // since the VLC installer does not add itself to the system PATH by default.
            var windowsCandidates = new List<string> { "libvlc.dll" };

            foreach (var programFiles in new[]
            {
                Environment.GetEnvironmentVariable("ProgramFiles"),
                Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            })
            {
                if (string.IsNullOrEmpty(programFiles))
                    continue;

                try
                {
                    windowsCandidates.Add(Path.Combine(programFiles, "VideoLAN", "VLC", "libvlc.dll"));
                }
                catch (ArgumentException)
                {
                    // Skip if the environment variable contains invalid path characters.
                }
            }

            foreach (var candidate in windowsCandidates)
            {
                if (NativeLibraryProbe.CanLoad(candidate))
                {
                    var location = TryResolveNativeLibraryPath(candidate);
                    var version = TryGetNativeLibraryVersion(location)
                        ?? TryGetLibVlcRuntimeVersion(location ?? candidate);
                    return CheckResult.Ok(FormatNativeLibraryDetail(candidate, location, version));
                }
            }
        }
        else
        {
            string[] candidates = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? ["libvlc.dylib"]
                : ["libvlc.so", "libvlc.so.5"];

            foreach (var candidate in candidates)
            {
                if (NativeLibraryProbe.CanLoad(candidate))
                {
                    var location = TryResolveNativeLibraryPath(candidate);
                    var version = TryGetNativeLibraryVersion(location)
                        ?? TryGetLibVlcRuntimeVersion(location ?? candidate);
                    return CheckResult.Ok(FormatNativeLibraryDetail(candidate, location, version));
                }
            }
        }

        // LibVLC is optional; only needed if Gondwana.Video is used.
        return CheckResult.Skip();
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

            if (!string.Equals(parts[0], workloadId, StringComparison.OrdinalIgnoreCase))
                continue;

            return parts[1];
        }

        return null;
    }

    private static string? GetLatestNuGetPackageVersion(string packageId)
    {
        try
        {
            var packageDir = Path.Combine(GetNuGetPackagesPath(), packageId);
            if (!Directory.Exists(packageDir))
                return null;

            string? latestRawVersion = null;
            Version? latestParsedVersion = null;

            foreach (var directory in Directory.EnumerateDirectories(packageDir))
            {
                var rawVersion = Path.GetFileName(directory);
                if (string.IsNullOrWhiteSpace(rawVersion))
                    continue;

                var normalizedVersion = rawVersion.Split('-', 2)[0];
                if (string.IsNullOrWhiteSpace(normalizedVersion))
                    continue;
                if (!Version.TryParse(normalizedVersion, out var parsedVersion))
                {
                    latestRawVersion ??= rawVersion;
                    continue;
                }

                if (latestParsedVersion is null || parsedVersion > latestParsedVersion)
                {
                    latestParsedVersion = parsedVersion;
                    latestRawVersion = rawVersion;
                }
            }

            return latestRawVersion;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string GetNuGetPackagesPath() =>
        Environment.GetEnvironmentVariable("NUGET_PACKAGES")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages");

    private static string FormatNativeLibraryDetail(string fallbackName, string? location, string? version)
    {
        var preferredLocation = string.IsNullOrWhiteSpace(location) ? fallbackName : location;
        return string.IsNullOrWhiteSpace(version)
            ? preferredLocation
            : $"{version} ({preferredLocation})";
    }

    private static string? TryResolveNativeLibraryPath(string candidate)
    {
        if (Path.IsPathRooted(candidate) && File.Exists(candidate))
            return candidate;

        var fileName = Path.GetFileName(candidate);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        if (TryFindInDirectories(fileName, EnumeratePathDirectories(), out var pathLocation))
            return pathLocation;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            TryFindInDirectories(fileName, EnumerateCommonUnixLibraryDirectories(), out var unixLocation))
        {
            return unixLocation;
        }

        return null;
    }

    private static IEnumerable<string> EnumeratePathDirectories()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            yield break;

        foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = entry.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            yield return trimmed;
        }
    }

    private static IEnumerable<string> EnumerateCommonUnixLibraryDirectories()
    {
        yield return "/usr/lib";
        yield return "/usr/local/lib";
        yield return "/lib";
        yield return "/opt/homebrew/lib";
        yield return "/usr/lib/x86_64-linux-gnu";
        yield return "/usr/lib/aarch64-linux-gnu";
    }

    private static bool TryFindInDirectories(string fileName, IEnumerable<string> directories, out string path)
    {
        foreach (var directory in directories)
        {
            try
            {
                var fullPath = Path.Combine(directory, fileName);
                if (File.Exists(fullPath))
                {
                    path = fullPath;
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // Skip malformed directories in PATH/environment values.
            }
        }

        path = string.Empty;
        return false;
    }

    private static string? TryGetNativeLibraryVersion(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(path);
            var productVersion = versionInfo.ProductVersion?.Trim();
            if (!string.IsNullOrWhiteSpace(productVersion))
                return productVersion;

            var fileVersion = versionInfo.FileVersion?.Trim();
            if (!string.IsNullOrWhiteSpace(fileVersion))
                return fileVersion;
        }
        catch
        {
            // Version metadata is optional for native libraries on some platforms.
        }

        return null;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr LibVlcGetVersionDelegate();

    private static string? TryGetLibVlcRuntimeVersion(string libraryPathOrName)
    {
        if (string.IsNullOrWhiteSpace(libraryPathOrName))
            return null;

        IntPtr handle = IntPtr.Zero;
        try
        {
            if (!NativeLibrary.TryLoad(libraryPathOrName, out handle) || handle == IntPtr.Zero)
                return null;

            if (!NativeLibrary.TryGetExport(handle, "libvlc_get_version", out var export) || export == IntPtr.Zero)
                return null;

            var getVersion = Marshal.GetDelegateForFunctionPointer<LibVlcGetVersionDelegate>(export);
            var versionPtr = getVersion();
            var version = Marshal.PtrToStringAnsi(versionPtr)?.Trim();
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (handle != IntPtr.Zero)
                NativeLibrary.Free(handle);
        }
    }

    private static bool TryGetButlerFromKnownLocations(out string butlerPath)
    {
        foreach (var candidate in EnumerateKnownButlerPaths())
        {
            if (File.Exists(candidate))
            {
                butlerPath = candidate;
                return true;
            }
        }

        butlerPath = string.Empty;
        return false;
    }

    private static IEnumerable<string> EnumerateKnownButlerPaths()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
                yield return Path.Combine(localAppData, "itch", "butler", "butler.exe");

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
                yield return Path.Combine(userProfile, ".itch", "butler", "butler.exe");

            yield break;
        }

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userHome))
            yield return Path.Combine(userHome, ".itch", "butler", "butler");
    }

    private static void AddDirectoryToProcessPath(string directoryPath)
    {
        var processPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? string.Empty;

        static string NormalizePathEntry(string path)
        {
            var trimmed = path.Trim().Trim('"');
            if (trimmed.Length == 0)
                return string.Empty;
            try
            {
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
            }
            catch
            {
                return Path.TrimEndingDirectorySeparator(trimmed);
            }
        }

        var normalizedDirectoryPath = NormalizePathEntry(directoryPath);
        var pathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        bool alreadyInPath = processPath
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizePathEntry)
            .Any(p => p.Equals(normalizedDirectoryPath, pathComparison));

        if (!alreadyInPath)
        {
            Environment.SetEnvironmentVariable(
                "PATH",
                string.IsNullOrWhiteSpace(processPath)
                    ? directoryPath
                    : processPath + Path.PathSeparator + directoryPath,
                EnvironmentVariableTarget.Process);
        }
    }
}

internal enum CheckStatus { Ok, Warning, Fail, Skip }

internal sealed class CheckResult
{
    public CheckStatus Status { get; }
    public string? Detail { get; }

    private CheckResult(CheckStatus status, string? detail)
    {
        Status = status;
        Detail = detail;
    }

    public static CheckResult Ok(string? detail = null) => new(CheckStatus.Ok, detail);
    public static CheckResult Warning(string? detail = null) => new(CheckStatus.Warning, detail);
    public static CheckResult Fail(string? detail = null) => new(CheckStatus.Fail, detail);
    public static CheckResult Skip(string? detail = null) => new(CheckStatus.Skip, detail);
}
