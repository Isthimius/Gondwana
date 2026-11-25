#Requires -Version 5.1
<#
.SYNOPSIS
    Reformat Gondwana C# source files with JetBrains CleanupCode.

.DESCRIPTION
    Runs only the formatting profile, using the repository's .editorconfig.
    Installs the global JetBrains.ReSharper.GlobalTools package if jb is
    missing, unless -NoInstall is specified. No SDK workload, build, CI,
    or developer setup changes are required by this script.

.PARAMETER All
    Select all existing Git-tracked C# files (including templates outside the
    solution), excluding bin and obj directories.

.PARAMETER Files
    Select one or more literal C# file paths, relative to the repository root
    or absolute paths inside it. Untracked source files may be selected.

.PARAMETER NoInstall
    Fail rather than installing the global tool if jb is unavailable.

.EXAMPLE
    .\Tooling\scripts\Format-Code.ps1 -All

.EXAMPLE
    .\Tooling\scripts\Format-Code.ps1 -Files 'Gondwana/EngineState.Definitions.cs'

.EXAMPLE
    .\Tooling\scripts\Format-Code.ps1 -All -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'All')]
param(
    [Parameter(ParameterSetName = 'All')]
    [switch] $All,

    [Parameter(Mandatory = $true, ParameterSetName = 'Files')]
    [ValidateNotNullOrEmpty()]
    [string[]] $Files,

    [switch] $NoInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $All -and $PSCmdlet.ParameterSetName -ne 'Files') {
    throw 'Specify -All or -Files <path1>,<path2>. Use -WhatIf to preview the scope.'
}

$repoRoot = (Get-Item -LiteralPath (Join-Path $PSScriptRoot '../..')).FullName
$rootPrefix = $repoRoot.TrimEnd([char[]]@('/', '\')) + [IO.Path]::DirectorySeparatorChar
$isWindowsPlatform = $env:OS -eq 'Windows_NT'
$comparison = if ($isWindowsPlatform) {
    [StringComparison]::OrdinalIgnoreCase
} else {
    [StringComparison]::Ordinal
}
$comparer = if ($isWindowsPlatform) {
    [StringComparer]::OrdinalIgnoreCase
} else {
    [StringComparer]::Ordinal
}

function Find-CleanupCode {
    $command = Get-Command jb -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -ne $command) {
        return $command.Source
    }

    # A newly installed global tool may not yet be on the caller's PATH.
    $shimName = if ($isWindowsPlatform) { 'jb.exe' } else { 'jb' }
    $shim = Join-Path (Join-Path $HOME '.dotnet/tools') $shimName
    if (Test-Path -LiteralPath $shim -PathType Leaf) {
        return $shim
    }
}

Push-Location -LiteralPath $repoRoot
try {
    if ($All) {
        if (-not (Get-Command git -CommandType Application -ErrorAction SilentlyContinue)) {
            throw 'Git is required for -All.'
        }

        $candidates = @(git -c core.quotepath=false ls-files -- '*.cs')
        if ($LASTEXITCODE -ne 0) {
            throw "git ls-files failed (exit code $LASTEXITCODE)."
        }
    } else {
        $candidates = $Files
    }

    $seen = [Collections.Generic.HashSet[string]]::new($comparer)
    $targets = [Collections.Generic.List[string]]::new()
    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate) -or $candidate -match '[*?]') {
            throw "Expected a literal C# file path, not a wildcard: $candidate"
        }

        $absolute = if ([IO.Path]::IsPathRooted($candidate)) {
            [IO.Path]::GetFullPath($candidate)
        } else {
            [IO.Path]::GetFullPath((Join-Path $repoRoot $candidate))
        }

        if (-not $absolute.StartsWith($rootPrefix, $comparison) -or
            [IO.Path]::GetExtension($absolute) -ine '.cs') {
            throw "Expected a C# file inside the repository: $candidate"
        }

        $relative = $absolute.Substring($rootPrefix.Length)
        if ($relative -match '(^|[\\/])(bin|obj)([\\/]|$)') {
            if ($All) {
                continue
            }
            throw "Build output is not a cleanup target: $candidate"
        }

        if (-not (Test-Path -LiteralPath $absolute -PathType Leaf)) {
            if ($All) {
                # A tracked file may have been deleted in the working tree.
                continue
            }
            throw "File not found: $candidate"
        }

        if ($seen.Add($absolute)) {
            $targets.Add($absolute)
        }
    }

    if ($targets.Count -eq 0) {
        throw 'No C# files matched the requested cleanup scope.'
    }

    Write-Host "Selected $($targets.Count) C# file(s)."
    if (-not $PSCmdlet.ShouldProcess("$($targets.Count) C# file(s) in $repoRoot", 'Reformat with JetBrains CleanupCode (install jb if necessary)')) {
        return
    }

    $jb = Find-CleanupCode
    if (-not $jb) {
        if ($NoInstall) {
            throw 'jb was not found. Install JetBrains.ReSharper.GlobalTools or omit -NoInstall.'
        }
        if (-not (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) {
            throw 'The .NET SDK is required to install JetBrains.ReSharper.GlobalTools.'
        }

        Write-Host 'Installing JetBrains.ReSharper.GlobalTools as a global .NET tool...'
        & dotnet tool install --global JetBrains.ReSharper.GlobalTools
        if ($LASTEXITCODE -ne 0) {
            throw "JetBrains tool installation failed (exit code $LASTEXITCODE)."
        }

        $jb = Find-CleanupCode
        if (-not $jb) {
            throw 'jb was installed but could not be located. Check the global .NET tools directory and PATH.'
        }
    }

    # Direct-file mode reformats files without needing a solution build.
    # Batch paths to stay comfortably below Windows command-line length limits.
    $batch = [Collections.Generic.List[string]]::new()
    $batchLength = 0

    function Invoke-FormatBatch([string[]] $paths) {
        $arguments = @('cleanupcode', '--profile=Built-in: Reformat Code') + $paths
        & $jb @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "CleanupCode failed (exit code $LASTEXITCODE). Review any changes already made before retrying."
        }
    }

    foreach ($target in $targets) {
        if ($batch.Count -gt 0 -and ($batchLength + $target.Length + 3) -gt 20000) {
            Invoke-FormatBatch -paths ($batch.ToArray())
            $batch.Clear()
            $batchLength = 0
        }
        $batch.Add($target)
        $batchLength += $target.Length + 3
    }

    if ($batch.Count -gt 0) {
        Invoke-FormatBatch -paths ($batch.ToArray())
    }

    Write-Host 'Formatting complete. Review the changes with git diff.'
} finally {
    Pop-Location
}
