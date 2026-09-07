# Authoritative project metadata for both generators and releases.
# Paths are repository-relative; omitted projects generate neither output.
# A commit touching more than one group intentionally appears under each one.
$ChangelogProjects = @(
    [pscustomobject]@{ Path = "Gondwana"; RootName = "Gondwana"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Audio.Browser"; RootName = "Gondwana.Audio.Browser"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Audio.Midi"; RootName = "Gondwana.Audio.Midi"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Avalonia"; RootName = "Gondwana.Avalonia"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Avalonia.Hosting"; RootName = "Gondwana.Avalonia.Hosting"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Blazor"; RootName = "Gondwana.Blazor"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Blazor.Hosting"; RootName = "Gondwana.Blazor.Hosting"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Hosting"; RootName = "Gondwana.Hosting"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Input.SDL2"; RootName = "Gondwana.Input.SDL2"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Video"; RootName = "Gondwana.Video"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Widgets"; RootName = "Gondwana.Widgets"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.WinForms"; RootName = "Gondwana.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.WinForms.Hosting"; RootName = "Gondwana.WinForms.Hosting"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Cli"; RootName = "Tooling / Gondwana.Cli"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Mcp"; RootName = "Tooling / Gondwana.Mcp"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Templates"; RootName = "Tooling / Gondwana.Templates"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Tooling.Assets.WinForms"; RootName = "Tooling / Gondwana.Tooling.Assets.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Tooling.Studio.Avalonia"; RootName = "Tooling / Gondwana.Tooling.Studio.Avalonia"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Tooling.Studio.Core"; RootName = "Tooling / Gondwana.Tooling.Studio.Core"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Tooling.Studio.WinForms"; RootName = "Tooling / Gondwana.Tooling.Studio.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Tooling.Tilesheets.WinForms"; RootName = "Tooling / Gondwana.Tooling.Tilesheets.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{
        Path = $null
        RootName = "Build / Repository"
        GenerateChangelog = $false
        IncludeInRootChangelog = $true
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

# Root-only areas can supply multiple globs; project globs derive from Path.
foreach ($project in $ChangelogProjects) {
    if ($project.Path) {
        $project | Add-Member -NotePropertyName IncludePaths -NotePropertyValue @("$($project.Path)/**/*")
    }
}
