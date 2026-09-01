#Requires -Version 5.1
<#
.SYNOPSIS
    Generates per-project CHANGELOG.md files using git-cliff.

.DESCRIPTION
    Runs git-cliff once per library project, filtering commits by changed file
    paths via --include-path. Each project gets its own CHANGELOG.md placed in
    its project folder.

    Excluded from generation: Demos and test-only projects.

    Behaviour:
      - If the project's CHANGELOG.md is new/empty, the complete project history
        is generated. Existing Git tags become versioned sections and current
        untagged commits are emitted as [Unreleased], unless -Tag is supplied.
      - If the changelog already exists, released history is preserved and the
        leading generated current section is replaced from commits since the
        latest Git tag.
      - With -Tag, current unreleased commits are written under that version tag.
      - Without -Tag, current unreleased commits are written under [Unreleased].

.PARAMETER Tag
    Version tag to stamp on the current unreleased commits (e.g. "v1.2.3").
    When omitted, current unreleased commits remain under [Unreleased].

.PARAMETER PreviewOnly
    Print the final generated changelog output to the console instead of writing
    to disk. Nothing is modified on disk when this switch is set.

.PARAMETER Projects
    Override the default list of project folder paths (relative to repo root).
    Accepts an array of strings, e.g. @("Gondwana", "Gondwana.Audio.Midi").

.PARAMETER CliffConfigPath
    Path to the cliff.toml config file. Defaults to cliff.toml in the repo root.

.EXAMPLE
    # Refresh [Unreleased] sections for all library projects
    .\Generate-Project-Changelogs.ps1

.EXAMPLE
    # Preview the current generated changelogs without touching disk
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
# Default project list — all library/tooling projects (not Demos or tests).
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
    "Tooling/Gondwana.Mcp",
    "Tooling/Gondwana.Templates",
    "Tooling/Gondwana.Tooling.Assets.WinForms",
    "Tooling/Gondwana.Tooling.Studio.Avalonia",
    "Tooling/Gondwana.Tooling.Studio.Core",
    "Tooling/Gondwana.Tooling.Studio.WinForms",
    "Tooling/Gondwana.Tooling.Tilesheets.WinForms"
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

$releaseHeadingPattern = '(?m)^#\s+(?:\[Unreleased\]|\[?v?\d+\.\d+\.\d+)'
$releaseHeadingRegex = New-Object System.Text.RegularExpressions.Regex($releaseHeadingPattern)

function Test-HeadingMatchesTag {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Heading,

        [string]$CurrentTag
    )

    if ([string]::IsNullOrWhiteSpace($CurrentTag)) {
        return $false
    }

    $version = $CurrentTag -replace '^v', ''
    $escapedVersion = [regex]::Escape($version)
    return $Heading -match "^#\s+\[?v?$escapedVersion(?:\]|\s|$)"
}

function Get-ChangelogParts {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content,

        [string]$CurrentTag
    )

    if ([string]::IsNullOrWhiteSpace($Content)) {
        return [pscustomobject]@{
            Prefix          = ""
            CurrentSection  = ""
            ReleasedHistory = ""
        }
    }

    $matches = $releaseHeadingRegex.Matches($Content)
    if ($matches.Count -eq 0) {
        return [pscustomobject]@{
            Prefix          = $Content.Trim()
            CurrentSection  = ""
            ReleasedHistory = ""
        }
    }

    $first = $matches[0]
    $prefix = $Content.Substring(0, $first.Index).Trim()
    $firstIsCurrent = ($first.Value -match '\[Unreleased\]') -or
                      (Test-HeadingMatchesTag -Heading $first.Value -CurrentTag $CurrentTag)

    if (-not $firstIsCurrent) {
        return [pscustomobject]@{
            Prefix          = $prefix
            CurrentSection  = ""
            ReleasedHistory = $Content.Substring($first.Index).Trim()
        }
    }

    if ($matches.Count -gt 1) {
        $second = $matches[1]
        $currentSection = $Content.Substring($first.Index, $second.Index - $first.Index).Trim()
        $releasedHistory = $Content.Substring($second.Index).Trim()
    }
    else {
        $currentSection = $Content.Substring($first.Index).Trim()
        $releasedHistory = ""
    }

    return [pscustomobject]@{
        Prefix          = $prefix
        CurrentSection  = $currentSection
        ReleasedHistory = $releasedHistory
    }
}

