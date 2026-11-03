using Gondwana.Cli.Commands.New;

namespace Gondwana.Tests.Cli;

/// <summary>
/// Covers the Blazor scaffolding prerequisite decisions without modifying local SDK workloads.
/// </summary>
public sealed class BlazorScaffoldPrerequisitesTests
{
    /// <summary>
    /// Rejects missing, older, newer, or unsupported SDK versions before project creation.
    /// </summary>
    [Theory]
    [InlineData(null, -1)]
    [InlineData("8.0.414", 0)]
    [InlineData("9.0.305", 0)]
    [InlineData("10.0.99", 0)]
    [InlineData("10.0.100-preview.1", 0)]
    [InlineData("10.0.100-rc.1", 0)]
    [InlineData("10.0.401-preview.2", 0)]
    [InlineData("11.0.100", 0)]
    [InlineData("not-a-version", 0)]
    public void RequiresNet10Sdk(string? version, int exitCode)
    {
        var result = BlazorScaffoldPrerequisites.Prepare(
            version, exitCode, skipWorkload: false,
            checkWorkload: () => throw new InvalidOperationException("Workload check should not run."),
            confirmInstall: () => throw new InvalidOperationException("Prompt should not run."),
            installWorkload: () => throw new InvalidOperationException("Install should not run."),
            canPrompt: true);

        Assert.False(result.CanCreate);
        Assert.False(result.WorkloadReady);
        Assert.Contains(".NET SDK 10", result.Message);
    }

    /// <summary>
    /// Does not prompt or install if a compatible SDK 10 workload is present.
    /// </summary>
    [Theory]
    [InlineData("10.0.100")]
    [InlineData("10.0.300/10.0.100")]
    [InlineData("10.0.401")]
    public void CompatibleWorkloadDoesNotInstall(string manifestVersion)
    {
        var checks = 0;
        var result = BlazorScaffoldPrerequisites.Prepare(
            "10.0.401", 0, skipWorkload: false,
            checkWorkload: () => { checks++; return new(true, manifestVersion, null); },
            confirmInstall: () => throw new InvalidOperationException("Prompt should not run."),
            installWorkload: () => throw new InvalidOperationException("Install should not run."),
            canPrompt: true);

        Assert.True(result.CanCreate);
        Assert.True(result.WorkloadReady);
        Assert.Equal(1, checks);
    }

    /// <summary>
    /// Installs a missing workload when accepted and verifies it again.
    /// </summary>
    [Fact]
    public void MissingWorkloadPromptsInstallsAndRechecks()
    {
        var checks = 0;
        var prompts = 0;
        var installs = 0;
        var result = BlazorScaffoldPrerequisites.Prepare(
            "10.0.401", 0, skipWorkload: false,
            checkWorkload: () => ++checks == 1 ? new(true, null, null) : new(true, "10.0.300", null),
            confirmInstall: () => { prompts++; return true; },
            installWorkload: () => { installs++; return 0; },
            canPrompt: true);

        Assert.True(result.CanCreate);
        Assert.True(result.WorkloadReady);
        Assert.Equal(2, checks);
        Assert.Equal(1, prompts);
        Assert.Equal(1, installs);
    }

    /// <summary>
    /// Offers installation when an installed workload belongs to an older SDK.
    /// </summary>
    [Fact]
    public void IncompatibleWorkloadPromptsForInstall()
    {
        var checks = 0;
        var result = BlazorScaffoldPrerequisites.Prepare(
            "10.0.401", 0, skipWorkload: false,
            checkWorkload: () => ++checks == 1 ? new(true, "8.0.400", null) : new(true, "10.0.300", null),
            confirmInstall: () => true,
            installWorkload: () => 0,
            canPrompt: true);

        Assert.True(result.CanCreate);
        Assert.True(result.WorkloadReady);
        Assert.Equal(2, checks);
    }

    /// <summary>
    /// Declining the optional install still creates a project but marks it not ready.
    /// </summary>
    [Fact]
    public void DecliningInstallationAllowsScaffoldingWithoutClaimingReadiness()
    {
        var result = BlazorScaffoldPrerequisites.Prepare(
            "10.0.401", 0, skipWorkload: false,
            checkWorkload: () => new(true, null, null),
            confirmInstall: () => false,
            installWorkload: () => throw new InvalidOperationException("Install should not run."),
            canPrompt: true);

        Assert.True(result.CanCreate);
        Assert.False(result.WorkloadReady);
        Assert.Contains("declined", result.Message);
    }

