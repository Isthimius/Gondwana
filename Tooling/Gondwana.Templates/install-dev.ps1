#Requires -Version 5.1
<#
.SYNOPSIS
    Packs Gondwana.Templates and installs (or updates) the dotnet new templates.

.DESCRIPTION
    Run this script from a developer machine to build a local NuGet template
    package and immediately register it with 'dotnet new', replacing any
    previously installed version of the Gondwana templates.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    Write-Host 'Packing Gondwana.Templates...' -ForegroundColor Cyan
    dotnet pack --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'dotnet pack failed.' }

    $nupkg = Get-ChildItem -Path .\bin\Release -Filter 'Gondwana.Templates.*.nupkg' |
             Sort-Object LastWriteTime -Descending |
             Select-Object -First 1

    if (-not $nupkg) { throw 'No Gondwana.Templates .nupkg found in .\bin\Release.' }

    Write-Host "Installing $($nupkg.Name) as dotnet new templates..." -ForegroundColor Cyan
    dotnet new install $nupkg.FullName
    if ($LASTEXITCODE -ne 0) { throw 'dotnet new install failed.' }

    Write-Host "Done. Run 'dotnet new gondwana-winforms --help' to verify." -ForegroundColor Green
} finally {
    Pop-Location
}