function Join-ChangelogParts {
    param(
        [AllowEmptyString()]
        [string]$Prefix,

        [AllowEmptyString()]
        [string]$CurrentSection,

        [AllowEmptyString()]
        [string]$ReleasedHistory
    )

    $parts = @()

    if (-not [string]::IsNullOrWhiteSpace($Prefix)) {
        $parts += $Prefix.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($CurrentSection)) {
        $parts += $CurrentSection.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($ReleasedHistory)) {
        $parts += $ReleasedHistory.Trim()
    }

    if ($parts.Count -eq 0) {
        return ""
    }

    $separator = [Environment]::NewLine + [Environment]::NewLine
    return (($parts -join $separator).TrimEnd() + [Environment]::NewLine)
}

function Invoke-GitCliffToContent {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$Project
    )

    $tempChangelog = [System.IO.Path]::GetTempFileName()
    try {
        & git-cliff @Arguments "--output" $tempChangelog
        if ($LASTEXITCODE -ne 0) {
            throw "git-cliff failed for project '$Project' (exit $LASTEXITCODE)."
        }

        $content = Get-Content $tempChangelog -Raw
        if ($null -eq $content) {
            return ""
        }

        return $content
    }
    finally {
        if (Test-Path $tempChangelog) {
            Remove-Item $tempChangelog -Force
        }
    }
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

    $baseCliffArgs = @(
        "--config", $CliffConfigPath,
        "--repository", $repoRoot,
        "--include-path", $includePath
    )

    try {
        if ($changelogIsNew) {
            # Bootstrap from the complete project history. Parse and recompose
            # git-cliff's output immediately so the first run has the exact same
            # structure as all later incremental refreshes.
            $cliffArgs = @($baseCliffArgs)
            if ($Tag) {
                $cliffArgs += "--tag", $Tag
            }

            $generatedContent = Invoke-GitCliffToContent -Arguments $cliffArgs -Project $project
            $generatedParts = Get-ChangelogParts -Content $generatedContent -CurrentTag $Tag

            $finalContent = Join-ChangelogParts `
                -Prefix $generatedParts.Prefix `
                -CurrentSection $generatedParts.CurrentSection `
                -ReleasedHistory $generatedParts.ReleasedHistory
        }
        else {
            # Existing changelog: released history is authoritative. Strip only
            # a leading generated [Unreleased] section (or the same -Tag section
            # from a repeated release attempt), generate the current range into a
            # separate buffer, then compose the document ourselves. Do not use
            # git-cliff --prepend: it can inject a second document header.
            $existingContent = Get-Content $changelogPath -Raw
            if ($null -eq $existingContent) {
                $existingContent = ""
            }

            $existingParts = Get-ChangelogParts -Content $existingContent -CurrentTag $Tag

            $cliffArgs = @($baseCliffArgs) + "--unreleased"
            if ($Tag) {
                $cliffArgs += "--tag", $Tag
            }

            $generatedContent = Invoke-GitCliffToContent -Arguments $cliffArgs -Project $project
            $generatedParts = Get-ChangelogParts -Content $generatedContent -CurrentTag $Tag

            if ([string]::IsNullOrWhiteSpace($generatedParts.CurrentSection)) {
                throw "git-cliff did not generate a current changelog section for project '$project'."
            }

            $prefix = $existingParts.Prefix
            if ([string]::IsNullOrWhiteSpace($prefix)) {
                $prefix = $generatedParts.Prefix
            }

            $finalContent = Join-ChangelogParts `
                -Prefix $prefix `
                -CurrentSection $generatedParts.CurrentSection `
                -ReleasedHistory $existingParts.ReleasedHistory
        }

        if ($PreviewOnly) {
            Write-Host "  (preview only)"
            Write-Host $finalContent
        }
        else {
            Write-Utf8NoBom -Path $changelogPath -Content $finalContent
            Write-Host "  Written."
        }
    }
    catch {
        Write-Warning $_.Exception.Message
        $failed += $project
    }
}

Write-Host ""

if ($failed.Count -gt 0) {
    throw "git-cliff failed for the following projects: $($failed -join ', ')"
}

Write-Host "Done. Per-project changelogs generated."
