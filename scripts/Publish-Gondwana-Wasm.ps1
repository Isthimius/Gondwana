#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and publishes a Gondwana WASM project for browser deployment.

.DESCRIPTION
    Installs the wasm-tools .NET workload (if needed), then runs
    'dotnet publish -f net8.0-browser -c Release'.
    On success, prints the path to the generated AppBundle directory.

.PARAMETER Project
    Path to the .csproj file or directory containing a single .csproj.
    Defaults to the current directory.

.PARAMETER Configuration
    Build configuration. Defaults to 'Release'.

.PARAMETER SkipWorkload
    Skip 'dotnet workload install wasm-tools'. Use this flag if the workload
    is already installed and you want a faster build.

.EXAMPLE
    .\Publish-Gondwana-Wasm.ps1
    .\Publish-Gondwana-Wasm.ps1 -Project .\src\MyGame
    .\Publish-Gondwana-Wasm.ps1 -SkipWorkload -Configuration Debug

.OUTPUTS
    AppBundle directory path on success (e.g. bin\Release\net8.0-browser\browser-wasm\AppBundle).
#>
param(
    [string] $Project       = '.',
    [string] $Configuration = 'Release',
    [switch] $SkipWorkload
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ─── Resolve .csproj ──────────────────────────────────────────────────────────
if (Test-Path $Project -PathType Leaf) {
    $csproj = Resolve-Path $Project
} else {
    $csprojFiles = @(Get-ChildItem -Path $Project -Filter '*.csproj' -ErrorAction SilentlyContinue)
    if ($csprojFiles.Count -eq 0) {
        throw "No .csproj found in '$Project'. Pass -Project <path-to-csproj>."
    }
    if ($csprojFiles.Count -gt 1) {
        throw "Multiple .csproj files found in '$Project'. Pass -Project <path-to.csproj> to specify one."
    }
    $csproj = $csprojFiles[0].FullName
}

$projectDir = Split-Path $csproj -Parent
Write-Host "Project  : $csproj" -ForegroundColor Cyan

# ─── Verify this is a WASM-capable project ────────────────────────────────────
$content = Get-Content $csproj -Raw
if ($content -notmatch 'net8\.0-browser') {
    Write-Warning "The project does not appear to target 'net8.0-browser'."
    Write-Warning "Make sure <TargetFrameworks> includes 'net8.0-browser' in $csproj."
    Write-Warning "Continuing anyway — dotnet publish may fail."
}

# ─── Install wasm-tools workload ──────────────────────────────────────────────
if (-not $SkipWorkload) {
    Write-Host ""
    Write-Host "Installing / updating wasm-tools workload..." -ForegroundColor Cyan
    dotnet workload install wasm-tools
    if ($LASTEXITCODE -ne 0) { throw "dotnet workload install wasm-tools failed (exit $LASTEXITCODE)." }
}

# ─── Publish ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Publishing for net8.0-browser ($Configuration)..." -ForegroundColor Cyan
dotnet publish $csproj -f net8.0-browser -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }

# ─── Locate AppBundle ─────────────────────────────────────────────────────────
$appBundle = Join-Path $projectDir "bin" $Configuration "net8.0-browser" "browser-wasm" "AppBundle"
if (Test-Path $appBundle) {
    Write-Host ""
    Write-Host "Publish succeeded!" -ForegroundColor Green
    Write-Host "AppBundle: $appBundle" -ForegroundColor Green
    Write-Output $appBundle
} else {
    # Some versions of the WASM tooling place the output elsewhere; search for it.
    $found = Get-ChildItem -Path (Join-Path $projectDir "bin") -Filter "AppBundle" -Recurse -ErrorAction SilentlyContinue |
             Select-Object -First 1
    if ($found) {
        Write-Host ""
        Write-Host "Publish succeeded!" -ForegroundColor Green
        Write-Host "AppBundle: $($found.FullName)" -ForegroundColor Green
        Write-Output $found.FullName
    } else {
        Write-Warning "Could not locate AppBundle directory. Check the publish output above."
    }
}
