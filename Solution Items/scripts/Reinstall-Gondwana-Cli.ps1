#Requires -Version 5.1
<#
.SYNOPSIS
    Packs and reinstalls the local Gondwana CLI global tool from this repository.

.DESCRIPTION
    Intended for repeated local CLI iteration when package versions have not changed.
    The script:
      1. Packs Tooling/Gondwana.Cli/Gondwana.Cli.csproj into a local package source.
      2. Uninstalls the existing global Gondwana.Cli tool if it is already installed.
      3. Installs Gondwana.Cli globally from the freshly packed local package source.
      4. Prints the installed gondwana version when available.

.PARAMETER Configuration
    Build configuration passed to 'dotnet pack'. Defaults to 'Release'.

.PARAMETER PackageOutput
    Package output directory for the temporary local tool feed.
    Relative paths are resolved from the repository root.
    Defaults to '.local-nuget' under the repository root.

.EXAMPLE
    .\Reinstall-Gondwana-Cli.ps1

.EXAMPLE
    .\Reinstall-Gondwana-Cli.ps1 -Configuration Debug

.EXAMPLE
    .\Reinstall-Gondwana-Cli.ps1 -PackageOutput artifacts\local-tools
#>
param(
    [string] $Configuration = 'Release',
    [string] $PackageOutput = '.local-nuget'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Get-Item (Join-Path $PSScriptRoot '../..')).FullName
$cliProject = Join-Path $repoRoot 'Tooling/Gondwana.Cli/Gondwana.Cli.csproj'
$packageSource = if ([System.IO.Path]::IsPathRooted($PackageOutput)) {
    $PackageOutput
} else {
    Join-Path $repoRoot $PackageOutput
}
$tempNuGetPackages = Join-Path ([System.IO.Path]::GetTempPath()) "gondwana-cli-reinstall-packages-$([Guid]::NewGuid().ToString('N'))"

function Invoke-Cmd {
    param([string] $Executable, [string[]] $Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "'$Executable $($Arguments -join ' ')' exited with code $LASTEXITCODE."
    }
}

function Test-GlobalToolInstalled {
    param([string] $PackageId)

    $toolList = dotnet tool list --global 2>&1
    return (@($toolList | Where-Object { $_ -match "^\s*$([regex]::Escape($PackageId))\s" })).Count -gt 0
}

if (-not (Test-Path $cliProject -PathType Leaf)) {
    throw "Gondwana.Cli project not found at '$cliProject'."
}

New-Item -ItemType Directory -Path $packageSource -Force | Out-Null
New-Item -ItemType Directory -Path $tempNuGetPackages -Force | Out-Null

Write-Host "CLI project : $cliProject" -ForegroundColor Cyan
Write-Host "Package feed: $packageSource" -ForegroundColor Cyan

$previousNuGetPackages = $env:NUGET_PACKAGES
try {
    Write-Host ""
    Write-Host "Packing Gondwana.Cli..." -ForegroundColor Cyan
    Invoke-Cmd dotnet @('pack', $cliProject, '--configuration', $Configuration, '--output', $packageSource, '--nologo')

    if (Test-GlobalToolInstalled 'Gondwana.Cli') {
        Write-Host ""
        Write-Host "Uninstalling existing global Gondwana.Cli..." -ForegroundColor Cyan
        Invoke-Cmd dotnet @('tool', 'uninstall', '--global', 'Gondwana.Cli')
    }

    Write-Host ""
    Write-Host "Installing Gondwana.Cli from isolated local package feed..." -ForegroundColor Cyan
    $env:NUGET_PACKAGES = $tempNuGetPackages
    Invoke-Cmd dotnet @('tool', 'install', '--global', 'Gondwana.Cli', '--source', $packageSource, '--prerelease', '--ignore-failed-sources', '--no-http-cache')

    $gondwanaCommand = Get-Command gondwana -ErrorAction SilentlyContinue
    if ($null -ne $gondwanaCommand) {
        $versionLine = ((& $gondwanaCommand.Source --version 2>&1) | Select-Object -First 1).ToString().Trim()

        Write-Host ""
        Write-Host "Reinstall succeeded!" -ForegroundColor Green
        if (-not [string]::IsNullOrWhiteSpace($versionLine)) {
            Write-Host "Version   : $versionLine" -ForegroundColor Green
        }
    } else {
        Write-Host ""
        Write-Host "Reinstall succeeded!" -ForegroundColor Green
        Write-Warning "The 'gondwana' command is not available in this session yet. Open a new shell and run 'gondwana --version'."
    }
} finally {
    if ([string]::IsNullOrWhiteSpace($previousNuGetPackages)) {
        Remove-Item Env:NUGET_PACKAGES -ErrorAction SilentlyContinue
    } else {
        $env:NUGET_PACKAGES = $previousNuGetPackages
    }

    Remove-Item $tempNuGetPackages -Recurse -Force -ErrorAction SilentlyContinue
}
