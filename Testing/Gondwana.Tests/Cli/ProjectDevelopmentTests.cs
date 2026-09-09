using System.Text;
using System.Xml.Linq;
using Gondwana.Cli.Commands;
using Spectre.Console.Cli;

namespace Gondwana.Tests.Cli;

[Collection("Global engine state")]
public sealed class ProjectDevelopmentTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "GondwanaCli_" + Guid.NewGuid().ToString("N"));
    public ProjectDevelopmentTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, recursive: true);

    private string Project(string items = "<PackageReference Include=\"Gondwana\" Version=\"2.5.2\" />", string properties = "")
    {
        var path = Path.Combine(root, "Game.csproj");
        File.WriteAllText(path, $"<Project Sdk=\"Microsoft.NET.Sdk\">\r\n  <!-- keep this -->\r\n  <PropertyGroup><TargetFramework>net8.0</TargetFramework>{properties}</PropertyGroup>\r\n  <ItemGroup>\r\n    {items}\r\n  </ItemGroup>\r\n</Project>\r\n", new UTF8Encoding(true));
        return path;
    }

    private static int Run(params string[] args)
    {
        var app = new CommandApp();
        app.Configure(c => { c.AddCommand<UpgradeCommand>("upgrade"); c.AddCommand<AddCommand>("add"); c.AddCommand<CheckCommand>("check"); });
        return app.Run(args);
    }

    [Fact]
    public void Resolution_AcceptsFileOrDirectory_RejectsMissingAndAmbiguous()
    {
        var path = Project();
        Assert.True(ProjectHelper.TryResolveProject(root, out var resolved, out _));
        Assert.Equal(path, resolved);
        Assert.True(ProjectHelper.TryResolveProject(path, out _, out _));
        File.WriteAllText(Path.Combine(root, "Other.csproj"), "<Project />");
        Assert.False(ProjectHelper.TryResolveProject(root, out _, out var error));
        Assert.Contains("Multiple", error);
        Assert.False(ProjectHelper.TryResolveProject(Path.Combine(root, "missing"), out _, out _));
    }

    [Fact]
    public void Check_DetectsVersionMismatch_AndFixRechecks()
    {
        var path = Project("<PackageReference Include=\"Gondwana\" Version=\"2.6.0\" /><PackageReference Include=\"Gondwana.Widgets\" Version=\"2.5.2\" />");
        Assert.Equal(1, Run("check", "-p", path));
        Assert.Contains(ProjectHealth.Inspect(new(path)), d => d.Label == "Package versions" && d.Status == "Fail");
        Assert.Equal(0, Run("check", "--fix", "-p", path));
        Assert.All(new ProjectPackages(path).Packages, p => Assert.Equal("2.6.0", p.Version));
    }

    [Fact]
    public void CheckFix_RefusesToDowngradeOrSelectOptionalHosting()
    {
        var path = Project("<PackageReference Include=\"Gondwana\" Version=\"2.5.2\" /><PackageReference Include=\"Gondwana.WinForms\" Version=\"2.6.0\" />");
        var before = File.ReadAllBytes(path);
        Assert.Equal(1, Run("check", "--fix", "-p", path));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Upgrade_DryRunLeavesAllBytesUnchanged_ThenUpdatesOnlyFamily()
    {
        var path = Project("<PackageReference Include=\"Gondwana\" Version=\"2.5.2\" /><PackageReference Include=\"Gondwana.Widgets\"><Version>2.5.2</Version></PackageReference><PackageReference Include=\"Other\" Version=\"1.0.0\" />");
        var before = File.ReadAllBytes(path);
        Assert.Equal(0, Run("upgrade", "--version", "2.6.0", "--dry-run", "-p", path));
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(0, Run("upgrade", "--version", "2.6.0", "-p", path));
        Assert.All(new ProjectPackages(path).Packages, p => Assert.Equal("2.6.0", p.Version));
        var text = File.ReadAllText(path);
        Assert.Contains("<!-- keep this -->", text);
        Assert.Contains("Include=\"Other\" Version=\"1.0.0\"", text);
        Assert.Contains("\r\n", text);
        Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, File.ReadAllBytes(path).Take(3));
        var updated = File.ReadAllBytes(path);
        Assert.Equal(0, Run("upgrade", "--version", "2.6.0", "-p", path));
        Assert.Equal(updated, File.ReadAllBytes(path));
    }

    [Fact]
    public void Upgrade_UpdatesSharedProperty_WithoutReplacingReferences()
    {
        var path = Project("<PackageReference Include=\"Gondwana\" Version=\"$(GondwanaVersion)\" /><PackageReference Include=\"Gondwana.Widgets\" Version=\"$(GondwanaVersion)\" />", "<GondwanaVersion>2.5.2</GondwanaVersion>");
        Assert.Equal(0, Run("upgrade", "--version", "2.6.0-preview.1", "-p", path));
        Assert.Contains("<GondwanaVersion>2.6.0-preview.1</GondwanaVersion>", File.ReadAllText(path));
        Assert.Contains("Version=\"$(GondwanaVersion)\"", File.ReadAllText(path));
    }

    [Fact]
    public void Upgrade_RespectsCentralVersions_AndOnlyReferencedPackages()
    {
        var central = Path.Combine(root, "Directory.Packages.props");
        File.WriteAllText(central, "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup><PackageVersion Include=\"Gondwana\" Version=\"2.5.2\" /><PackageVersion Include=\"Gondwana.Video\" Version=\"2.5.2\" /></ItemGroup></Project>");
        var path = Project("<PackageReference Include=\"Gondwana\" />");
        var originalProject = File.ReadAllBytes(path);
        var originalCentral = File.ReadAllBytes(central);
        Assert.Equal(0, Run("upgrade", "--version", "2.6.0", "--dry-run", "-p", path));
        Assert.Equal(originalCentral, File.ReadAllBytes(central));
        Assert.Equal(0, Run("upgrade", "--version", "2.6.0", "-p", path));
        Assert.Equal(originalProject, File.ReadAllBytes(path));
        Assert.Contains("Include=\"Gondwana.Video\" Version=\"2.5.2\"", File.ReadAllText(central));
        Assert.Equal("2.6.0", new ProjectPackages(path).Packages.Single().Version);
        Assert.Equal(0, Run("add", "widgets", "-p", path));
        Assert.Contains("Gondwana.Widgets", File.ReadAllText(central));
        Assert.All(XDocument.Load(path).Descendants("PackageReference"), p => Assert.Null(p.Attribute("Version")));
    }

    [Theory]
    [InlineData("<PackageReference Include=\"Gondwana\" Version=\"2.*\" />", "")]
    [InlineData("<PackageReference Include=\"Gondwana\" Version=\"2.5.2\" Condition=\"'$(X)' == 'true'\" />", "")]
    [InlineData("<PackageReference Include=\"Gondwana\" Version=\"$(V)\" /><PackageReference Include=\"Other\" Version=\"$(V)\" />", "<V>2.5.2</V>")]
    [InlineData("<PackageReference Include=\"Gondwana\" Version=\"2.5.2\" /><PackageReference Include=\"Gondwana\" Version=\"2.5.2\" />", "")]
    public void Upgrade_RejectsAmbiguityWithoutMutation(string items, string properties)
    {
        var path = Project(items, properties);
        var before = File.ReadAllBytes(path);
        Assert.Equal(1, Run("upgrade", "--version", "2.6.0", "-p", path));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Add_IsIdempotent_AndUsesExistingFamilyVersion()
    {
        var path = Project();
        Assert.Equal(0, Run("add", "widgets", "-p", path));
        Assert.Equal("2.5.2", new ProjectPackages(path).Packages.Single(p => p.Name == "Gondwana.Widgets").Version);
        var before = File.ReadAllBytes(path);
        Assert.Equal(0, Run("add", "widgets", "-p", path));
        Assert.Equal(0, Run("add", "audio", "-p", path));
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(1, Run("add", "not-a-feature", "-p", path));
        Assert.Equal(1, Run("add", "hosting", "-p", path));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Add_FailsWithoutGondwana()
    {
        var path = Project("");
        Assert.Equal(1, Run("add", "widgets", "-p", path));
    }

    [Theory]
    [InlineData("WinForms")]
    [InlineData("Avalonia")]
    [InlineData("Blazor")]
    public void AddHosting_UsesActualAdapter(string host)
    {
        var path = Project($"<PackageReference Include=\"Gondwana\" Version=\"2.5.2\" /><PackageReference Include=\"Gondwana.{host}\" Version=\"2.5.2\" />");
        Assert.Equal(0, Run("add", "hosting", "-p", path));
        Assert.Contains(new ProjectPackages(path).Packages, p => p.Name == $"Gondwana.{host}.Hosting");
        if (host == "Blazor")
        {
            Assert.Equal(0, Run("add", "audio", "-p", path));
            Assert.Contains(new ProjectPackages(path).Packages, p => p.Name == "Gondwana.Audio.Browser");
            Assert.Equal(1, Run("add", "video", "-p", path));
        }
    }

    [Fact]
    public async Task LatestVersion_UsesCommonStableIntersection()
    {
        var version = await PackageVersions.LatestCommonStable(["Gondwana", "Gondwana.Widgets"], name => Task.FromResult(name == "Gondwana" ? new[] { "2.5.2", "2.6.0", "2.7.0", "3.0.0-beta" } : new[] { "2.5.2", "2.6.0", "3.0.0-beta" }));
        Assert.Equal("2.6.0", version);
        await Assert.ThrowsAsync<InvalidOperationException>(() => PackageVersions.LatestCommonStable(["Gondwana"], _ => Task.FromResult(new[] { "3.0.0-beta" })));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssetScan_ReportsUnreadableDirectory_AndContinuesWithSiblings(bool denied)
    {
        var blocked = Directory.CreateDirectory(Path.Combine(root, "blocked")).FullName;
        var readable = Directory.CreateDirectory(Path.Combine(root, "readable")).FullName;
        var asset = Path.Combine(readable, "valid.gts");
        File.WriteAllText(asset, "fixture");
        var warnings = new List<string>();
        var files = ProjectHealth.SourceFiles(root, (path, _) => warnings.Add(path), path =>
        {
            if (path == blocked)
            {
                if (denied) throw new UnauthorizedAccessException("Access denied");
                throw new IOException("Directory disappeared");
            }
            return new DirectoryInfo(path).GetFileSystemInfos();
        }).ToArray();
        Assert.Equal(new[] { blocked }, warnings);
        Assert.Equal(new[] { asset }, files);
    }
}
