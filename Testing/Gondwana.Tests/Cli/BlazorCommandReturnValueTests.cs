using Gondwana.Cli.Commands.Deploy;
using Gondwana.Cli.Commands.Publish;
using Spectre.Console.Cli;

namespace Gondwana.Tests.Cli;

public sealed class BlazorCommandReturnValueTests
{
    private static readonly Lock PathLock = new();

    [Fact]
    public void PublishBlazorCommand_ReturnsZero_WhenPublishSucceeds()
    {
        using var fixture = CliCommandFixture.Create();
        var projectPath = fixture.CreateBlazorProject();

        var exitCode = fixture.Run(config => config.AddCommand<PublishBlazorCommand>("publish-blazor"),
            "publish-blazor",
            "--project", projectPath,
            "--configuration", "Release");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", "Release", "net8.0-browser", "publish", "wwwroot", "index.html")));
    }

    [Fact]
    public void PublishItchCommand_ReturnsZero_WhenPackageSucceeds()
    {
        using var fixture = CliCommandFixture.Create();
        var projectPath = fixture.CreateBlazorProject();
        var zipPath = Path.Combine(fixture.RootDirectory, "publish", "game.zip");

        var exitCode = fixture.Run(config => config.AddCommand<PublishItchCommand>("publish-itch"),
            "publish-itch",
            "--project", projectPath,
            "--configuration", "Release",
            "--output", zipPath);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(zipPath));
    }

    [Fact]
    public void DeployBlazorCommand_ReturnsZero_WhenLocalDeploySucceeds()
    {
        using var fixture = CliCommandFixture.Create();
        var projectPath = fixture.CreateBlazorProject();
        var webRoot = Path.Combine(fixture.RootDirectory, "webroot");

        var exitCode = fixture.Run(config => config.AddCommand<DeployBlazorCommand>("deploy-blazor"),
            "deploy-blazor",
            "--project", projectPath,
            "--configuration", "Release",
            "--web-root", webRoot);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(webRoot, "index.html")));
    }

    [Fact]
    public void DeployItchCommand_ReturnsZero_WhenUploadSucceeds()
    {
        using var fixture = CliCommandFixture.Create(includeButler: true);
        var projectPath = fixture.CreateBlazorProject();

        var exitCode = fixture.Run(config => config.AddCommand<DeployItchCommand>("deploy-itch"),
            "deploy-itch",
            "--project", projectPath,
            "--configuration", "Release",
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
        private readonly IDisposable _lockHandle;

        private CliCommandFixture(bool includeButler)
        {
            _lockHandle = PathLock.EnterScope();
            RootDirectory = Path.Combine(Path.GetTempPath(), "gondwana-cli-tests", Guid.NewGuid().ToString("N"));
            _workspaceDirectory = Path.Combine(RootDirectory, "workspace");
            _toolsDirectory = Path.Combine(RootDirectory, "tools");
            ButlerLogPath = Path.Combine(RootDirectory, "butler.log");

            Directory.CreateDirectory(_workspaceDirectory);
            Directory.CreateDirectory(_toolsDirectory);

            CreateFakeDotnet();
            if (includeButler)
                CreateFakeButler();

            _originalPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            Environment.SetEnvironmentVariable("PATH", _toolsDirectory + Path.PathSeparator + _originalPath);
            Environment.SetEnvironmentVariable("FAKE_BUTLER_LOG", ButlerLogPath);
        }

        public string RootDirectory { get; }

        public string ButlerLogPath { get; }

        public static CliCommandFixture Create(bool includeButler = false) => new(includeButler);

        public string CreateBlazorProject()
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

            _lockHandle.Dispose();
        }

        private void CreateFakeDotnet()
        {
            var scriptPath = Path.Combine(_toolsDirectory, "dotnet");
            File.WriteAllText(scriptPath,
                """
                #!/usr/bin/env bash
                set -e

                if [ "$1" = "workload" ]; then
                  exit 0
                fi

                if [ "$1" = "publish" ]; then
                  csproj="$2"
                  config="Release"
                  shift 2
                  while [ "$#" -gt 0 ]; do
                    if [ "$1" = "-c" ]; then
                      config="$2"
                      shift 2
                    else
                      shift
                    fi
                  done

                  project_dir="$(dirname "$csproj")"
                  publish_dir="$project_dir/bin/$config/net8.0-browser/publish/wwwroot"
                  mkdir -p "$publish_dir"
                  printf '<html><body>ok</body></html>' > "$publish_dir/index.html"
                  exit 0
                fi

                exit 0
                """);

            File.SetUnixFileMode(scriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
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

            File.SetUnixFileMode(scriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }
}
