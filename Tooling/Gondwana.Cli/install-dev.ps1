#Requires -Version 5.1
<#
.SYNOPSIS
    Packs Gondwana.Cli and installs (or updates) it as a global .NET tool.

.DESCRIPTION
    Run this script from a developer machine to build a local NuGet package and
    immediately install it as the global 'gondwana' tool, replacing any
    previously installed version.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    Write-Host 'Packing Gondwana.Cli...' -ForegroundColor Cyan
    dotnet pack --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'dotnet pack failed.' }

    $nupkg = Get-ChildItem -Path .\bin\Release -Filter 'Gondwana.Cli.*.nupkg' |
             Sort-Object LastWriteTime -Descending |
             Select-Object -First 1

    if (-not $nupkg) { throw 'No Gondwana.Cli .nupkg found in .\bin\Release.' }

    Write-Host "Installing $($nupkg.Name) as a global tool..." -ForegroundColor Cyan
    dotnet tool update --global Gondwana.Cli --add-source $nupkg.DirectoryName
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool update failed.' }

    Write-Host "Done. Run 'gondwana --version' to verify." -ForegroundColor Green
} finally {
    Pop-Location
}
