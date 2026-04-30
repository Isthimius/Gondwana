using Gondwana.Cli.Commands;
using Gondwana.Cli.Commands.Assets;
using Gondwana.Cli.Commands.New;
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
