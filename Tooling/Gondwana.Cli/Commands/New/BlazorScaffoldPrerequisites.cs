namespace Gondwana.Cli.Commands.New;

/// <summary>
/// A snapshot of the workload state reported by the selected .NET SDK.
/// </summary>
internal readonly record struct BlazorWorkloadProbe(bool Success, string? Version, string? Error);

/// <summary>
/// Outcome of checking the prerequisites for a new Blazor WebAssembly project.
/// </summary>
internal readonly record struct BlazorPrerequisiteResult(bool CanCreate, bool WorkloadReady, string Message);

/// <summary>
/// Coordinates SDK and workload preflight without coupling its decisions to processes or a console.
/// </summary>
internal static class BlazorScaffoldPrerequisites
{
    /// <summary>
    /// Validates the selected SDK and offers to install a missing or incompatible WASM workload.
    /// </summary>
    internal static BlazorPrerequisiteResult Prepare(
        string? sdkVersion,
        int sdkExitCode,
        bool skipWorkload,
        Func<BlazorWorkloadProbe> checkWorkload,
        Func<bool> confirmInstall,
        Func<int> installWorkload,
        bool canPrompt)
    {
        if (sdkExitCode != 0 || !IsSupportedSdk(sdkVersion))
        {
            var found = sdkExitCode == 0 && !string.IsNullOrWhiteSpace(sdkVersion)
                ? $" (found {sdkVersion.Trim()})"
                : string.Empty;
            return new(false, false,
                $"The Gondwana Blazor template requires .NET SDK 10.0.100 or later in the 10.0 line{found}. " +
                "Install a .NET 10 SDK and check 'dotnet --version' and any ancestor global.json.");
        }

        if (skipWorkload)
            return new(true, false,
                "WebAssembly workload check skipped (--skip-workload). Run 'dotnet workload install wasm-tools' before building if needed.");

        var workload = checkWorkload();
        if (!workload.Success)
            return new(false, false,
                $"{workload.Error ?? "Could not check installed .NET workloads."} " +
                "Check 'dotnet workload list', or use --skip-workload to scaffold without checking.");

        if (IsCompatibleWorkload(workload.Version))
            return new(true, true, $"Compatible wasm-tools workload installed ({workload.Version}).");

        if (!canPrompt)
            return new(false, false,
                "A compatible .NET 10 wasm-tools workload is not installed and input is non-interactive. " +
                "Run 'dotnet workload install wasm-tools' or use --skip-workload to generate the project without it.");

        if (!confirmInstall())
            return new(true, false,
                "WebAssembly workload installation declined. Run 'dotnet workload install wasm-tools' before building.");

        if (installWorkload() != 0)
            return new(false, false,
                "Could not install wasm-tools. Run 'dotnet workload install wasm-tools' manually, " +
                "or use --skip-workload to scaffold without it.");

        var afterInstall = checkWorkload();
        if (!afterInstall.Success || !IsCompatibleWorkload(afterInstall.Version))
            return new(false, false,
                "wasm-tools installation completed, but a compatible .NET 10 workload could not be verified. " +
                "Run 'dotnet workload list' and check the selected SDK.");

        return new(true, true, $"Compatible wasm-tools workload installed ({afterInstall.Version}).");
    }

    private static bool IsSupportedSdk(string? sdkVersion)
    {
        if (string.IsNullOrWhiteSpace(sdkVersion) ||
            !Version.TryParse(sdkVersion.Trim().Split('-')[0], out var parsed))
            return false;

        return parsed.Major == 10 && parsed.Minor == 0 && parsed.Build >= 100;
    }

    private static bool IsCompatibleWorkload(string? version)
    {
        if (string.IsNullOrWhiteSpace(version) ||
            !Version.TryParse(version.Split('/')[0].Split('-')[0], out var parsed))
            return false;

        return parsed.Major == 10 && parsed.Minor == 0;
    }
}
