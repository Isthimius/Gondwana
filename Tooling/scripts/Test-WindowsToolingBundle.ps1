# Verify the actual staged tool directories, not just successful dotnet builds.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ToolingRoot
)

$ErrorActionPreference = 'Stop'
$ToolingRoot = (Resolve-Path -LiteralPath $ToolingRoot).Path

$apps = @(
    @{ Folder = 'Gondwana.Tooling.Studio.WinForms'; Exe = 'Gondwana.Tooling.Studio.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Studio.Assets.WinForms'; Exe = 'Gondwana.Tooling.Studio.Assets.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Studio.Animations.WinForms'; Exe = 'Gondwana.Tooling.Studio.Animations.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Studio.Audio.WinForms'; Exe = 'Gondwana.Tooling.Studio.Audio.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Studio.Scenes.WinForms'; Exe = 'Gondwana.Tooling.Studio.Scenes.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Studio.SceneViewer.WinForms'; Exe = 'Gondwana.Tooling.Studio.SceneViewer.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Studio.Sprites.WinForms'; Exe = 'Gondwana.Tooling.Studio.Sprites.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Studio.Tilesheets.WinForms'; Exe = 'Gondwana.Tooling.Studio.Tilesheets.WinForms' }
)

foreach ($app in $apps) {
    $appFolder = Join-Path $ToolingRoot $app.Folder
    foreach ($relative in @("$($app.Exe).exe", "$($app.Exe).dll",
                            "$($app.Exe).deps.json", "$($app.Exe).runtimeconfig.json",
                            'hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll')) {
        $path = Join-Path $appFolder $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing executable or self-contained runtime file: $path"
        }
    }
}

$pluginFolder = Join-Path $ToolingRoot 'Gondwana.Tooling.Studio.WinForms/plugins'
foreach ($file in @(
    'ProjectDiagnostics/plugin.json',
    'ProjectDiagnostics/Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics.deps.json',
    'ProjectDiagnostics/Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics.dll',
    'ExternalImport/plugin.json',
    'ExternalImport/Gondwana.Tooling.Studio.Plugin.ExternalImport.dll',
    'ExternalImport/Gondwana.Tooling.Studio.Plugin.ExternalImport.deps.json',
    'ExternalImport/Gondwana.Tooling.Importers.dll'
)) {
    $path = Join-Path $pluginFolder $file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing Studio plugin or dependency: $path"
    }
}

foreach ($package in @('ProjectDiagnostics', 'ExternalImport')) {
    $folder = Join-Path $pluginFolder $package
    $manifest = Get-Content -LiteralPath (Join-Path $folder 'plugin.json') -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or $manifest.contractVersion -ne 2 -or
        $manifest.entryAssembly -ne "Gondwana.Tooling.Studio.Plugin.$package.dll") {
        throw "Invalid bundled plugin manifest: $folder"
    }
    foreach ($file in Get-ChildItem -LiteralPath $folder -Filter '*.dll' -Recurse) {
        if ($file.Name -ne $manifest.entryAssembly -and
            -not ($package -eq 'ExternalImport' -and $file.Name -eq 'Gondwana.Tooling.Importers.dll')) {
            throw "Unexpected private library in bundled plugin: $($file.FullName)"
        }
    }
}

# Validate the sibling layout recognized by SceneViewerLauncher.Locate.
$viewer = Join-Path $ToolingRoot 'Gondwana.Tooling.Studio.SceneViewer.WinForms/Gondwana.Tooling.Studio.SceneViewer.WinForms.exe'
foreach ($hostFolder in @('Gondwana.Tooling.Studio.WinForms', 'Gondwana.Tooling.Studio.Scenes.WinForms')) {
    $fromHost = [System.IO.Path]::GetFullPath((Join-Path (Join-Path $ToolingRoot $hostFolder) "../Gondwana.Tooling.Studio.SceneViewer.WinForms/Gondwana.Tooling.Studio.SceneViewer.WinForms.exe"))
    if ($fromHost -ne [System.IO.Path]::GetFullPath($viewer) -or -not (Test-Path -LiteralPath $fromHost -PathType Leaf)) {
        throw "Scene Viewer is not discoverable from $hostFolder"
    }
}

Write-Host "Validated all eight self-contained Windows apps, Studio plugins, and Scene Viewer layout."
