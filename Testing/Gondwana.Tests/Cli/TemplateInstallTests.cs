using Gondwana.Cli.Commands;
using Gondwana.Cli.Commands.Templates;
using Spectre.Console.Cli;

namespace Gondwana.Tests.Cli;

/// <summary>
/// Regression coverage for CLI template installation version selection.
/// </summary>
public sealed class TemplateInstallTests
{
    /// <summary>
    /// Verifies explicit NuGet versions are accepted, while invalid/ranged inputs are rejected.
    /// </summary>
    [Theory]
    [InlineData("2.6.2-beta.1", true)]
    [InlineData("2.6.2", true)]
    [InlineData("2.6.2.27", true)]
    [InlineData("2.6.2-rc.10", true)]
    [InlineData("2.6.2+build.1", true)]
    [InlineData("2.6.2-beta.1+build.1", true)]
    [InlineData("2.6.2+.", false)]
    [InlineData("2.6.2+build..1", false)]
    [InlineData("2.6.2+.build", false)]
    [InlineData("2.6.2+build.", false)]
    [InlineData("2.6.2+", false)]
    [InlineData("", false)]
    [InlineData("latest", false)]
    [InlineData("2.6.*", false)]
    [InlineData("Gondwana.Templates@2.6.2", false)]
    [InlineData("2.6.2 --force", false)]
    public void ExactVersionValidation(string version, bool expected) =>
        Assert.Equal(expected, TemplatePackageHelper.IsValidVersion(version));

    /// <summary>
    /// Verifies prerelease selection follows SemVer precedence, not lexical ordering.
    /// </summary>
    [Fact]
    public void LatestAvailable_IncludesPrereleasesAndSortsNumericIdentifiers()
    {
        var versions = new[] { "2.6.1", "2.6.2-beta.9", "2.6.2-beta.10", "2.6.2-beta.2", "2.6.0" };
        Assert.Equal("2.6.2-beta.10", TemplatePackageHelper.LatestAvailableVersion(versions));
        Assert.Equal("2.6.2", TemplatePackageHelper.LatestAvailableVersion(
            [.. versions, "2.6.2"]));
        Assert.Equal("2.7.0", TemplatePackageHelper.LatestAvailableVersion(
            [.. versions, "2.7.0"]));
    }

    /// <summary>
    /// Verifies that earlier prerelease identifiers sort before later ones and that
    /// invalid or absent versions fail rather than selecting an arbitrary package.
    /// </summary>
    [Fact]
    public void LatestAvailable_HandlesIdentifiersAndMissingVersions()
    {
        Assert.Equal("2.6.2-rc.1", TemplatePackageHelper.LatestAvailableVersion(
            ["2.6.2-alpha.5", "2.6.2-beta.9", "2.6.2-rc.1"]));
        Assert.Equal("2.6.2-beta.1.1", TemplatePackageHelper.LatestAvailableVersion(
            ["2.6.2-beta.1", "2.6.2-beta.1.1"]));
        Assert.Throws<InvalidOperationException>(() =>
            TemplatePackageHelper.LatestAvailableVersion(["unknown", "latest"]));
    }

    /// <summary>
    /// Verifies ordinary installs retain their legacy behavior, while explicit versions
    /// use dotnet's version-pinned install syntax and allow replacing installed templates.
    /// </summary>
    [Fact]
    public void InstallArguments_PreserveDefaultAndPinExplicitVersions()
    {
        Assert.Equal(["new", "install", "Gondwana.Templates"],
            TemplatePackageHelper.InstallArguments());
        Assert.Equal(["new", "install", "Gondwana.Templates::2.6.2-beta.1", "--force"],
            TemplatePackageHelper.InstallArguments("2.6.2-beta.1"));
    }

    /// <summary>
    /// Verifies conflicting switches and malformed versions are rejected before
    /// an external dotnet process is started.
    /// </summary>
    [Theory]
    [InlineData("2.6.2-beta.1", true)]
    [InlineData("invalid", false)]
    public void InvalidSettings_ReturnFailureWithoutInstalling(string version, bool prerelease)
    {
        var app = new CommandApp();
        app.Configure(config => config.AddCommand<TemplatesInstallCommand>("install"));
        var args = prerelease
            ? new[] { "install", "--prerelease", "--version", version }
            : new[] { "install", "--version", version };
        Assert.Equal(1, app.Run(args));
    }
}