    /// <summary>
    /// A failed installation or failed post-install verification must not claim success.
    /// </summary>
    [Theory]
    [InlineData(1, "10.0.300")]
    [InlineData(0, "8.0.400")]
    [InlineData(0, null)]
    public void FailedInstallOrVerificationBlocksScaffolding(int installExitCode, string? afterVersion)
    {
        var checkCount = 0;
        var result = BlazorScaffoldPrerequisites.Prepare(
            "10.0.401", 0, skipWorkload: false,
            checkWorkload: () => ++checkCount == 1 ? new(true, null, null) : new(true, afterVersion, null),
            confirmInstall: () => true,
            installWorkload: () => installExitCode,
            canPrompt: true);

        Assert.False(result.CanCreate);
        Assert.False(result.WorkloadReady);
        Assert.Equal(installExitCode == 0 ? 2 : 1, checkCount);
    }

    /// <summary>
    /// Does not block project generation merely because the workload check was deliberately skipped.
    /// </summary>
    [Fact]
    public void SkipWorkloadNeverProbesPromptsOrInstalls()
    {
        var result = BlazorScaffoldPrerequisites.Prepare(
            "10.0.401", 0, skipWorkload: true,
            checkWorkload: () => throw new InvalidOperationException("Workload check should not run."),
            confirmInstall: () => throw new InvalidOperationException("Prompt should not run."),
            installWorkload: () => throw new InvalidOperationException("Install should not run."),
            canPrompt: false);

        Assert.True(result.CanCreate);
        Assert.False(result.WorkloadReady);
        Assert.Contains("--skip-workload", result.Message);
    }

    /// <summary>
    /// CI and redirected input cannot trigger an unexpected interactive installation.
    /// </summary>
    [Fact]
    public void MissingWorkloadInNonInteractiveShellGivesActionableError()
    {
        var result = BlazorScaffoldPrerequisites.Prepare(
            "10.0.401", 0, skipWorkload: false,
            checkWorkload: () => new(true, null, null),
            confirmInstall: () => throw new InvalidOperationException("Prompt should not run."),
            installWorkload: () => throw new InvalidOperationException("Install should not run."),
            canPrompt: false);

        Assert.False(result.CanCreate);
        Assert.Contains("--skip-workload", result.Message);
        Assert.Contains("dotnet workload install wasm-tools", result.Message);
    }

    /// <summary>
    /// Handles workload-list failures without offering an installation under an unknown SDK state.
    /// </summary>
    [Fact]
    public void FailedWorkloadProbeBlocksScaffolding()
    {
        var result = BlazorScaffoldPrerequisites.Prepare(
            "10.0.401", 0, skipWorkload: false,
            checkWorkload: () => new(false, null, "dotnet workload list failed."),
            confirmInstall: () => throw new InvalidOperationException("Prompt should not run."),
            installWorkload: () => throw new InvalidOperationException("Install should not run."),
            canPrompt: true);

        Assert.False(result.CanCreate);
        Assert.Contains("dotnet workload list failed.", result.Message);
    }

    /// <summary>
    /// Accepts the stable minimum SDK without requiring the optional workload probe.
    /// </summary>
    [Fact]
    public void StableMinimumSdkIsAccepted()
    {
        var result = BlazorScaffoldPrerequisites.Prepare(
            "10.0.100", 0, skipWorkload: true,
            checkWorkload: () => throw new InvalidOperationException("Workload check should not run."),
            confirmInstall: () => throw new InvalidOperationException("Prompt should not run."),
            installWorkload: () => throw new InvalidOperationException("Install should not run."),
            canPrompt: false);

        Assert.True(result.CanCreate);
    }

    /// <summary>
    /// When the output directory exists, its own global.json can select the SDK.
    /// </summary>
    [Fact]
    public void ExistingProjectOutputUsesItsOwnSdkScope()
    {
        var root = Path.Combine(Path.GetTempPath(), "GondwanaBlazorScope_" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "project");
        Directory.CreateDirectory(output);
        try
        {
            File.WriteAllText(Path.Combine(output, "global.json"), "{ \"sdk\": { \"version\": \"8.0.100\" } }");
            Assert.Equal(output, BlazorScaffoldPrerequisites.ResolveSdkWorkingDirectory(output));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Nonexistent nested output paths inherit the nearest existing ancestor's SDK scope.
    /// </summary>
    [Fact]
    public void NewNestedOutputUsesNearestExistingAncestorSdkScope()
    {
        var root = Path.Combine(Path.GetTempPath(), "GondwanaBlazorScope_" + Guid.NewGuid().ToString("N"));
        var parent = Path.Combine(root, "parent");
        Directory.CreateDirectory(parent);
        try
        {
            File.WriteAllText(Path.Combine(parent, "global.json"), "{ \"sdk\": { \"version\": \"8.0.100\" } }");
            var output = Path.Combine(parent, "nested", "MyGame");
            var relativeOutput = Path.GetRelativePath(Directory.GetCurrentDirectory(), output);

            Assert.Equal(parent, BlazorScaffoldPrerequisites.ResolveSdkWorkingDirectory(output));
            Assert.Equal(parent, BlazorScaffoldPrerequisites.ResolveSdkWorkingDirectory(relativeOutput));
            Assert.False(Directory.Exists(output)); // The preflight must not create directories.
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
