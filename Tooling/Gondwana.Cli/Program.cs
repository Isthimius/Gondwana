using Gondwana.Cli.Commands;
using Gondwana.Cli.Commands.Assets;
using Gondwana.Cli.Commands.Deploy;
using Gondwana.Cli.Commands.New;
using Gondwana.Cli.Commands.Publish;
using Gondwana.Cli.Commands.Run;
using Gondwana.Cli.Commands.Templates;
using Gondwana.Logging;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

// Suppress engine info/debug logs so they don't pollute CLI output.
EngineLogger.SetLogLevel(LogLevel.Warning);
EngineLogger.Mode = EngineLoggingMode.Synchronous;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("gondwana");
    config.UseAssemblyInformationalVersion();

    config.AddCommand<HelpCommand>("help")
          .WithDescription("Show a summary of all available commands.");

    config.AddCommand<DoctorCommand>("doctor")
          .WithDescription("Validate your local Gondwana development environment.");

    config.AddCommand<InfoCommand>("info")
          .WithDescription("Show information about the Gondwana project in the current directory.");

    config.AddBranch("new", branch =>
    {
        branch.SetDescription("Scaffold a new Gondwana project.");

        branch.AddCommand<NewWinFormsCommand>("winforms")
              .WithDescription("Create a new WinForms Gondwana project (Windows only).")
              .WithExample("new", "winforms", "MyGame");

        branch.AddCommand<NewAvaloniaCommand>("avalonia")
              .WithDescription("Create a new Avalonia Gondwana project (Windows, macOS, Linux).")
              .WithExample("new", "avalonia", "MyGame");

        branch.AddCommand<NewBlazorCommand>("blazor")
              .WithDescription("Create a new Blazor WebAssembly Gondwana project for browser/WASM.")
              .WithExample("new", "blazor", "MyGame");
    });

    config.AddBranch("templates", branch =>
    {
        branch.SetDescription("Manage Gondwana dotnet new templates.");

        branch.AddCommand<TemplatesInstallCommand>("install")
              .WithDescription("Install Gondwana.Templates from NuGet, or check for updates if already installed.");

        branch.AddCommand<TemplatesUpdateCommand>("update")
              .WithDescription("Check installed Gondwana templates for updates without downgrading newer local versions.");

        branch.AddCommand<TemplatesListCommand>("list")
              .WithDescription("List installed Gondwana templates.");
    });

    config.AddCommand<AssetsPackCommand>("pack")
          .WithDescription("Pack a directory of files into an asset bundle.")
          .WithExample("pack", "./Assets", "./game.assets");

    config.AddBranch("publish", branch =>
    {
        branch.SetDescription("Publish a Gondwana project for distribution.");

        branch.SetDefaultCommand<PublishDesktopCommand>();

        branch.AddCommand<PublishItchCommand>("itch")
              .WithDescription("Package a browser/WASM build as an itch.io-ready zip.")
              .WithExample("publish", "itch")
              .WithExample("publish", "itch", "--project", "./src/MyGame", "--skip-build");

        branch.AddCommand<PublishBlazorCommand>("blazor")
              .WithDescription("Publish a Gondwana Blazor WebAssembly project for browser deployment.")
              .WithExample("publish", "blazor")
              .WithExample("publish", "blazor", "--project", "./src/MyGame", "--skip-workload");
    });

    config.AddBranch("run", branch =>
    {
        branch.SetDescription("Run a Gondwana project.");

        branch.SetDefaultCommand<RunDesktopCommand>();

        branch.AddCommand<RunBlazorCommand>("blazor")
              .WithDescription("Build and run the Blazor WebAssembly project in the browser.")
              .WithExample("run", "blazor")
              .WithExample("run", "blazor", "--skip-workload");
    });

    config.AddBranch("deploy", branch =>
    {
        branch.SetDescription("Deploy a Gondwana project to a distribution target.");

        branch.SetDefaultCommand<DeployBlazorCommand>();

        branch.AddCommand<DeployBlazorCommand>("blazor")
              .WithDescription("Deploy a published Blazor WebAssembly project to a web server or local path.")
              .WithExample("deploy", "blazor", "--web-root", "./dist")
              .WithExample("deploy", "blazor", "--remote-host", "user@example.com", "--remote-path", "/var/www/game");

        branch.AddCommand<DeployItchCommand>("itch")
              .WithDescription("Deploy a browser/WASM build to itch.io via butler.")
              .WithExample("deploy", "itch", "--user", "myuser", "--game", "mygame");
    });
});

return app.Run(args);
