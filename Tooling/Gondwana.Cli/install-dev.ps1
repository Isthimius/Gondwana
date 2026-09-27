#Requires -Version 5.1
<#
.SYNOPSIS
    Packs Gondwana.Cli and reinstalls it as a global .NET tool.

.DESCRIPTION
    Compatibility wrapper for the repository's canonical local CLI reinstall script.
    Unlike 'dotnet tool update', the canonical script uninstalls the existing tool,
    clears the cached Gondwana.Cli package, packs the current checkout, and installs
    that exact local package. This is required for reliable iteration when the
    semantic package version has not changed.

.EXAMPLE
    .\install-dev.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script = (Resolve-Path (Join-Path $PSScriptRoot '..\scripts\Reinstall-Gondwana-Cli.ps1')).Path
& $script @args

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
