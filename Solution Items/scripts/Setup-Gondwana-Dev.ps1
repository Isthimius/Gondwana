#Requires -Version 5.1
<#
.SYNOPSIS
    Sets up a local Gondwana development environment from scratch.

.DESCRIPTION
    Idempotent setup script for new contributors to the Gondwana repository.
    Safe to run more than once — each step installs/restores requirements and
    applies updates where available.

    Steps performed:
      1.  Verifies Git is available on PATH.
      2.  Checks for the .NET 8 SDK; installs it via winget if missing (Windows only).
      3.  Restores local .NET tools (nbgv) from .config/dotnet-tools.json.
      4.  Restores NuGet packages for the solution, forcing dependency
          reevaluation.
      5.  Builds the solution in Release configuration.
      6.  Installs/updates the Gondwana CLI global tool (Gondwana.Cli).
      7.  Installs/updates Gondwana project templates (Gondwana.Templates).
      8.  Installs wasm-tools (if missing) and updates installed workloads.
      9.  Checks for SDL2 native binaries required by Gondwana.Input.SDL2.
      10. Checks for LibVLC native binaries required by Gondwana.Video;
          installs VLC (which includes LibVLC) via winget if missing (Windows only).
      11. Runs 'gondwana doctor' to confirm the final environment state.

    Steps 8–10 are skipped when -SkipOptional is supplied.

.PARAMETER SkipBuild
    Skip step 5 (dotnet build). Restores packages and tools only.

.PARAMETER SkipOptional
    Skip steps 8–10: wasm-tools workload, SDL2 check, and LibVLC check/install.

.EXAMPLE
    # Full setup — run from anywhere inside the cloned repository
    .\Setup-Gondwana-Dev.ps1

.EXAMPLE
    # Skip building the solution (faster for CI-like environments)
    .\Setup-Gondwana-Dev.ps1 -SkipBuild

.EXAMPLE
    # Install core tools only; skip WASM workload, SDL2, and LibVLC
    .\Setup-Gondwana-Dev.ps1 -SkipOptional
