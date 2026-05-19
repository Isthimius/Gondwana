using System.ComponentModel;
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
            ("butler",             CheckButler,            null),
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
        // to be updated to the latest available versions on Windows.
        var alwaysFixLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "git-cliff",
        };
        bool hasWindowsAlwaysFixes = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                                     checks.Any(c => c.Fix != null && alwaysFixLabels.Contains(c.Label));

        if (exitCode == 0 && !hasWindowsAlwaysFixes)
            return exitCode;

        var fixable = results
            .Zip(checks, (r, c) => (r.Label, r.Result, c.Fix))
            .Where(x => x.Fix != null &&
                        (x.Result.Status == CheckStatus.Fail ||
                         (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && alwaysFixLabels.Contains(x.Label))))
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
        ProcessHelper.RunLive("dotnet", "new install Gondwana.Templates");
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

        if (hasWinForms && hasAvalonia && hasWasm)
            return CheckResult.Ok("gondwana-winforms, gondwana-avalonia, gondwana-wasm found");

        var found = new List<string>();
        if (hasWinForms) found.Add("gondwana-winforms");
        if (hasAvalonia) found.Add("gondwana-avalonia");
        if (hasWasm)     found.Add("gondwana-wasm");

        if (found.Count > 0)
            return CheckResult.Ok(string.Join(", ", found) + " found");

        return CheckResult.Fail("Gondwana templates not installed. Run: gondwana templates install");
    }

    private static CheckResult CheckWasmTools()
    {
        var output = ProcessHelper.Run("dotnet", "workload list", out int exitCode);
        if (exitCode != 0)
            return CheckResult.Fail($"dotnet workload list failed (exit {exitCode}).");

        if (!output.Contains("wasm-tools", StringComparison.OrdinalIgnoreCase))
            return CheckResult.Fail("wasm-tools workload not installed. Run: dotnet workload install wasm-tools");

        return CheckResult.Ok("wasm-tools installed");
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
        if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            return CheckResult.Fail("butler not found on PATH. Install from https://itch.io/docs/butler/.");

        var versionLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        return CheckResult.Ok(versionLine ?? "found");
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
                return CheckResult.Ok(candidate);
        }

        // SkiaSharp is typically installed via NuGet and bundled into the project output
        // at build time — its native DLL is never placed on the system PATH.
        // Check the NuGet global packages cache so that a NuGet install is recognised.
        if (IsNuGetPackageCached("skiasharp"))
            return CheckResult.Ok("found in NuGet global cache");

        return CheckResult.Fail("SkiaSharp not found. Restore a project that references SkiaSharp, or run: dotnet add package SkiaSharp");
    }

    /// <summary>
    /// Returns true if at least one version of the given NuGet package exists in the
    /// global packages cache. <paramref name="packageId"/> must be lowercase — NuGet
    /// normalises package IDs to lowercase when writing to the cache on all platforms.
    /// </summary>
    private static bool IsNuGetPackageCached(string packageId)
    {
        try
        {
            // NUGET_PACKAGES env var overrides the default cache location.
            var nugetHome = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget", "packages");

            var packageDir = Path.Combine(nugetHome, packageId);
            return Directory.Exists(packageDir) && Directory.EnumerateDirectories(packageDir).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
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
                return CheckResult.Ok(candidate);
        }

        return CheckResult.Fail("SDL2 native library not found. Required by Gondwana.Input.SDL2.");
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
                    return CheckResult.Ok(candidate);
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
                    return CheckResult.Ok(candidate);
            }
        }

        // LibVLC is optional; only needed if Gondwana.Video is used.
        return CheckResult.Skip();
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
