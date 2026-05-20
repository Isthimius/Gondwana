#Requires -Version 5.1
<#
.SYNOPSIS
    Packages and uploads a Gondwana WASM build to itch.io via butler.

.DESCRIPTION
    1. Optionally builds the project by calling Publish-Gondwana-Wasm.ps1.
    2. Zips the contents of the AppBundle directory (index.html at root).
    3. Pushes the zip to the specified itch.io game/channel using butler.

    Prerequisites:
    - butler (https://itch.io/docs/butler/) on PATH and authenticated.
    - The game must already exist on itch.io.

.PARAMETER Project
    Path to the .csproj file or directory containing a single .csproj.
    Defaults to the current directory.

.PARAMETER ItchGame
    The itch.io game slug in the form "user/game" (e.g. "isthimius/mygame").

.PARAMETER ItchChannel
    The itch.io release channel name. Defaults to "html5".

.PARAMETER Configuration
    Build configuration. Defaults to 'Release'.

.PARAMETER SkipBuild
    Skip the dotnet publish step; use an existing AppBundle.

.PARAMETER SkipWorkload
    Skip 'dotnet workload install wasm-tools' during the publish step.

.EXAMPLE
    .\Deploy-Gondwana-Itch.ps1 -ItchGame "isthimius/mygame"
    .\Deploy-Gondwana-Itch.ps1 -ItchGame "isthimius/mygame" -SkipBuild -ItchChannel "html5-beta"
    .\Deploy-Gondwana-Itch.ps1 -Project .\src\MyGame -ItchGame "isthimius/mygame"
#>
param(
    [string] $Project       = '.',
    [Parameter(Mandatory)] [string] $ItchGame,
    [string] $ItchChannel   = 'html5',
    [string] $Configuration = 'Release',
    [switch] $SkipBuild,
    [switch] $SkipWorkload
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ─── Require butler ───────────────────────────────────────────────────────────
if (-not (Get-Command butler -ErrorAction SilentlyContinue)) {
    throw "butler not found on PATH.`nInstall it from https://itch.io/docs/butler/ and run 'butler login'."
}

# ─── Build ────────────────────────────────────────────────────────────────────
if (-not $SkipBuild) {
    $publishScript = Join-Path $PSScriptRoot "Publish-Gondwana-Wasm.ps1"
    if (-not (Test-Path $publishScript)) {
        throw "Publish-Gondwana-Wasm.ps1 not found at '$publishScript'. Place both scripts in the same directory."
    }

    $appBundle = & $publishScript -Project $Project -Configuration $Configuration `
                                  $(if ($SkipWorkload) { '-SkipWorkload' })
    if (-not $appBundle -or -not (Test-Path $appBundle)) {
        throw "Publish step did not produce an AppBundle. Aborting."
    }
} else {
    # Locate an existing AppBundle
    if (Test-Path $Project -PathType Leaf) {
        $projectDir = Split-Path $Project -Parent
    } else {
        $projectDir = $Project
    }
    $appBundle = Join-Path $projectDir "bin" $Configuration "net8.0-browser" "browser-wasm" "AppBundle"
    if (-not (Test-Path $appBundle)) {
        $found = Get-ChildItem -Path (Join-Path $projectDir "bin") -Filter "AppBundle" -Recurse -ErrorAction SilentlyContinue |
                 Select-Object -First 1
        if ($found) { $appBundle = $found.FullName }
        else { throw "AppBundle not found. Run without -SkipBuild or point to an existing AppBundle." }
    }
}

Write-Host "AppBundle : $appBundle" -ForegroundColor Cyan

# ─── Zip AppBundle ────────────────────────────────────────────────────────────
$zipPath = Join-Path ([System.IO.Path]::GetTempPath()) "gondwana-wasm-$([System.IO.Path]::GetRandomFileName()).zip"

Write-Host ""
Write-Host "Zipping AppBundle..." -ForegroundColor Cyan

# Compress the *contents* of AppBundle so index.html is at the zip root.
Compress-Archive -Path (Join-Path $appBundle "*") -DestinationPath $zipPath -Force

Write-Host "Zip      : $zipPath" -ForegroundColor Cyan

# ─── Push to itch.io ──────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Pushing to itch.io: $ItchGame @ $ItchChannel ..." -ForegroundColor Cyan

butler push $zipPath "$ItchGame`:$ItchChannel"
if ($LASTEXITCODE -ne 0) {
    Remove-Item $zipPath -ErrorAction SilentlyContinue
    throw "butler push failed (exit $LASTEXITCODE)."
}

Remove-Item $zipPath -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Deployed to itch.io!" -ForegroundColor Green
Write-Host "Game     : https://$($ItchGame.Split('/')[0]).itch.io/$($ItchGame.Split('/')[1])"
