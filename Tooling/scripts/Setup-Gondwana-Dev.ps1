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
      11. Ensures git-cliff is installed; updates via winget when available (Windows).
      12. Installs butler (itch.io) by downloading the latest binary from the broth CDN
          (with fallback endpoints documented at https://itch.io/docs/butler/installing.html)
          and extracting it to
          %LOCALAPPDATA%\itch\butler (Windows) or ~/.itch/butler (Linux/macOS). Prints a
          reminder to add the directory to PATH and run 'butler login'.
      13. Runs 'gondwana doctor' to confirm the final environment state.

    Steps 8–12 are skipped when -SkipOptional is supplied.

.PARAMETER SkipBuild
    Skip step 5 (dotnet build). Restores packages and tools only.

.PARAMETER SkipOptional
    Skip steps 8–12: wasm-tools, SDL2, LibVLC, git-cliff, and butler checks/install.

.EXAMPLE
    # Full setup — run from anywhere inside the cloned repository
    .\Setup-Gondwana-Dev.ps1

.EXAMPLE
    # Skip building the solution (faster for CI-like environments)
    .\Setup-Gondwana-Dev.ps1 -SkipBuild

.EXAMPLE
    # Install core tools only; skip WASM workload, SDL2, LibVLC, git-cliff, and butler
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

function Get-InstalledTemplatePackageVersion {
    $output = dotnet new uninstall 2>&1
    $lines = @($output | ForEach-Object { $_.ToString() })

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $packageLine = $lines[$i].Trim()
        if ($packageLine -match '^Gondwana\.Templates(?:\s*::\s*(.+?))?\s*$') {
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

function Test-TemplatesInstalled {
    return -not [string]::IsNullOrWhiteSpace((Get-InstalledTemplatePackageVersion))
}

function Test-Workload {
    param([string] $Id)
    $output = dotnet workload list 2>&1
    return (@($output | Where-Object { $_ -match "\b$([regex]::Escape($Id))\b" })).Count -gt 0
}

function Get-CommandVersionLine {
    param(
        [string] $Command,
        [string[]] $VersionArgs = @('--version')
    )

    try {
        return ((& $Command @VersionArgs 2>&1) | Select-Object -First 1).ToString().Trim()
    } catch {
        return $null
    }
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

Step '1/13  Git'
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git not found on PATH.`nInstall Git from https://git-scm.com and reopen your terminal."
}
OK "$(git --version)"

# ─── Step 2: .NET 8 SDK ───────────────────────────────────────────────────────

Step '2/13  .NET 8 SDK'
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

Step '3/13  Local .NET tools (nbgv)'
Push-Location $repoRoot
try {
    Invoke-Cmd dotnet @('tool', 'restore', '--no-cache')
    OK 'Local tools restored from latest available packages (nbgv ready).'
} finally {
    Pop-Location
}

# ─── Step 4: NuGet restore ────────────────────────────────────────────────────

Step '4/13  NuGet restore'
Invoke-Cmd dotnet @('restore', $solutionFile, '--nologo', '--force', '--no-cache',
                    '/p:Configuration=Release', '/p:EnableWindowsTargeting=true')
OK 'NuGet packages restored with dependency reevaluation.'

# ─── Step 5: Build ────────────────────────────────────────────────────────────

Step '5/13  Build'
if ($SkipBuild) {
    INFO 'Skipped (-SkipBuild).'
} else {
    Invoke-Cmd dotnet @('build', $solutionFile, '--configuration', 'Release',
                        '--no-restore', '--nologo', '/p:EnableWindowsTargeting=true')
    OK 'Solution built successfully.'
}

# ─── Step 6: Gondwana CLI ─────────────────────────────────────────────────────

Step '6/13  Gondwana CLI (Gondwana.Cli)'
if (Test-GlobalTool 'gondwana.cli') {
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # dotnet may print handled downgrade messages to stderr; keep processing based on exit code/text.
        $ErrorActionPreference = 'Continue'
        $updateOutput = & dotnet tool update --global Gondwana.Cli 2>&1
        $updateExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    $updateOutputText = ($updateOutput | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine

    if ($updateExitCode -eq 0) {
        if (-not [string]::IsNullOrWhiteSpace($updateOutputText)) {
            Write-Host $updateOutputText
        }
        $cliLine = (dotnet tool list -g 2>&1) | Where-Object { $_ -match 'gondwana\.cli' } | Select-Object -First 1
        OK "Installed and updated to latest available: $($cliLine.Trim())"
    } else {
        $isRequestedVersionLower = @($updateOutput | Where-Object {
            $_ -and $_.ToString() -match 'requested version .* lower than existing version'
        }).Count -gt 0

        if ($isRequestedVersionLower) {
            if (-not [string]::IsNullOrWhiteSpace($updateOutputText)) {
                Write-Host $updateOutputText
            }
            INFO 'Configured source offers an older Gondwana.Cli version than the one already installed; keeping current version.'
            $cliLine = (dotnet tool list -g 2>&1) | Where-Object { $_ -match 'gondwana\.cli' } | Select-Object -First 1
            if ($cliLine) {
                OK "Current global version retained: $($cliLine.Trim())"
            } else {
                OK 'Current global version retained.'
            }
        } else {
            throw "'dotnet tool update --global Gondwana.Cli' exited with code $updateExitCode.`n$updateOutputText"
        }
    }
} else {
    Invoke-Cmd dotnet @('tool', 'install', '--global', 'Gondwana.Cli')
    OK 'Gondwana.Cli installed.'
}

# ─── Step 7: Gondwana project templates ───────────────────────────────────────

Step '7/13  Gondwana project templates (Gondwana.Templates)'
$installedTemplateVersion = Get-InstalledTemplatePackageVersion
if (-not [string]::IsNullOrWhiteSpace($installedTemplateVersion)) {
    $updateOutput = & dotnet new update 2>&1
    $updateExitCode = $LASTEXITCODE
    $updateOutputText = ($updateOutput | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine

    if ($updateExitCode -ne 0) {
        throw "'dotnet new update' exited with code $updateExitCode.`n$updateOutputText"
    }

    if (-not [string]::IsNullOrWhiteSpace($updateOutputText)) {
        Write-Host $updateOutputText
    }

    $currentTemplateVersion = Get-InstalledTemplatePackageVersion
    if (-not [string]::IsNullOrWhiteSpace($currentTemplateVersion) -and
        $currentTemplateVersion -ne $installedTemplateVersion) {
        OK "Gondwana.Templates updated to latest available: $currentTemplateVersion"
    } elseif (-not [string]::IsNullOrWhiteSpace($currentTemplateVersion)) {
        OK "Installed templates checked; current version retained: $currentTemplateVersion"
    } else {
        OK 'Installed templates checked and kept at the current version.'
    }
} else {
    Invoke-Cmd dotnet @('new', 'install', 'Gondwana.Templates')
    $currentTemplateVersion = Get-InstalledTemplatePackageVersion
    if (-not [string]::IsNullOrWhiteSpace($currentTemplateVersion)) {
        OK "Gondwana.Templates installed: $currentTemplateVersion"
    } else {
        OK 'Gondwana.Templates installed.'
    }
}

# ─── Optional steps (8–12) ────────────────────────────────────────────────────

if ($SkipOptional) {
    Write-Host "`n   · Steps 8–12 skipped (-SkipOptional)."
} else {

    # ─── Step 8: WASM workload ────────────────────────────────────────────────

    Step '8/13  WASM workload (wasm-tools)'
    if (Test-Workload 'wasm-tools') {
        Invoke-Cmd dotnet @('workload', 'update')
        OK 'wasm-tools and other installed workloads updated to latest available versions.'
    } else {
        Invoke-Cmd dotnet @('workload', 'install', 'wasm-tools')
        OK 'wasm-tools workload installed.'
    }

    # ─── Step 9: SDL2 native library ──────────────────────────────────────────

    Step '9/13  SDL2 native library (Gondwana.Input.SDL2)'
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

    Step '10/13 LibVLC native library (Gondwana.Video)'
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

    # ─── Step 11: git-cliff ────────────────────────────────────────────────────

    Step '11/13 git-cliff'
    if (Get-Command git-cliff -ErrorAction SilentlyContinue) {
        if ($isWindowsOS -and (Get-Command winget -ErrorAction SilentlyContinue)) {
            $wingetUpgradeOutput = & winget upgrade --id orhun.git-cliff --exact --silent `
                --accept-source-agreements --accept-package-agreements 2>&1
            $wingetUpgradeExitCode = $LASTEXITCODE
            $wingetUpgradeText = ($wingetUpgradeOutput | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine

            $isNoUpgrade = ($wingetUpgradeExitCode -ne 0) -and (
                $wingetUpgradeText -match 'No available upgrade found' -or
                $wingetUpgradeText -match 'No newer package' -or
                $wingetUpgradeText -match 'No applicable update'
            )
            if ($wingetUpgradeExitCode -ne 0 -and -not $isNoUpgrade) {
                WARN "Could not auto-update git-cliff via winget: 'winget upgrade ...' exited with code $wingetUpgradeExitCode."
            }
        }

        $gitCliffVersion = Get-CommandVersionLine -Command 'git-cliff'
        OK ("git-cliff ready{0}" -f $(if ($gitCliffVersion) { ": $gitCliffVersion" } else { "." }))
    } else {
        if ($isWindowsOS -and (Get-Command winget -ErrorAction SilentlyContinue)) {
            try {
                Invoke-Cmd winget @('install', '--id', 'orhun.git-cliff', '--exact', '--silent',
                                    '--accept-source-agreements', '--accept-package-agreements')
                # Refresh PATH so the newly installed git-cliff is usable in this session
                $env:PATH = [System.Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' +
                            [System.Environment]::GetEnvironmentVariable('PATH', 'User')
                $gitCliffVersion = Get-CommandVersionLine -Command 'git-cliff'
                OK ("git-cliff installed{0}" -f $(if ($gitCliffVersion) { ": $gitCliffVersion" } else { "." }))
            } catch {
                WARN "winget install orhun.git-cliff failed: $_"
                INFO "Install git-cliff manually from https://git-cliff.org/"
            }
        } else {
            WARN "git-cliff not found on PATH."
            INFO "Install git-cliff from https://git-cliff.org/"
        }
    }

    # ─── Step 12: butler ───────────────────────────────────────────────────────

    Step '12/13 butler (itch.io)'
    if (Get-Command butler -ErrorAction SilentlyContinue) {
        $butlerVersion = Get-CommandVersionLine -Command 'butler'
        OK ("butler ready{0}" -f $(if ($butlerVersion) { ": $butlerVersion" } else { "." }))
    } else {
        # Determine the broth CDN platform slug and executable name.
        $isMacOSPlatform = if (Get-Variable -Name 'IsMacOS' -ErrorAction SilentlyContinue) { $IsMacOS } else { $false }
        # $env:PROCESSOR_ARCHITECTURE is always set on Windows (AMD64, ARM64, x86).
        # RuntimeInformation.ProcessArchitecture is only reliably available in PS Core 6+; avoid it in PS 5.1.
        $butlerArchitecture = if ($isWindowsOS -and $env:PROCESSOR_ARCHITECTURE) {
            switch ($env:PROCESSOR_ARCHITECTURE.ToUpperInvariant()) {
                'AMD64' { 'x64' }
                'ARM64' { 'arm64' }
                default { 'x86' }
            }
        } else {
            try {
                [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
            } catch {
                'x64'
            }
        }

        $butlerPlatform = if ($isWindowsOS) {
            if ($butlerArchitecture -ne 'x64') {
                WARN "No native butler package mapping is defined for Windows '$butlerArchitecture'; falling back to windows-amd64."
            }

            'windows-amd64'
        } elseif ($isMacOSPlatform) {
            switch ($butlerArchitecture) {
                'arm64' { 'darwin-arm64' }
                'x64'   { 'darwin-amd64' }
                default {
                    WARN "No native butler package mapping is defined for macOS '$butlerArchitecture'; falling back to darwin-amd64."
                    'darwin-amd64'
                }
            }
        } else {
            switch ($butlerArchitecture) {
                'arm64' { 'linux-arm64' }
                'x64'   { 'linux-amd64' }
                default {
                    WARN "No native butler package mapping is defined for Linux '$butlerArchitecture'; falling back to linux-amd64."
                    'linux-amd64'
                }
            }
        }

        $butlerExeName = if ($isWindowsOS) { 'butler.exe' } else { 'butler' }

        $butlerInstallDir = if ($isWindowsOS) {
            Join-Path (Join-Path $env:LOCALAPPDATA 'itch') 'butler'
        } else {
            Join-Path (Join-Path $HOME '.itch') 'butler'
        }

        $butlerUrls = @(
            "https://broth.itch.ovh/butler/$butlerPlatform/LATEST/archive/default",
            "https://broth.itch.zone/butler/$butlerPlatform/LATEST/archive/default"
        )
        $butlerZip = Join-Path ([System.IO.Path]::GetTempPath()) ("butler-install-{0}.zip" -f ([System.Guid]::NewGuid().ToString('N')))

        try {
            if (-not (Test-Path $butlerInstallDir)) {
                [System.IO.Directory]::CreateDirectory($butlerInstallDir) | Out-Null
            }

            $downloadSucceeded = $false
            $lastDownloadError = $null
            foreach ($butlerUrl in $butlerUrls) {
                INFO "butler not found — downloading from $butlerUrl ..."
                try {
                    if (Test-Path $butlerZip) { Remove-Item $butlerZip -Force -ErrorAction SilentlyContinue }
                    Invoke-WebRequest -Uri $butlerUrl -OutFile $butlerZip -UseBasicParsing
                    $downloadSucceeded = $true
                    break
                } catch {
                    $lastDownloadError = $_
                    WARN "Download failed from ${butlerUrl}: $($_.Exception.Message)"
                }
            }

            if (-not $downloadSucceeded) {
                throw "Could not download butler from any known source. Last error: $($lastDownloadError.Exception.Message)"
            }

            Expand-Archive -Path $butlerZip -DestinationPath $butlerInstallDir -Force

            # Set executable bit on non-Windows platforms.
            if (-not $isWindowsOS) {
                & chmod +x (Join-Path $butlerInstallDir $butlerExeName)
            }

            # Add to PATH for the current session so gondwana doctor can find it.
            $normalizedButlerInstallDir = $butlerInstallDir.Trim().Trim('"').TrimEnd('\', '/')
            $pathEntries = @()
            if (-not [string]::IsNullOrWhiteSpace($env:PATH)) {
                $pathEntries = $env:PATH.Split([System.IO.Path]::PathSeparator, [System.StringSplitOptions]::RemoveEmptyEntries)
            }

            $hasButlerInstallDir = $false
            foreach ($entry in $pathEntries) {
                $normalizedEntry = $entry.Trim().Trim('"').TrimEnd('\', '/')
                if (($isWindowsOS -and $normalizedEntry -ieq $normalizedButlerInstallDir) -or
                    (-not $isWindowsOS -and $normalizedEntry -ceq $normalizedButlerInstallDir)) {
                    $hasButlerInstallDir = $true
                    break
                }
            }

            if (-not $hasButlerInstallDir) {
                if ([string]::IsNullOrWhiteSpace($env:PATH)) {
                    $env:PATH = $butlerInstallDir
                } else {
                    $env:PATH = "$env:PATH$([System.IO.Path]::PathSeparator)$butlerInstallDir"
                }
            }

            $butlerVersion = Get-CommandVersionLine -Command 'butler'
            OK ("butler installed to $butlerInstallDir{0}" -f $(if ($butlerVersion) { ": $butlerVersion" } else { "." }))
            WARN "Add '$butlerInstallDir' to your PATH to use butler in future terminal sessions."
            INFO "Run 'butler login' to authenticate with itch.io."
        } catch {
            WARN "Failed to download/install butler: $_"
            INFO "Install butler manually from https://itch.io/docs/butler/installing.html"
        } finally {
            if (Test-Path $butlerZip) { Remove-Item $butlerZip -Force -ErrorAction SilentlyContinue }
        }
    }
}

# ─── Step 13: gondwana doctor ─────────────────────────────────────────────────

Step '13/13 gondwana doctor'
if (Get-Command gondwana -ErrorAction SilentlyContinue) {
    gondwana doctor
} else {
    WARN "'gondwana' command not found on PATH."
    INFO "If you just installed Gondwana.Cli, reopen your terminal and run 'gondwana doctor'."
}

Write-Host ""
Write-Host "Setup complete." -ForegroundColor Green
