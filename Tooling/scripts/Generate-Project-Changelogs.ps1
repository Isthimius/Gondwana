#Requires -Version 5.1
<#
.SYNOPSIS
    Generates per-project CHANGELOG.md files using git-cliff.

.DESCRIPTION
    Runs git-cliff once per library project, filtering commits by changed file
    paths via --include-path. Each project gets its own CHANGELOG.md placed in
    its project folder. This is the standard monorepo approach described in the
    git-cliff docs.

    Excluded from generation: Demos, Gondwana.Tests (test-only project).

    Behaviour:
      - If the project's CHANGELOG.md is new/empty, the complete project history
        is generated. Existing Git tags become versioned sections and current
        untagged commits are emitted as [Unreleased], unless -Tag is supplied.
      - If the changelog already exists, a leading [Unreleased] section from a
        previous run is removed and regenerated from commits since the latest tag.
      - With -Tag, current unreleased commits are written under that version tag.
      - Without -Tag, current unreleased commits are written under [Unreleased].

.PARAMETER Tag
    Version tag to stamp on the current unreleased commits (e.g. "v1.2.3").
    When omitted, current unreleased commits remain under [Unreleased].

.PARAMETER PreviewOnly
    Print the generated changelog output to the console instead of writing to
    disk. Nothing is modified on disk when this switch is set.

.PARAMETER Projects
    Override the default list of project folder paths (relative to repo root).
    Accepts an array of strings, e.g. @("Gondwana", "Gondwana.Audio.Midi").

.PARAMETER CliffConfigPath
    Path to the cliff.toml config file. Defaults to cliff.toml in the repo root.

.EXAMPLE
    # Refresh [Unreleased] sections for all library projects
    .\Generate-Project-Changelogs.ps1

.EXAMPLE
    # Preview the current unreleased changes without touching disk
    .\Generate-Project-Changelogs.ps1 -PreviewOnly

.EXAMPLE
    # Convert the current unreleased changes to a versioned release section
    .\Generate-Project-Changelogs.ps1 -Tag v1.2.3

.EXAMPLE
    # Generate changelogs for a subset of projects only
    .\Generate-Project-Changelogs.ps1 -Projects @("Gondwana", "Gondwana.WinForms")
#>
param(
    [string]$Tag,
    [switch]$PreviewOnly,
    [string[]]$Projects,
    [string]$CliffConfigPath = "cliff.toml"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Default project list — all library projects (not Demos or Gondwana.Tests).
# Paths are relative to the repo root.
# ---------------------------------------------------------------------------
$DefaultProjects = @(
    "Gondwana",
    "Gondwana.Audio.Browser",
    "Gondwana.Audio.Midi",
    "Gondwana.Avalonia",
    "Gondwana.Avalonia.Hosting",
    "Gondwana.Blazor",
    "Gondwana.Blazor.Hosting",
    "Gondwana.Hosting",
    "Gondwana.Input.SDL2",
    "Gondwana.Video",
    "Gondwana.Widgets",
    "Gondwana.WinForms",
    "Gondwana.WinForms.Hosting",
    "Tooling/Gondwana.Cli",
    "Tooling/Gondwana.Tooling.Studio",
    "Tooling/Gondwana.Templates",
    "Tooling/Gondwana.Tooling.Assets.WinForms"
)

if (-not $Projects) {
    $Projects = $DefaultProjects
}

# Resolve the repo root from the script's location:
# Tooling/scripts/ -> Tooling/ -> root
$repoRoot = (Get-Item (Join-Path $PSScriptRoot '../..')).FullName

if (-not [System.IO.Path]::IsPathRooted($CliffConfigPath)) {
    $CliffConfigPath = Join-Path $repoRoot $CliffConfigPath
}

if (-not (Test-Path $CliffConfigPath)) {
    throw "Missing cliff.toml at '$CliffConfigPath'. Ensure it exists in the repository root."
}

if (-not (Get-Command git-cliff -ErrorAction SilentlyContinue)) {
    throw "git-cliff was not found on PATH. Install with: winget install --id orhun.git-cliff"
}

function Remove-LeadingUnreleasedSection {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content
    )

    if ([string]::IsNullOrWhiteSpace($Content)) {
        return $Content
    }

    # Only remove [Unreleased] when it is the first release-like top-level
    # heading. This preserves released history even if unusual text appears
    # elsewhere in the document.
    $releaseHeadingPattern = '(?m)^#\s+(?:\[Unreleased\]|\[?v?\d+\.\d+\.\d+)'
    $firstReleaseHeading = [regex]::Match($Content, $releaseHeadingPattern)

    if (-not $firstReleaseHeading.Success -or $firstReleaseHeading.Value -notmatch '\[Unreleased\]') {
        return $Content
    }

    $versionHeadingPattern = '(?m)^#\s+\[?v?\d+\.\d+\.\d+'
    $nextVersionHeading = [regex]::Match(
        $Content,
        $versionHeadingPattern,
        $firstReleaseHeading.Index + $firstReleaseHeading.Length
    )

    $before = $Content.Substring(0, $firstReleaseHeading.Index).TrimEnd()

    if ($nextVersionHeading.Success) {
        $after = $Content.Substring($nextVersionHeading.Index).TrimStart()
    }
    else {
        $after = ""
    }

    if ([string]::IsNullOrWhiteSpace($before)) {
        return $after
    }

    if ([string]::IsNullOrWhiteSpace($after)) {
        return $before + [Environment]::NewLine
    }

    return $before + [Environment]::NewLine + [Environment]::NewLine + $after
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content
    )

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

