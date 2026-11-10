#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'Prepare-PrereleaseTemplates.ps1'
$sourcePath = Join-Path $PSScriptRoot '../Gondwana.Templates/templates'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('gondwana-prerelease-templates-' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    foreach ($source in @(Get-ChildItem $sourcePath -Filter MyGame.csproj -Recurse -File)) {
        $dir = Join-Path $temp $source.Directory.Name
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Copy-Item $source.FullName (Join-Path $dir 'MyGame.csproj')
    }
    $original = @{}
    foreach ($file in @(Get-ChildItem $temp -Filter MyGame.csproj -Recurse)) {
        $original[$file.FullName] = [IO.File]::ReadAllText($file.FullName)
    }
    & $scriptPath -Version '2.6.2' -TemplateRoot $temp
    foreach ($file in @(Get-ChildItem $temp -Filter MyGame.csproj -Recurse)) {
        if ([IO.File]::ReadAllText($file.FullName) -cne $original[$file.FullName]) {
            throw 'Stable packaging changed template references.'
        }
    }
    & $scriptPath -Version '2.6.2-beta.1' -TemplateRoot $temp
    foreach ($file in @(Get-ChildItem $temp -Filter MyGame.csproj -Recurse)) {
        $content = [IO.File]::ReadAllText($file.FullName)
        if ($content -notmatch 'Include="Gondwana" Version="2\.6\.2-beta\.1"' -or $content -match 'Include="Gondwana(?:\.[^"]+)?" Version="2\.\*"') {
            throw "Beta Gondwana references were not pinned in $($file.FullName)."
        }
        if ($content -match 'Include="Microsoft.AspNetCore.Components.WebAssembly"') {
            # Keep the two Microsoft package versions matched through prerelease packaging.
            foreach ($package in @('Microsoft.AspNetCore.Components.WebAssembly', 'Microsoft.AspNetCore.Components.WebAssembly.DevServer')) {
                $expected = 'Include="' + $package + '" Version="10.0.14"'
                if (!$content.Contains($expected)) {
                    throw "Blazor template dependency $package must remain on tested 10.0.14."
                }
            }
        }
        if ($content -match 'Include="Avalonia.Desktop"' -and $content -notmatch 'Include="Avalonia.Desktop" Version="11\.\*"') {
            throw 'Unrelated Avalonia dependencies were modified.'
        }
    }
    $rejected = $false
    try { & $scriptPath -Version 'not-a-version' -TemplateRoot $temp } catch { $rejected = $true }
    if (-not $rejected) { throw 'Malformed prerelease version was accepted.' }
    Write-Host 'PASS: stable templates unchanged; prerelease Gondwana dependencies pinned; unrelated references untouched.'
}
finally {
    Remove-Item $temp -Force -Recurse -ErrorAction SilentlyContinue
}
