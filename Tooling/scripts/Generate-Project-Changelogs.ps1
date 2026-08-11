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

    Behaviour matches release.ps1:
      - If the project's CHANGELOG.md is new/empty, --output is used so the
        header block is written.
      - If it already exists, --prepend is used to prepend only the new section.

.PARAMETER Tag
    Version tag to pass to git-cliff (e.g. "v1.2.3"). When omitted, git-cliff
    uses the most recent tag it can find; combine with -Unreleased to preview
    unreleased changes without a tag.

.PARAMETER Unreleased
    Pass --unreleased to git-cliff so only commits since the last tag are shown.

.PARAMETER PreviewOnly
    Print the generated changelog section to the console instead of writing to
    disk. Nothing is modified on disk when this switch is set.

.PARAMETER Projects
    Override the default list of project folder paths (relative to repo root).
    Accepts an array of strings, e.g. @("Gondwana", "Gondwana.Audio.Midi").

.PARAMETER CliffConfigPath
    Path to the cliff.toml config file. Defaults to cliff.toml in the repo root.

.EXAMPLE
    # Generate changelogs for all library projects for the current unreleased commits
    .\Generate-Project-Changelogs.ps1 -Unreleased

.EXAMPLE
    # Preview what would be written for a specific tag, without touching disk
    .\Generate-Project-Changelogs.ps1 -Tag v1.2.3 -Unreleased -PreviewOnly

.EXAMPLE
    # Generate changelogs for a subset of projects only
    .\Generate-Project-Changelogs.ps1 -Projects @("Gondwana", "Gondwana.WinForms") -Unreleased
#>
param(
    [string]$Tag,
    [switch]$Unreleased,
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
# Solution Items/scripts/ → Solution Items/ → root
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

$failed = @()

foreach ($project in $Projects) {
    $projectFolder = Join-Path $repoRoot $project
    if (-not (Test-Path $projectFolder)) {
        Write-Warning "Project folder not found, skipping: $projectFolder"
        continue
    }

    $includePath = "$($project -replace '\\', '/')/**/*"
    $changelogPath = Join-Path $projectFolder "CHANGELOG.md"

    Write-Host ""
    Write-Host "--- $project ---"
    Write-Host "  include-path : $includePath"
    Write-Host "  changelog    : $changelogPath"

    # Build the git-cliff argument list.
    $cliffArgs = @(
        "--config", $CliffConfigPath,
        "--repository", $repoRoot,
        "--include-path", $includePath
    )

    if ($Tag) {
        $cliffArgs += "--tag", $Tag
    }

    if ($Unreleased) {
        $cliffArgs += "--unreleased"
    }

    if ($PreviewOnly) {
        # Write to stdout; nothing touches disk.
        Write-Host "  (preview only)"
        & git-cliff @cliffArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "git-cliff failed for project '$project' (exit $LASTEXITCODE)."
            $failed += $project
        }
    }
    else {
        $changelogIsNew = (-not (Test-Path $changelogPath)) -or ((Get-Item $changelogPath).Length -eq 0)

        if ($changelogIsNew) {
            # First-ever changelog: --output so the "# Changelog" header is written.
            & git-cliff @cliffArgs "--output" $changelogPath
        }
        else {
            # Existing changelog: prepend only the new unreleased section.
            & git-cliff @cliffArgs "--prepend" $changelogPath
        }

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "git-cliff failed for project '$project' (exit $LASTEXITCODE)."
            $failed += $project
        }
        else {
            Write-Host "  Written."
        }
    }
}

Write-Host ""

if ($failed.Count -gt 0) {
    throw "git-cliff failed for the following projects: $($failed -join ', ')"
}

Write-Host "Done. Per-project changelogs generated."
