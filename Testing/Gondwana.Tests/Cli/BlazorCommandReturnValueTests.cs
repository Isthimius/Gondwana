using Gondwana.Cli.Commands.Deploy;
using Gondwana.Cli.Commands.Publish;
using Gondwana.Cli.Commands;
using Spectre.Console.Cli;

namespace Gondwana.Tests.Cli;

public sealed class BlazorCommandReturnValueTests
{
    private static readonly object PathLock = new();

    [Fact]
    public void PublishBlazorCommand_ReturnsZero_WhenPublishSucceeds()
    {
        using var fixture = CliCommandFixture.Create();
        var projectPath = fixture.CreateBuildableBlazorProject();

        var exitCode = fixture.Run(config => config.AddCommand<PublishBlazorCommand>("publish-blazor"),
            "publish-blazor",
            "--project", projectPath,
            "--configuration", "Release",
            "--skip-workload");

        Assert.Equal(0, exitCode);
        var wwwroot = ProjectHelper.TryLocateBlazorPublishRoot(projectPath, "Release");
        Assert.NotNull(wwwroot);
        Assert.True(File.Exists(Path.Combine(wwwroot!, "index.html")));
    }

    [Fact]
    public void PublishItchCommand_ReturnsZero_WhenPackageSucceeds()
    {
        using var fixture = CliCommandFixture.Create();
        var projectPath = fixture.CreatePublishedBlazorProject();
        var zipPath = Path.Combine(fixture.RootDirectory, "publish", "game.zip");

        var exitCode = fixture.Run(config => config.AddCommand<PublishItchCommand>("publish-itch"),
            "publish-itch",
            "--project", projectPath,
            "--configuration", "Release",
            "--skip-build",
            "--output", zipPath);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(zipPath));
    }

    [Fact]
    public void DeployBlazorCommand_ReturnsZero_WhenLocalDeploySucceeds()
    {
        using var fixture = CliCommandFixture.Create();
        var projectPath = fixture.CreatePublishedBlazorProject();
        var webRoot = Path.Combine(fixture.RootDirectory, "webroot");

        var exitCode = fixture.Run(config => config.AddCommand<DeployBlazorCommand>("deploy-blazor"),
            "deploy-blazor",
            "--project", projectPath,
            "--configuration", "Release",
            "--skip-build",
            "--web-root", webRoot);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(webRoot, "index.html")));
    }

    [Fact]
    public void DeployItchCommand_ReturnsZero_WhenUploadSucceeds()
    {
        using var fixture = CliCommandFixture.Create(includeButler: true);
        var projectPath = fixture.CreatePublishedBlazorProject();

        var exitCode = fixture.Run(config => config.AddCommand<DeployItchCommand>("deploy-itch"),
            "deploy-itch",
            "--project", projectPath,
            "--configuration", "Release",
            "--skip-build",
            "--itch-game", "user/game");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(fixture.ButlerLogPath));
        Assert.Contains("push", File.ReadAllText(fixture.ButlerLogPath));
        Assert.Contains("user/game:html5", File.ReadAllText(fixture.ButlerLogPath));
    }

    private sealed class CliCommandFixture : IDisposable
    {
        private readonly string _originalPath;
        private readonly string _toolsDirectory;
        private readonly string _workspaceDirectory;
        private readonly bool _lockTaken;

        private CliCommandFixture(bool includeButler)
        {
            Monitor.Enter(PathLock);
            _lockTaken = true;
            RootDirectory = Path.Combine(Path.GetTempPath(), "gondwana-cli-tests", Guid.NewGuid().ToString("N"));
            _workspaceDirectory = Path.Combine(RootDirectory, "workspace");
            _toolsDirectory = Path.Combine(RootDirectory, "tools");
            ButlerLogPath = Path.Combine(RootDirectory, "butler.log");

            Directory.CreateDirectory(_workspaceDirectory);
            Directory.CreateDirectory(_toolsDirectory);

            if (includeButler)
                CreateFakeButler();

            _originalPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            Environment.SetEnvironmentVariable("PATH", _toolsDirectory + Path.PathSeparator + _originalPath);
            Environment.SetEnvironmentVariable("FAKE_BUTLER_LOG", ButlerLogPath);
        }

        public string RootDirectory { get; }

        public string ButlerLogPath { get; }

        public static CliCommandFixture Create(bool includeButler = false) => new(includeButler);

        public string CreatePublishedBlazorProject()
        {
            var projectDirectory = Path.Combine(_workspaceDirectory, "Game");
            Directory.CreateDirectory(projectDirectory);

            var projectPath = Path.Combine(projectDirectory, "Game.csproj");
            File.WriteAllText(projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Gondwana.Blazor" Version="2.0.0" />
                  </ItemGroup>
                </Project>
                """);

            var publishDirectory = Path.Combine(projectDirectory, "bin", "Release", "net8.0-browser", "publish", "wwwroot");
            Directory.CreateDirectory(publishDirectory);
            File.WriteAllText(Path.Combine(publishDirectory, "index.html"), "<html><body>ok</body></html>");

            return projectPath;
        }

        public string CreateBuildableBlazorProject()
        {
            var projectDirectory = Path.Combine(_workspaceDirectory, "BuildableGame");
            Directory.CreateDirectory(projectDirectory);
            Directory.CreateDirectory(Path.Combine(projectDirectory, "Pages"));
            Directory.CreateDirectory(Path.Combine(projectDirectory, "wwwroot"));

            var projectPath = Path.Combine(projectDirectory, "BuildableGame.csproj");
            File.WriteAllText(projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="8.0.8" />
                    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="8.0.8" PrivateAssets="all" />
                  </ItemGroup>
                </Project>
                """);

            File.WriteAllText(Path.Combine(projectDirectory, "Program.cs"),
                """
                using BuildableGame;
                using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

                var builder = WebAssemblyHostBuilder.CreateDefault(args);
                builder.RootComponents.Add<App>("#app");
                await builder.Build().RunAsync();
                """);

            File.WriteAllText(Path.Combine(projectDirectory, "App.razor"),
                """
                <Router AppAssembly="@typeof(App).Assembly">
                    <Found Context="routeData">
                        <RouteView RouteData="@routeData" />
                    </Found>
                </Router>
                """);

            File.WriteAllText(Path.Combine(projectDirectory, "_Imports.razor"),
                """
                @using Microsoft.AspNetCore.Components.Routing
                """);

            File.WriteAllText(Path.Combine(projectDirectory, "Pages", "Index.razor"),
                """
                @page "/"

                <h1>Hello</h1>
                """);

            File.WriteAllText(Path.Combine(projectDirectory, "wwwroot", "index.html"),
                """
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <title>BuildableGame</title>
                    <base href="/" />
                </head>
                <body>
                    <div id="app">Loading...</div>
                    <script src="_framework/blazor.webassembly.js"></script>
                </body>
                </html>
                """);

            return projectPath;
        }

        public int Run(Action<IConfigurator> configure, params string[] args)
        {
            var app = new CommandApp();
            app.Configure(configure);
            return app.Run(args);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("PATH", _originalPath);
            Environment.SetEnvironmentVariable("FAKE_BUTLER_LOG", null);

            try
            {
                if (Directory.Exists(RootDirectory))
                    Directory.Delete(RootDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup for temp test data.
            }

            if (_lockTaken)
                Monitor.Exit(PathLock);
        }

        private void CreateFakeButler()
        {
            var scriptPath = Path.Combine(_toolsDirectory, "butler");
            File.WriteAllText(scriptPath,
                """
                #!/usr/bin/env bash
                set -e

                if [ "$1" = "--version" ]; then
                  echo "butler fake"
                  exit 0
                fi

                if [ "$1" = "push" ]; then
                  printf '%s\n' "$@" > "$FAKE_BUTLER_LOG"
                  exit 0
                fi

                exit 0
                """);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(scriptPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }
    }
}