$failed = @()

foreach ($project in $Projects) {
    $projectFolder = Join-Path $repoRoot $project
    if (-not (Test-Path $projectFolder)) {
        Write-Warning "Project folder not found, skipping: $projectFolder"
        continue
    }

    $includePath = "$($project -replace '\\', '/')/**/*"
    $changelogPath = Join-Path $projectFolder "CHANGELOG.md"
    $changelogIsNew = (-not (Test-Path $changelogPath)) -or ((Get-Item $changelogPath).Length -eq 0)

    if ($changelogIsNew) {
        $mode = "bootstrap full history"
    }
    elseif ($Tag) {
        $mode = "release $Tag"
    }
    else {
        $mode = "refresh [Unreleased]"
    }

    Write-Host ""
    Write-Host "--- $project ---"
    Write-Host "  include-path : $includePath"
    Write-Host "  changelog    : $changelogPath"
    Write-Host "  mode         : $mode"

    $cliffArgs = @(
        "--config", $CliffConfigPath,
        "--repository", $repoRoot,
        "--include-path", $includePath
    )

    if ($changelogIsNew) {
        # No changelog exists yet: build the complete history. git-cliff includes
        # current untagged commits as [Unreleased] by default; supplying --tag
        # stamps those commits with the requested release version instead.
        if ($Tag) {
            $cliffArgs += "--tag", $Tag
        }

        if ($PreviewOnly) {
            Write-Host "  (preview only)"
            & git-cliff @cliffArgs
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "git-cliff failed for project '$project' (exit $LASTEXITCODE)."
                $failed += $project
            }
            continue
        }

        $tempChangelog = [System.IO.Path]::GetTempFileName()
        try {
            & git-cliff @cliffArgs "--output" $tempChangelog
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "git-cliff failed for project '$project' (exit $LASTEXITCODE)."
                $failed += $project
                continue
            }

            $generatedContent = Get-Content $tempChangelog -Raw
            if ($null -eq $generatedContent) {
                $generatedContent = ""
            }

            Write-Utf8NoBom -Path $changelogPath -Content $generatedContent
            Write-Host "  Written."
        }
        finally {
            if (Test-Path $tempChangelog) {
                Remove-Item $tempChangelog -Force
            }
        }

        continue
    }

    # Existing changelog: released history is authoritative. Regenerate only
    # the current range since the latest tag and replace any previous top
    # [Unreleased] section instead of stacking duplicate generated sections.
    $cliffArgs += "--unreleased"
    if ($Tag) {
        $cliffArgs += "--tag", $Tag
    }

    if ($PreviewOnly) {
        Write-Host "  (preview only)"
        & git-cliff @cliffArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "git-cliff failed for project '$project' (exit $LASTEXITCODE)."
            $failed += $project
        }
        continue
    }

    $existingContent = Get-Content $changelogPath -Raw
    if ($null -eq $existingContent) {
        $existingContent = ""
    }

    $baseContent = Remove-LeadingUnreleasedSection -Content $existingContent
    $tempChangelog = [System.IO.Path]::GetTempFileName()

    try {
        Write-Utf8NoBom -Path $tempChangelog -Content $baseContent

        & git-cliff @cliffArgs "--prepend" $tempChangelog
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "git-cliff failed for project '$project' (exit $LASTEXITCODE)."
            $failed += $project
            continue
        }

        $generatedContent = Get-Content $tempChangelog -Raw
        if ($null -eq $generatedContent) {
            $generatedContent = ""
        }

        Write-Utf8NoBom -Path $changelogPath -Content $generatedContent
        Write-Host "  Written."
    }
    finally {
        if (Test-Path $tempChangelog) {
            Remove-Item $tempChangelog -Force
        }
    }
}

Write-Host ""

if ($failed.Count -gt 0) {
    throw "git-cliff failed for the following projects: $($failed -join ', ')"
}

Write-Host "Done. Per-project changelogs generated."
