# Authoritative project metadata for both generators and releases.
# Paths are repository-relative; omitted projects generate neither output.
# A commit touching more than one group intentionally appears under each one.
$ChangelogProjects = @(
    # core projects - individual changelogs and root changelog
    [pscustomobject]@{ Path = "Gondwana"; RootName = "Gondwana"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Audio.Browser"; RootName = "Gondwana.Audio.Browser"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Audio.Midi"; RootName = "Gondwana.Audio.Midi"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Audio.NAudio"; RootName = "Gondwana.Audio.NAudio"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Avalonia"; RootName = "Gondwana.Avalonia"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Avalonia.Hosting"; RootName = "Gondwana.Avalonia.Hosting"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Blazor"; RootName = "Gondwana.Blazor"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Blazor.Hosting"; RootName = "Gondwana.Blazor.Hosting"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Hosting"; RootName = "Gondwana.Hosting"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Input.SDL2"; RootName = "Gondwana.Input.SDL2"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Video"; RootName = "Gondwana.Video"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Video.Widgets"; RootName = "Gondwana.Video.Widgets"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.Widgets"; RootName = "Gondwana.Widgets"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.WinForms"; RootName = "Gondwana.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Gondwana.WinForms.Hosting"; RootName = "Gondwana.WinForms.Hosting"; GenerateChangelog = $true; IncludeInRootChangelog = $true },

    # demo projects - individual changelogs but not root changelog
    [pscustomobject]@{ Path = "Demos/Gondwana.Asteroids.Shared"; RootName = "Demos / Gondwana.Asteroids.Shared"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.Asteroids.WinForms"; RootName = "Demos / Gondwana.Asteroids.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.Asteroids.Avalonia"; RootName = "Demos / Gondwana.Asteroids.Avalonia"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.Asteroids.Blazor"; RootName = "Demos / Gondwana.Asteroids.Blazor"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.Breakout.Shared"; RootName = "Demos / Gondwana.Breakout.Shared"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.Breakout.WinForms"; RootName = "Demos / Gondwana.Breakout.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.Breakout.Avalonia"; RootName = "Demos / Gondwana.Breakout.Avalonia"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.Breakout.Blazor"; RootName = "Demos / Gondwana.Breakout.Blazor"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.CoordinateTest"; RootName = "Demos / Gondwana.CoordinateTest"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.Flappy"; RootName = "Demos / Gondwana.Flappy"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.ParticleTest"; RootName = "Demos / Gondwana.ParticleTest"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.Platformer"; RootName = "Demos / Gondwana.Platformer"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.Pong"; RootName = "Demos / Gondwana.Pong"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.RageToPro"; RootName = "Demos / Gondwana.RageToPro"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.SideScroller"; RootName = "Demos / Gondwana.SideScroller"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.SpaceDuel"; RootName = "Demos / Gondwana.SpaceDuel"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.TheGreatPlop"; RootName = "Demos / Gondwana.TheGreatPlop"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.Torchlight"; RootName = "Demos / Gondwana.Torchlight"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Gondwana.ZeldaPrototype"; RootName = "Demos / Gondwana.ZeldaPrototype"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Slider.WinForms"; RootName = "Demos / Slider.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Slider.Blazor"; RootName = "Demos / Slider.Blazor"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Slider.Shared"; RootName = "Demos / Slider.Shared"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Spot.Avalonia"; RootName = "Demos / Spot.Avalonia"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Spot.Blazor"; RootName = "Demos / Spot.Blazor"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Spot.Shared"; RootName = "Demos / Spot.Shared"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/Spot.WinForms"; RootName = "Demos / Spot.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/VideoTest"; RootName = "Demos / VideoTest"; GenerateChangelog = $true; IncludeInRootChangelog = $false },
    [pscustomobject]@{ Path = "Demos/WidgetsTest"; RootName = "Demos / WidgetsTest"; GenerateChangelog = $true; IncludeInRootChangelog = $false },

    # tooling projects - individual changelogs and root changelog
    [pscustomobject]@{ Path = "Tooling/Studio/Plugin/Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics"; RootName = "Tooling / Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Studio/Plugin/Gondwana.Tooling.Studio.Plugin.ExternalImport"; RootName = "Tooling / Gondwana.Tooling.Studio.Plugin.ExternalImport"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Tooling.Assets"; RootName = "Tooling / Gondwana.Tooling.Assets"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Tooling.Importers"; RootName = "Tooling / Gondwana.Tooling.Importers"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Cli"; RootName = "Tooling / Gondwana.Cli"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Mcp"; RootName = "Tooling / Gondwana.Mcp"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Gondwana.Templates"; RootName = "Tooling / Gondwana.Templates"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Studio/Gondwana.Tooling.Studio.Assets.WinForms"; RootName = "Tooling / Gondwana.Tooling.Studio.Assets.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Studio/Gondwana.Tooling.Studio.Animations.WinForms"; RootName = "Tooling / Gondwana.Tooling.Studio.Animations.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Studio/Gondwana.Tooling.Studio.Audio.WinForms"; RootName = "Tooling / Gondwana.Tooling.Studio.Audio.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Studio/Gondwana.Tooling.Studio.Core"; RootName = "Tooling / Gondwana.Tooling.Studio.Core"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Studio/Gondwana.Tooling.Studio.WinForms"; RootName = "Tooling / Gondwana.Tooling.Studio.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Studio/Gondwana.Tooling.Studio.Scenes.WinForms"; RootName = "Tooling / Gondwana.Tooling.Studio.Scenes.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Studio/Gondwana.Tooling.Studio.SceneViewer.WinForms"; RootName = "Tooling / Gondwana.Tooling.Studio.SceneViewer.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Studio/Gondwana.Tooling.Studio.Sprites.WinForms"; RootName = "Tooling / Gondwana.Tooling.Studio.Sprites.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    [pscustomobject]@{ Path = "Tooling/Studio/Gondwana.Tooling.Studio.Tilesheets.WinForms"; RootName = "Tooling / Gondwana.Tooling.Studio.Tilesheets.WinForms"; GenerateChangelog = $true; IncludeInRootChangelog = $true },
    
    # Shared Studio libraries have no individual changelogs, but belong in the root.
    # The pre-reorganization source path is temporary through stable v2.7.0.
    [pscustomobject]@{
        Path = $null
        RootName = "Tooling / Studio Shared"
        GenerateChangelog = $false
        IncludeInRootChangelog = $true
        IncludePaths = @("Tooling/Studio/Shared/**/*", "Tooling/Shared/**/*")
    },

    # root-only areas - no individual changelogs, but included in root changelog
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

# TEMPORARY v2.7.0 TRANSITION: retain old source folders when generating
# historical and unreleased changelogs through the stable v2.7.0 release.
# Remove these legacy paths AFTER v2.7.0 is released; leave current project paths.
$ChangelogLegacyPaths = @{
    "Demos/Slider.WinForms" = @("Demos/Slider")
    "Tooling/Studio/Gondwana.Tooling.Studio.Assets.WinForms" = @("Tooling/Studio/Gondwana.Tooling.Assets.WinForms", "Tooling/Gondwana.Tooling.Assets.WinForms", "Tooling/Gondwana.Assets.WinForms")
    "Tooling/Studio/Gondwana.Tooling.Studio.Animations.WinForms" = @("Tooling/Studio/Gondwana.Tooling.Animations.WinForms", "Tooling/Gondwana.Tooling.Animations.WinForms")
    "Tooling/Studio/Gondwana.Tooling.Studio.Audio.WinForms" = @("Tooling/Studio/Gondwana.Tooling.Audio.WinForms", "Tooling/Gondwana.Tooling.Audio.WinForms")
    "Tooling/Studio/Gondwana.Tooling.Studio.Scenes.WinForms" = @("Tooling/Studio/Gondwana.Tooling.Scenes.WinForms", "Tooling/Gondwana.Tooling.Scenes.WinForms")
    "Tooling/Studio/Gondwana.Tooling.Studio.Sprites.WinForms" = @("Tooling/Studio/Gondwana.Tooling.Sprites.WinForms", "Tooling/Gondwana.Tooling.Sprites.WinForms")
    "Tooling/Studio/Gondwana.Tooling.Studio.Tilesheets.WinForms" = @("Tooling/Studio/Gondwana.Tooling.Tilesheets.WinForms", "Tooling/Gondwana.Tooling.Tilesheets.WinForms")
    "Tooling/Studio/Gondwana.Tooling.Studio.SceneViewer.WinForms" = @("Tooling/Studio/Gondwana.Tooling.SceneViewer.WinForms", "Tooling/Gondwana.Tooling.SceneViewer.WinForms")
    "Tooling/Studio/Gondwana.Tooling.Studio.Core" = @("Tooling/Gondwana.Tooling.Studio.Core")
    "Tooling/Studio/Gondwana.Tooling.Studio.WinForms" = @("Tooling/Gondwana.Tooling.Studio.WinForms")
    "Tooling/Studio/Plugin/Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics" = @("Tooling/Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics")
    "Tooling/Studio/Plugin/Gondwana.Tooling.Studio.Plugin.ExternalImport" = @("Tooling/Gondwana.Tooling.Studio.Plugin.ExternalImport")
}

# The root and per-project generators share the same source-path coverage.
foreach ($project in $ChangelogProjects) {
    if ($project.Path) {
        $includePaths = @("$($project.Path)/**/*")
        if ($ChangelogLegacyPaths.ContainsKey($project.Path)) {
            $includePaths += @($ChangelogLegacyPaths[$project.Path] | ForEach-Object { "$_/**/*" })
        }
        $project | Add-Member -NotePropertyName IncludePaths -NotePropertyValue $includePaths
    }
}
