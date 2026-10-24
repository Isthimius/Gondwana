# Verify the actual staged tool directories, not just successful dotnet builds.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ToolingRoot
)

$ErrorActionPreference = 'Stop'
$ToolingRoot = (Resolve-Path -LiteralPath $ToolingRoot).Path

$apps = @(
    @{ Folder = 'Gondwana.Tooling.Studio.WinForms'; Exe = 'Gondwana.Studio.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Assets.WinForms'; Exe = 'Gondwana.Tooling.Assets.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Animations.WinForms'; Exe = 'Gondwana.Tooling.Animations.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Audio.WinForms'; Exe = 'Gondwana.Tooling.Audio.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Scenes.WinForms'; Exe = 'Gondwana.Tooling.Scenes.WinForms' }
    @{ Folder = 'Gondwana.Tooling.SceneViewer.WinForms'; Exe = 'Gondwana.Tooling.SceneViewer.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Sprites.WinForms'; Exe = 'Gondwana.Tooling.Sprites.WinForms' }
    @{ Folder = 'Gondwana.Tooling.Tilesheets.WinForms'; Exe = 'Gondwana.Tooling.Tilesheets.WinForms' }
)

foreach ($app in $apps) {
    $appFolder = Join-Path $ToolingRoot $app.Folder
    foreach ($relative in @("$($app.Exe).exe", "$($app.Exe).runtimeconfig.json",
                            'hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll')) {
        $path = Join-Path $appFolder $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing executable or self-contained runtime file: $path"
        }
    }
}

$pluginFolder = Join-Path $ToolingRoot 'Gondwana.Tooling.Studio.WinForms/plugins'
foreach ($file in @(
    'Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics.dll',
    'Gondwana.Tooling.Studio.Plugin.ExternalImport.dll',
    'Gondwana.Tooling.Studio.Plugin.ExternalImport.deps.json',
    'Gondwana.Tooling.Importers.dll'
)) {
    $path = Join-Path $pluginFolder $file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing Studio plugin or dependency: $path"
    }
}

# Validate the sibling layout recognized by SceneViewerLauncher.Locate.
$viewer = Join-Path $ToolingRoot 'Gondwana.Tooling.SceneViewer.WinForms/Gondwana.Tooling.SceneViewer.WinForms.exe'
foreach ($hostFolder in @('Gondwana.Tooling.Studio.WinForms', 'Gondwana.Tooling.Scenes.WinForms')) {
    $fromHost = [System.IO.Path]::GetFullPath((Join-Path (Join-Path $ToolingRoot $hostFolder) "../Gondwana.Tooling.SceneViewer.WinForms/Gondwana.Tooling.SceneViewer.WinForms.exe"))
    if ($fromHost -ne [System.IO.Path]::GetFullPath($viewer) -or -not (Test-Path -LiteralPath $fromHost -PathType Leaf)) {
        throw "Scene Viewer is not discoverable from $hostFolder"
    }
}

Write-Host "Validated all eight self-contained Windows apps, Studio plugins, and Scene Viewer layout."
