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

        branch.AddCommand<NewWasmCommand>("wasm")
              .WithDescription("Create a new Avalonia Gondwana project targeting both desktop and browser/WASM.")
              .WithExample("new", "wasm", "MyGame");
    });

    config.AddBranch("templates", branch =>
    {
        branch.SetDescription("Manage Gondwana dotnet new templates.");

        branch.AddCommand<TemplatesInstallCommand>("install")
              .WithDescription("Install Gondwana.Templates from NuGet.");

        branch.AddCommand<TemplatesUpdateCommand>("update")
              .WithDescription("Update installed Gondwana templates.");

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
              .WithDescription("Package a browser/WASM AppBundle as an itch.io-ready zip.")
              .WithExample("publish", "itch")
              .WithExample("publish", "itch", "--project", "./src/MyGame", "--skip-build");

        branch.AddCommand<PublishWasmCommand>("wasm")
              .WithDescription("Publish a Gondwana project for browser/WASM (net8.0-browser).")
              .WithExample("publish", "wasm")
              .WithExample("publish", "wasm", "--project", "./src/MyGame", "--skip-workload");
    });

    config.AddBranch("run", branch =>
    {
        branch.SetDescription("Run a Gondwana project.");

        branch.SetDefaultCommand<RunDesktopCommand>();

        branch.AddCommand<RunWasmCommand>("wasm")
              .WithDescription("Build and run the project in the browser (net8.0-browser dev server).")
              .WithExample("run", "wasm")
              .WithExample("run", "wasm", "--skip-workload");
    });

    config.AddBranch("deploy", branch =>
    {
        branch.SetDescription("Deploy a Gondwana project to a distribution target.");

        branch.SetDefaultCommand<DeployWasmCommand>();

        branch.AddCommand<DeployWasmCommand>("wasm")
              .WithDescription("Deploy a browser/WASM AppBundle to a static web host.")
              .WithExample("deploy", "--web-root", "./dist/MyGame")
              .WithExample("deploy", "wasm", "--remote-host", "deploy@example.com", "--remote-path", "/var/www/html/mygame");

        branch.AddCommand<DeployItchCommand>("itch")
              .WithDescription("Deploy a browser/WASM build to itch.io via butler.")
              .WithExample("deploy", "itch", "--itch-game", "user/mygame")
              .WithExample("deploy", "itch", "--project", "./src/MyGame", "--itch-game", "user/mygame", "--skip-build");
    });

    config.AddBranch("assets", branch =>
    {
        branch.SetDescription("Pack, inspect, and extract Gondwana asset files.");

        branch.AddCommand<AssetsPackCommand>("pack")
              .WithDescription("Pack a directory of files into an asset bundle.")
              .WithExample("assets", "pack", "./Assets", "./game.assets");

        branch.AddCommand<AssetsListCommand>("list")
              .WithDescription("List all assets in a bundle.")
              .WithExample("assets", "list", "./game.assets");

        branch.AddCommand<AssetsExtractCommand>("extract")
              .WithDescription("Extract all assets from a bundle to a directory.")
              .WithExample("assets", "extract", "./game.assets", "./Extracted");

        branch.AddCommand<AssetsGenerateKeysCommand>("generate-keys")
              .WithDescription("Generate a C# constants class for all asset keys in a bundle.")
              .WithExample("assets", "generate-keys", "./game.assets");
    });
});

return app.Run(args);