#>
param(
    [switch] $SkipBuild,
    [switch] $SkipOptional
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Repo root is two levels above this script (Solution Items/scripts/ → root)
$repoRoot     = (Get-Item (Join-Path $PSScriptRoot '../..')).FullName
$solutionFile = Join-Path $repoRoot 'Gondwana.sln'

# $IsWindows is only defined in PowerShell Core 6+; use a safe lookup so the
# script remains compatible with Windows PowerShell 5.1 (PSEdition = 'Desktop').
$_isWindowsVar = Get-Variable -Name 'IsWindows' -ErrorAction SilentlyContinue
$isWindowsOS   = ($PSVersionTable.PSEdition -eq 'Desktop') -or
                 ($null -ne $_isWindowsVar -and $_isWindowsVar.Value -eq $true)

# ─── Helpers ──────────────────────────────────────────────────────────────────

function Step { param([string] $Label) Write-Host "`n── $Label" -ForegroundColor Cyan }
function OK   { param([string] $Msg)   Write-Host "   ✓ $Msg"  -ForegroundColor Green }
function INFO { param([string] $Msg)   Write-Host "   · $Msg" }
function WARN { param([string] $Msg)   Write-Host "   ⚠ $Msg"  -ForegroundColor Yellow }

function Invoke-Cmd {
    param([string] $Executable, [string[]] $Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "'$Executable $($Arguments -join ' ')' exited with code $LASTEXITCODE."
    }
}

function Test-GlobalTool {
    param([string] $PackageId)
    $output = dotnet tool list -g 2>&1
    return (@($output | Where-Object { $_ -match [regex]::Escape($PackageId) })).Count -gt 0
}

function Test-TemplatesInstalled {
    $output = dotnet new list 2>&1
    return (@($output | Where-Object { $_ -match 'gondwana-winforms' })).Count -gt 0
}

function Test-Workload {
    param([string] $Id)
    $output = dotnet workload list 2>&1
    return (@($output | Where-Object { $_ -match "\b$([regex]::Escape($Id))\b" })).Count -gt 0
}

function Test-NativeDll {
    param([string[]] $Names)
    # Search System32, SysWOW64, known VLC install paths, and every PATH directory
    $searchDirs = @(
        "$env:SystemRoot\System32",
        "$env:SystemRoot\SysWOW64",
        'C:\Program Files\VideoLAN\VLC',
        'C:\Program Files (x86)\VideoLAN\VLC'
    ) + ($env:PATH -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    foreach ($name in $Names) {
        foreach ($dir in $searchDirs) {
            if (-not [string]::IsNullOrWhiteSpace($dir) -and
                (Test-Path (Join-Path $dir $name) -PathType Leaf)) {
                return $true
            }
        }
    }
    return $false
}

# ─── Step 1: Git ──────────────────────────────────────────────────────────────

Step '1/11  Git'
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git not found on PATH.`nInstall Git from https://git-scm.com and reopen your terminal."
}
OK "$(git --version)"

# ─── Step 2: .NET 8 SDK ───────────────────────────────────────────────────────

Step '2/11  .NET 8 SDK'
$dotnetFound = $null -ne (Get-Command dotnet -ErrorAction SilentlyContinue)
$hasSdk8     = $dotnetFound -and
               (@((dotnet --list-sdks 2>&1) | Where-Object { $_ -match '^8\.' })).Count -gt 0

if ($hasSdk8) {
    $sdk8Line = (dotnet --list-sdks 2>&1) | Where-Object { $_ -match '^8\.' } | Select-Object -Last 1
    OK ".NET 8 SDK already installed: $($sdk8Line.Trim())"
} elseif ($isWindowsOS) {
    INFO '.NET 8 SDK not found — attempting install via winget...'
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw ".NET 8 SDK not found and winget is unavailable.`nDownload and install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8 then rerun this script."
    }
    Invoke-Cmd winget @('install', '--id', 'Microsoft.DotNet.SDK.8', '--silent',
                        '--accept-source-agreements', '--accept-package-agreements')
    # Refresh PATH so the newly installed dotnet is usable in this session
    $env:PATH = [System.Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' +
                [System.Environment]::GetEnvironmentVariable('PATH', 'User')
    OK '.NET 8 SDK installed. Reopen your terminal if subsequent steps fail to find dotnet.'
} else {
    throw ".NET 8 SDK not found.`nInstall it via your package manager or from https://dotnet.microsoft.com/download/dotnet/8 then rerun this script."
}

# ─── Step 3: Local .NET tools (nbgv) ──────────────────────────────────────────

Step '3/11  Local .NET tools (nbgv)'
Push-Location $repoRoot
try {
    Invoke-Cmd dotnet @('tool', 'restore', '--no-cache')
    OK 'Local tools restored from latest available packages (nbgv ready).'
} finally {
    Pop-Location
}

# ─── Step 4: NuGet restore ────────────────────────────────────────────────────

Step '4/11  NuGet restore'
Invoke-Cmd dotnet @('restore', $solutionFile, '--nologo', '--force', '--no-cache', '/p:EnableWindowsTargeting=true')
OK 'NuGet packages restored with dependency reevaluation.'

# ─── Step 5: Build ────────────────────────────────────────────────────────────

Step '5/11  Build'
if ($SkipBuild) {
    INFO 'Skipped (-SkipBuild).'
} else {
    Invoke-Cmd dotnet @('build', $solutionFile, '--configuration', 'Release',
                        '--no-restore', '--nologo', '/p:EnableWindowsTargeting=true')
    OK 'Solution built successfully.'
}

# ─── Step 6: Gondwana CLI ─────────────────────────────────────────────────────

Step '6/11  Gondwana CLI (Gondwana.Cli)'
if (Test-GlobalTool 'gondwana.cli') {
    Invoke-Cmd dotnet @('tool', 'update', '--global', 'Gondwana.Cli')
    $cliLine = (dotnet tool list -g 2>&1) | Where-Object { $_ -match 'gondwana\.cli' } | Select-Object -First 1
    OK "Installed and updated to latest available: $($cliLine.Trim())"
} else {
    Invoke-Cmd dotnet @('tool', 'install', '--global', 'Gondwana.Cli')
    OK 'Gondwana.Cli installed.'
}

# ─── Step 7: Gondwana project templates ───────────────────────────────────────

Step '7/11  Gondwana project templates (Gondwana.Templates)'
if (Test-TemplatesInstalled) {
    Invoke-Cmd dotnet @('new', 'update')
    OK 'Installed templates checked and updated where available.'
} else {
    Invoke-Cmd dotnet @('new', 'install', 'Gondwana.Templates')
    OK 'Gondwana.Templates installed.'
}

# ─── Optional steps (8–10) ────────────────────────────────────────────────────

if ($SkipOptional) {
    Write-Host "`n   · Steps 8–10 skipped (-SkipOptional)."
} else {

    # ─── Step 8: WASM workload ────────────────────────────────────────────────

    Step '8/11  WASM workload (wasm-tools)'
    if (Test-Workload 'wasm-tools') {
        Invoke-Cmd dotnet @('workload', 'update')
        OK 'Installed workloads updated (including wasm-tools when updates are available).'
    } else {
        Invoke-Cmd dotnet @('workload', 'install', 'wasm-tools')
        OK 'wasm-tools workload installed.'
    }

    # ─── Step 9: SDL2 native library ──────────────────────────────────────────

    Step '9/11  SDL2 native library (Gondwana.Input.SDL2)'
    $sdl2Dlls = if ($isWindowsOS) { @('SDL2.dll') } else { @('libSDL2-2.0.so.0', 'libSDL2.so') }
    if (Test-NativeDll $sdl2Dlls) {
        OK "SDL2 native library found."
    } else {
        WARN "SDL2 native library not detected on this system."
        INFO "SDL2 is only required if you use the Gondwana.Input.SDL2 package (gamepad input)."
        INFO "When building a project that references Gondwana.Input.SDL2, the SDL2-CS NuGet"
        INFO "package typically copies SDL2.dll to the project output directory automatically."
        INFO "If you need a system-wide install, download the runtime from:"
        INFO "  https://github.com/libsdl-org/SDL/releases"
    }

    # ─── Step 10: LibVLC native library ───────────────────────────────────────

    Step '10/11 LibVLC native library (Gondwana.Video)'
    $vlcDlls = if ($isWindowsOS) { @('libvlc.dll') } else { @('libvlc.so.5', 'libvlc.so') }
    if (Test-NativeDll $vlcDlls) {
        OK "LibVLC native library found."
    } else {
        INFO "LibVLC not detected. Attempting to install VLC via winget..."
        if ($isWindowsOS -and (Get-Command winget -ErrorAction SilentlyContinue)) {
            try {
                Invoke-Cmd winget @('install', '--id', 'VideoLAN.VLC', '--silent',
                                    '--accept-source-agreements', '--accept-package-agreements')
                # Refresh PATH so the newly installed VLC directory is visible
                $env:PATH = [System.Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' +
                            [System.Environment]::GetEnvironmentVariable('PATH', 'User')
                if (Test-NativeDll $vlcDlls) {
                    OK 'LibVLC is now available.'
                } else {
                    OK "VLC installed. libvlc.dll is in VLC's install directory."
                    INFO "Add 'C:\Program Files\VideoLAN\VLC' to your PATH if you use Gondwana.Video."
                }
            } catch {
                WARN "winget install VideoLAN.VLC failed: $_"
                WARN "Download and install VLC manually from https://www.videolan.org/vlc/"
                INFO "LibVLC is only required if you use the Gondwana.Video package."
            }
        } else {
            WARN "LibVLC not found. Install VLC to get libvlc:"
            INFO "  Windows: https://www.videolan.org/vlc/  or  winget install VideoLAN.VLC"
            INFO "  Linux:   sudo apt install libvlc-dev  (or equivalent)"
            INFO "  macOS:   brew install vlc"
            INFO "LibVLC is only required if you use the Gondwana.Video package."
        }
    }
}

# ─── Step 11: gondwana doctor ─────────────────────────────────────────────────

Step '11/11 gondwana doctor'
if (Get-Command gondwana -ErrorAction SilentlyContinue) {
    gondwana doctor
} else {
    WARN "'gondwana' command not found on PATH."
    INFO "If you just installed Gondwana.Cli, reopen your terminal and run 'gondwana doctor'."
}

Write-Host ""
Write-Host "Setup complete." -ForegroundColor Green
