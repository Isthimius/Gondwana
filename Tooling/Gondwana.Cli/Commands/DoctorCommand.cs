using System.Runtime.InteropServices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands;

internal sealed class DoctorCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold]Gondwana Doctor[/]");
        AnsiConsole.WriteLine();

        var checks = new List<(string Label, Func<CheckResult> Check)>
        {
            (".NET SDK",           CheckDotNetSdk),
            ("Gondwana Templates", CheckGondwanaTemplates),
            ("SkiaSharp",          CheckSkiaSharp),
            ("SDL2",               CheckSdl2),
            ("LibVLC",             CheckLibVlc),
        };

        var results = new List<(string Label, CheckResult Result)>();

        int maxLabel = checks.Max(c => c.Label.Length);

        foreach (var (label, check) in checks)
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

        AnsiConsole.WriteLine();

        int issues = results.Count(r => r.Result.Status == CheckStatus.Fail);
        int warnings = results.Count(r => r.Result.Status == CheckStatus.Warning);

        if (issues == 0 && warnings == 0)
        {
            AnsiConsole.MarkupLine("[green]All checks passed.[/]");
            return 0;
        }

        if (issues > 0)
            AnsiConsole.MarkupLine($"[red]{issues} issue(s) found.[/]");

        if (warnings > 0)
            AnsiConsole.MarkupLine($"[yellow]{warnings} warning(s) found.[/]");

        return issues > 0 ? 1 : 0;
    }

    // ─── Individual checks ────────────────────────────────────────────────────

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
        var output = ProcessHelper.Run("dotnet", "new list gondwana --columns author,type", out int exitCode);

        if (exitCode != 0)
            return CheckResult.Fail($"Failed to query installed templates (exit code {exitCode}). Is the .NET SDK installed and functional?");

        if (output.Contains("gondwana-winforms", StringComparison.OrdinalIgnoreCase))
            return CheckResult.Ok("gondwana-winforms found");

        return CheckResult.Fail("Gondwana templates not installed. Run: gondwana templates install");
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

        return CheckResult.Fail("Native SkiaSharp library not found. Run: dotnet add package SkiaSharp.NativeAssets.Linux (or equivalent for your platform).");
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
        string[] candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["libvlc.dll"]
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? ["libvlc.dylib"]
                : ["libvlc.so", "libvlc.so.5"];

        foreach (var candidate in candidates)
        {
            if (NativeLibraryProbe.CanLoad(candidate))
                return CheckResult.Ok(candidate);
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
