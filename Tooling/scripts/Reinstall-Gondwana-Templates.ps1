#Requires -Version 5.1
<#
.SYNOPSIS
    Packs and reinstalls the local Gondwana templates package from this repository.

.DESCRIPTION
    Intended for repeated local template iteration when package versions have not yet been published.
    The script:
      1. Packs Tooling/Gondwana.Templates/Gondwana.Templates.csproj into a local package source.
      2. Detects the exact version that was just packed.
      3. Uninstalls the existing Gondwana.Templates package if it is already installed.
      4. Installs that exact packed version via a local package source and isolated NuGet cache.
      5. Prints the installed template package version and the Gondwana templates now available.

.PARAMETER Configuration
    Build configuration passed to 'dotnet pack'. Defaults to 'Release'.

.PARAMETER PackageOutput
    Package output directory for the temporary local template feed.
    Relative paths are resolved from the repository root.
    Defaults to '.local-nuget' under the repository root.

.EXAMPLE
    .\Reinstall-Gondwana-Templates.ps1

.EXAMPLE
    .\Reinstall-Gondwana-Templates.ps1 -Configuration Debug

.EXAMPLE
    .\Reinstall-Gondwana-Templates.ps1 -PackageOutput artifacts\local-templates
#>
param(
    [string] $Configuration = 'Release',
    [string] $PackageOutput = '.local-nuget'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Get-Item (Join-Path $PSScriptRoot '../..')).FullName
$templatesProject = Join-Path $repoRoot 'Tooling/Gondwana.Templates/Gondwana.Templates.csproj'
$packageSource = if ([System.IO.Path]::IsPathRooted($PackageOutput)) {
    $PackageOutput
} else {
    Join-Path $repoRoot $PackageOutput
}
$tempNuGetPackages = Join-Path ([System.IO.Path]::GetTempPath()) "gondwana-templates-reinstall-packages-$([Guid]::NewGuid().ToString('N'))"

function Invoke-Cmd {
    param([string] $Executable, [string[]] $Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "'$Executable $($Arguments -join ' ')' exited with code $LASTEXITCODE."
    }
}

function Get-InstalledTemplatePackageVersion {
    $output = dotnet new uninstall 2>&1
    $lines = @($output | ForEach-Object { $_.ToString() })

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $trimmedLine = $lines[$i].Trim()
        if ($trimmedLine -match '^Gondwana\.Templates(?:\s*::\s*(.+?))?$') {
            if ($Matches[1]) {
                return $Matches[1].Trim()
            }

            for ($j = $i + 1; $j -lt $lines.Count; $j++) {
                $line = $lines[$j]
                if ($line -match '^\s*Version:\s*(.+?)\s*$') {
                    return $Matches[1].Trim()
                }
                if ($line -match '^\S') {
                    break
                }
            }

            return 'installed'
        }
    }

    return $null
}

function Get-LatestPackedTemplatePackage {
    param([string] $Source)

    $package = Get-ChildItem -Path $Source -Filter 'Gondwana.Templates.*.nupkg' -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $package) {
        throw "No Gondwana.Templates package was produced in '$Source'."
    }

    if ($package.Name -notmatch '^Gondwana\.Templates\.(.+)\.nupkg$') {
        throw "Could not determine the packed Gondwana.Templates version from '$($package.Name)'."
    }

    return [pscustomobject]@{
        Path    = $package.FullName
        Version = $Matches[1]
    }
}

if (-not (Test-Path $templatesProject -PathType Leaf)) {
    throw "Gondwana.Templates project not found at '$templatesProject'."
}

New-Item -ItemType Directory -Path $packageSource -Force | Out-Null
New-Item -ItemType Directory -Path $tempNuGetPackages -Force | Out-Null

Write-Host "Templates project : $templatesProject" -ForegroundColor Cyan
Write-Host "Package feed      : $packageSource" -ForegroundColor Cyan

$previousNuGetPackages = $env:NUGET_PACKAGES
try {
    Write-Host ""
    Write-Host "Packing Gondwana.Templates..." -ForegroundColor Cyan
    Invoke-Cmd dotnet @('pack', $templatesProject, '--configuration', $Configuration, '--output', $packageSource, '--nologo')

    $packedPackage = Get-LatestPackedTemplatePackage -Source $packageSource

    $installedVersion = Get-InstalledTemplatePackageVersion
    if (-not [string]::IsNullOrWhiteSpace($installedVersion)) {
        Write-Host ""
        Write-Host "Uninstalling existing Gondwana.Templates ($installedVersion)..." -ForegroundColor Cyan
        Invoke-Cmd dotnet @('new', 'uninstall', 'Gondwana.Templates')
    }

    Write-Host ""
    Write-Host "Installing Gondwana.Templates $($packedPackage.Version) from local package..." -ForegroundColor Cyan
    $env:NUGET_PACKAGES = $tempNuGetPackages
    Invoke-Cmd dotnet @('new', 'install', $packedPackage.Path, '--force')

    $currentVersion = Get-InstalledTemplatePackageVersion
    $templateList = dotnet new list gondwana 2>&1

    Write-Host ""
    Write-Host "Reinstall succeeded!" -ForegroundColor Green
    if (-not [string]::IsNullOrWhiteSpace($currentVersion)) {
        Write-Host "Version   : $currentVersion" -ForegroundColor Green
    }
    if (-not [string]::IsNullOrWhiteSpace(($templateList | Out-String))) {
        Write-Host ($templateList | Out-String).TrimEnd()
    }
} finally {
    if ([string]::IsNullOrWhiteSpace($previousNuGetPackages)) {
        Remove-Item Env:NUGET_PACKAGES -ErrorAction SilentlyContinue
    } else {
        $env:NUGET_PACKAGES = $previousNuGetPackages
    }

    Remove-Item $tempNuGetPackages -Recurse -Force -ErrorAction SilentlyContinue
}
