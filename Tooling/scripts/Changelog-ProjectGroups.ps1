# Shared changelog groups used by root changelog generation and releases.
# A commit touching more than one group intentionally appears under each one.
$ProjectChangelogGroups = @(
    [pscustomobject]@{ Name = "Gondwana"; IncludePaths = @("Gondwana/**/*") },
    [pscustomobject]@{ Name = "Gondwana.Audio.Browser"; IncludePaths = @("Gondwana.Audio.Browser/**/*") },
    [pscustomobject]@{ Name = "Gondwana.Audio.Midi"; IncludePaths = @("Gondwana.Audio.Midi/**/*") },
    [pscustomobject]@{ Name = "Gondwana.Avalonia"; IncludePaths = @("Gondwana.Avalonia/**/*") },
    [pscustomobject]@{ Name = "Gondwana.Avalonia.Hosting"; IncludePaths = @("Gondwana.Avalonia.Hosting/**/*") },
    [pscustomobject]@{ Name = "Gondwana.Blazor"; IncludePaths = @("Gondwana.Blazor/**/*") },
    [pscustomobject]@{ Name = "Gondwana.Blazor.Hosting"; IncludePaths = @("Gondwana.Blazor.Hosting/**/*") },
    [pscustomobject]@{ Name = "Gondwana.Hosting"; IncludePaths = @("Gondwana.Hosting/**/*") },
    [pscustomobject]@{ Name = "Gondwana.Input.SDL2"; IncludePaths = @("Gondwana.Input.SDL2/**/*") },
    [pscustomobject]@{ Name = "Gondwana.Video"; IncludePaths = @("Gondwana.Video/**/*") },
    [pscustomobject]@{ Name = "Gondwana.Widgets"; IncludePaths = @("Gondwana.Widgets/**/*") },
    [pscustomobject]@{ Name = "Gondwana.WinForms"; IncludePaths = @("Gondwana.WinForms/**/*") },
    [pscustomobject]@{ Name = "Gondwana.WinForms.Hosting"; IncludePaths = @("Gondwana.WinForms.Hosting/**/*") },
    [pscustomobject]@{ Name = "Tooling / Gondwana.Cli"; IncludePaths = @("Tooling/Gondwana.Cli/**/*") },
    [pscustomobject]@{ Name = "Tooling / Gondwana.Mcp"; IncludePaths = @("Tooling/Gondwana.Mcp/**/*") },
    [pscustomobject]@{ Name = "Tooling / Gondwana.Templates"; IncludePaths = @("Tooling/Gondwana.Templates/**/*") },
    [pscustomobject]@{ Name = "Tooling / Gondwana.Tooling.Assets.WinForms"; IncludePaths = @("Tooling/Gondwana.Tooling.Assets.WinForms/**/*") },
    [pscustomobject]@{ Name = "Tooling / Gondwana.Tooling.Studio.Avalonia"; IncludePaths = @("Tooling/Gondwana.Tooling.Studio.Avalonia/**/*") },
    [pscustomobject]@{ Name = "Tooling / Gondwana.Tooling.Studio.Core"; IncludePaths = @("Tooling/Gondwana.Tooling.Studio.Core/**/*") },
    [pscustomobject]@{ Name = "Tooling / Gondwana.Tooling.Studio.WinForms"; IncludePaths = @("Tooling/Gondwana.Tooling.Studio.WinForms/**/*") },
    [pscustomobject]@{ Name = "Tooling / Gondwana.Tooling.Tilesheets.WinForms"; IncludePaths = @("Tooling/Gondwana.Tooling.Tilesheets.WinForms/**/*") },
    [pscustomobject]@{
        Name = "Build / Repository"
        IncludePaths = @(
            ".github/**/*",
            "Solution Items/**/*",
            "Tooling/scripts/**/*",
            "Directory.Build.props",
            "Directory.Build.targets",
            "Directory.Packages.props",
            "version.json",
            "global.json",
            "NuGet.config",
            "cliff.toml",
            ".editorconfig",
            ".gitignore",
            "*.sln",
            "README.md"
        )
    }
)
