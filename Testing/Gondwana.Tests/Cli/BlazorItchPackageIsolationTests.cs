using System.IO.Compression;
using Gondwana.Cli.Commands;

namespace Gondwana.Tests.Cli;

public sealed class BlazorItchPackageIsolationTests
{
    [Fact]
    public void CreateBlazorItchPackage_BaseHrefOverride_DoesNotMutatePublishedIndex()
    {
        var root = Path.Combine(Path.GetTempPath(), "gondwana-cli-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var projectDirectory = Path.Combine(root, "Game");
            Directory.CreateDirectory(projectDirectory);

            var projectPath = Path.Combine(projectDirectory, "Game.csproj");
            File.WriteAllText(projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
                  <PropertyGroup>
                    <TargetFramework>net8.0-browser</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Gondwana.Blazor" Version="2.0.0" />
                  </ItemGroup>
                </Project>
                """);

            var wwwroot = Path.Combine(projectDirectory, "bin", "Release", "net8.0-browser", "publish", "wwwroot");
            Directory.CreateDirectory(wwwroot);
            var indexPath = Path.Combine(wwwroot, "index.html");
            const string originalIndex = "<html><head><base href=\"/\" /></head><body>ok</body></html>";
            File.WriteAllText(indexPath, originalIndex);

            var zipPath = Path.Combine(root, "game.zip");
            var packagePath = ProjectHelper.CreateBlazorItchPackage(
                projectPath,
                "Release",
                "net8.0-browser",
                skipBuild: true,
                skipWorkload: true,
                baseHref: "./",
                outputPath: zipPath,
                out var exitCode);

            Assert.Equal(0, exitCode);
            Assert.Equal(Path.GetFullPath(zipPath), packagePath);
            Assert.Equal(originalIndex, File.ReadAllText(indexPath));

            using var archive = ZipFile.OpenRead(zipPath);
            var indexEntry = archive.GetEntry("index.html");
            Assert.NotNull(indexEntry);
            using var reader = new StreamReader(indexEntry!.Open());
            Assert.Contains("<base href=\"./\"", reader.ReadToEnd());
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temp test data.
            }
        }
    }
}
