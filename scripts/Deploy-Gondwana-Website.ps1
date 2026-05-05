#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes a Gondwana WASM build to a personal website (static host).

.DESCRIPTION
    1. Optionally builds the project by calling Publish-Gondwana-Wasm.ps1.
    2. Copies the AppBundle contents to a local web root or uses rsync/scp
       to upload to a remote host.

    For hosting the game correctly, your web server must send the following
    HTTP response headers on every request (required by SharedArrayBuffer
    and .NET WASM threading):

        Cross-Origin-Opener-Policy:   same-origin
        Cross-Origin-Embedder-Policy: require-corp

    Example nginx config:
        add_header Cross-Origin-Opener-Policy "same-origin";
        add_header Cross-Origin-Embedder-Policy "require-corp";

    The site must be served over HTTPS.

.PARAMETER Project
    Path to the .csproj file or directory containing a single .csproj.
    Defaults to the current directory.

.PARAMETER WebRoot
    Local destination directory (e.g. C:\inetpub\wwwroot\mygame or /var/www/mygame).
    Required when not using -RemoteHost.

.PARAMETER RemoteHost
    SSH remote in the form user@host (e.g. "deploy@mysite.com").
    Used with -RemotePath for rsync deployment. Requires rsync on PATH.

.PARAMETER RemotePath
    Remote destination path (e.g. "/var/www/html/mygame").
    Required when -RemoteHost is specified.

.PARAMETER Configuration
    Build configuration. Defaults to 'Release'.

.PARAMETER SkipBuild
    Skip the dotnet publish step; use an existing AppBundle.

.PARAMETER SkipWorkload
    Skip 'dotnet workload install wasm-tools' during the publish step.

.EXAMPLE
    # Copy to a local IIS / nginx web root
    .\Deploy-Gondwana-Website.ps1 -WebRoot "C:\inetpub\wwwroot\mygame"

    # Deploy to a remote server via rsync (Linux/macOS/WSL)
    .\Deploy-Gondwana-Website.ps1 -RemoteHost "deploy@mysite.com" -RemotePath "/var/www/html/mygame"

    # Skip build if AppBundle already exists
    .\Deploy-Gondwana-Website.ps1 -WebRoot "C:\inetpub\wwwroot\mygame" -SkipBuild
#>
param(
    [string] $Project       = '.',
    [string] $WebRoot       = '',
    [string] $RemoteHost    = '',
    [string] $RemotePath    = '',
    [string] $Configuration = 'Release',
    [switch] $SkipBuild,
    [switch] $SkipWorkload
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ─── Validate deployment target ───────────────────────────────────────────────
$useLocal  = -not [string]::IsNullOrWhiteSpace($WebRoot)
$useRemote = (-not [string]::IsNullOrWhiteSpace($RemoteHost)) -and (-not [string]::IsNullOrWhiteSpace($RemotePath))

if (-not $useLocal -and -not $useRemote) {
    throw "Specify a deployment target: -WebRoot <path>  or  -RemoteHost <user@host> -RemotePath <path>"
}
if ($useLocal -and $useRemote) {
    throw "Specify either -WebRoot or -RemoteHost / -RemotePath, not both."
}
if ($useRemote -and -not (Get-Command rsync -ErrorAction SilentlyContinue)) {
    throw "rsync not found on PATH. Install rsync (available in WSL, macOS, Git Bash, or native Linux)."
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
        else { throw "AppBundle not found. Run without -SkipBuild or use an existing build." }
    }
}

Write-Host "AppBundle : $appBundle" -ForegroundColor Cyan
Write-Host ""

# ─── Deploy ───────────────────────────────────────────────────────────────────
if ($useLocal) {
    Write-Host "Copying to local web root: $WebRoot ..." -ForegroundColor Cyan
    if (-not (Test-Path $WebRoot)) {
        New-Item -ItemType Directory -Path $WebRoot -Force | Out-Null
    }
    # Robocopy /MIR mirrors the AppBundle, removing stale files on the destination.
    if ($env:OS -eq 'Windows_NT') {
        robocopy $appBundle $WebRoot /MIR /NJH /NJS /NDL /NFL
        # Robocopy exit codes 0-7 indicate success/partial success.
        if ($LASTEXITCODE -gt 7) { throw "robocopy failed (exit $LASTEXITCODE)." }
    } else {
        rsync -a --delete "$appBundle/" "$WebRoot/"
        if ($LASTEXITCODE -ne 0) { throw "rsync failed (exit $LASTEXITCODE)." }
    }
    Write-Host "Deployed to: $WebRoot" -ForegroundColor Green
} else {
    Write-Host "Deploying to ${RemoteHost}:${RemotePath} via rsync ..." -ForegroundColor Cyan
    # Trailing slash on source copies contents, not the directory itself.
    rsync -avz --delete "$appBundle/" "$RemoteHost`:$RemotePath/"
    if ($LASTEXITCODE -ne 0) { throw "rsync failed (exit $LASTEXITCODE)." }
    Write-Host "Deployed to $RemoteHost`:$RemotePath" -ForegroundColor Green
}

Write-Host ""
Write-Host "Reminder: your web server must send these headers for .NET WASM threading to work:" -ForegroundColor Yellow
Write-Host "  Cross-Origin-Opener-Policy:   same-origin"
Write-Host "  Cross-Origin-Embedder-Policy: require-corp"
Write-Host "The site must also be served over HTTPS." -ForegroundColor Yellow
