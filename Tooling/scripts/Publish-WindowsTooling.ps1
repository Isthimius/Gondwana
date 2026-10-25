# Publish the eight Windows authoring/viewer applications in one consistent layout.
# Called by nightly packaging validation and tagged releases; keep this list authoritative for both.
[CmdletBinding()]
param(
    [string]$OutputRoot = "publish/Tooling"
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not [System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot $OutputRoot
}
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

$tools = @(
    @{ Name = 'Gondwana.Tooling.Studio.WinForms'; Executable = 'Gondwana.Studio.WinForms.exe' }
    @{ Name = 'Gondwana.Tooling.Assets.WinForms'; Executable = 'Gondwana.Tooling.Assets.WinForms.exe' }
    @{ Name = 'Gondwana.Tooling.Animations.WinForms'; Executable = 'Gondwana.Tooling.Animations.WinForms.exe' }
    @{ Name = 'Gondwana.Tooling.Audio.WinForms'; Executable = 'Gondwana.Tooling.Audio.WinForms.exe' }
    @{ Name = 'Gondwana.Tooling.Scenes.WinForms'; Executable = 'Gondwana.Tooling.Scenes.WinForms.exe' }
    @{ Name = 'Gondwana.Tooling.SceneViewer.WinForms'; Executable = 'Gondwana.Tooling.SceneViewer.WinForms.exe' }
    @{ Name = 'Gondwana.Tooling.Sprites.WinForms'; Executable = 'Gondwana.Tooling.Sprites.WinForms.exe' }
    @{ Name = 'Gondwana.Tooling.Tilesheets.WinForms'; Executable = 'Gondwana.Tooling.Tilesheets.WinForms.exe' }
)

foreach ($tool in $tools) {
    $project = Join-Path $repositoryRoot "Tooling/$($tool.Name)/$($tool.Name).csproj"
    $destination = Join-Path $OutputRoot $tool.Name
    Write-Host "Publishing $($tool.Name) (win-x64, self-contained) ..."
    & dotnet publish $project -c Release -r win-x64 --self-contained true -o $destination --nologo /p:EnableWindowsTargeting=true /p:PublishTrimmed=false
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed: $($tool.Name)"
    }
}

# Plugins are intentionally external to Studio's single-file/hosting decisions.
# They use the Studio assembly identity from the host, not private contract copies.
$plugins = @(
    @{ Name = 'Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics'; Files = @('Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics.dll') }
    @{ Name = 'Gondwana.Tooling.Studio.Plugin.ExternalImport'; Files = @(
        'Gondwana.Tooling.Studio.Plugin.ExternalImport.dll',
        'Gondwana.Tooling.Studio.Plugin.ExternalImport.deps.json',
        'Gondwana.Tooling.Importers.dll'
    ) }
)
$pluginDestination = Join-Path $OutputRoot 'Gondwana.Tooling.Studio.WinForms/plugins'
New-Item -ItemType Directory -Path $pluginDestination -Force | Out-Null

foreach ($plugin in $plugins) {
    $project = Join-Path $repositoryRoot "Tooling/$($plugin.Name)/$($plugin.Name).csproj"
    Write-Host "Building official Studio plugin $($plugin.Name) ..."
    & dotnet build $project -c Release --nologo /p:EnableWindowsTargeting=true
    if ($LASTEXITCODE -ne 0) {
        throw "Plugin build failed: $($plugin.Name)"
    }

    $built = Join-Path $repositoryRoot "Tooling/$($plugin.Name)/bin/Release/net8.0-windows"
    foreach ($file in $plugin.Files) {
        $source = Join-Path $built $file
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Plugin output is missing: $source"
        }
        Copy-Item -LiteralPath $source -Destination $pluginDestination -Force
    }
}

# The Scene Viewer must stay a sibling of the Scene editor and Studio.
# SceneViewerLauncher.Locate already supports this published layout.
Write-Host "Published Windows tooling to $OutputRoot"
