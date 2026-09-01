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

$releaseHeadingPattern = '(?m)^#\s+(?<Version>\[Unreleased\]|v?\d+\.\d+\.\d+)(?:\s|$)'
$releaseHeadingRegex = New-Object System.Text.RegularExpressions.Regex($releaseHeadingPattern)

function Split-ExistingChangelog {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content
    )

    $releaseMatches = $releaseHeadingRegex.Matches($Content)
    if ($releaseMatches.Count -eq 0) {
        return [pscustomobject]@{
            Prefix          = $Content
            ReleasedHistory = ""
        }
    }

    $first = $releaseMatches[0]
    $prefix = $Content.Substring(0, $first.Index)

    if ($first.Groups["Version"].Value -eq "[Unreleased]") {
        if ($releaseMatches.Count -gt 1) {
            $releasedHistory = $Content.Substring($releaseMatches[1].Index)
        }
        else {
            $releasedHistory = ""
        }
    }
    else {
        $releasedHistory = $Content.Substring($first.Index)
    }

    return [pscustomobject]@{
        Prefix          = $prefix
        ReleasedHistory = $releasedHistory
    }
}

function Join-ChangelogContent {
    param(
        [AllowEmptyString()]
        [string]$Prefix,

        [AllowEmptyString()]
        [string]$CurrentSection,

        [AllowEmptyString()]
        [string]$ReleasedHistory
    )

    if ($Prefix.Contains("`r`n") -or $ReleasedHistory.Contains("`r`n")) {
        $newLine = "`r`n"
    }
    else {
        $newLine = "`n"
    }

    # Prefix and released history come from the existing changelog and are
    # deliberately appended without trimming or normalization. Only the
    # generated section and the boundaries around it are derived state.
    $result = $Prefix
    if (-not [string]::IsNullOrEmpty($result)) {
        if ($result.EndsWith($newLine + $newLine)) {
            # The prefix already has the desired blank line.
        }
        elseif ($result.EndsWith($newLine)) {
            $result += $newLine
        }
        else {
            $result += $newLine + $newLine
        }
    }

    $result += $CurrentSection.Trim()

    if (-not [string]::IsNullOrEmpty($ReleasedHistory)) {
        $result += $newLine + $newLine + $ReleasedHistory
        return $result
    }

    return ($result + $newLine)
}

function Normalize-GeneratedContent {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content
    )

    if ([string]::IsNullOrWhiteSpace($Content)) {
        return ""
    }

    $newLine = if ($Content.Contains("`r`n")) { "`r`n" } else { "`n" }
    $releaseBoundaryPattern = '(?:\r\n|\n|\r){3,}(?=^#\s+(?:\[Unreleased\]|v?\d+\.\d+\.\d+)(?:\s|$))'
    $normalized = [regex]::Replace(
        $Content.TrimEnd(),
        $releaseBoundaryPattern,
        $newLine + $newLine,
        [System.Text.RegularExpressions.RegexOptions]::Multiline
    )

    return ($normalized + $newLine)
}

function Invoke-GitCliffToContent {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$Project
    )

    $tempChangelog = Join-Path `
        ([System.IO.Path]::GetTempPath()) `
        ("gondwana-changelog-" + [Guid]::NewGuid().ToString("N") + ".md")

    Push-Location $repoRoot
    try {
        try {
            & git-cliff @Arguments "--output" $tempChangelog
            if ($LASTEXITCODE -ne 0) {
                throw "git-cliff failed for project '$Project' (exit $LASTEXITCODE)."
            }

            $content = Get-Content $tempChangelog -Raw
            if ($null -eq $content) {
                return ""
            }

            return (Normalize-GeneratedContent -Content $content)
        }
        finally {
            if (Test-Path $tempChangelog) {
                Remove-Item $tempChangelog -Force
            }
        }
    }
    finally {
        Pop-Location
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
    $existingContent = if (Test-Path $changelogPath) {
        Get-Content $changelogPath -Raw
    }
    else {
        ""
    }
    $changelogIsNew = [string]::IsNullOrWhiteSpace($existingContent)

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
        "--repository", ".",
        # Keep the glob in the same argv token as the option. PowerShell expands
        # a splatted bare glob into matching working-tree paths before invoking
        # a native command, which prevents git-cliff from grouping commits by tag.
        "--include-path=$includePath"
    )

    try {
        if ($changelogIsNew) {
            # For bootstrap, git-cliff already has exactly the required behavior:
            # full tagged history plus [Unreleased], or the supplied -Tag.
            $cliffArgs = @($baseCliffArgs)
            if ($Tag) {
                $cliffArgs += "--tag", $Tag
            }

            $finalContent = Invoke-GitCliffToContent -Arguments $cliffArgs -Project $project
        }
        else {
            # Existing released content is immutable. Split away only a leading
            # [Unreleased] section, generate its replacement without git-cliff's
            # document header, and put the untouched released history back.
            $existingParts = Split-ExistingChangelog -Content $existingContent

            $cliffArgs = @($baseCliffArgs) + @("--unreleased", "--strip", "header")
            if ($Tag) {
                $cliffArgs += "--tag", $Tag
            }

            $currentSection = Invoke-GitCliffToContent -Arguments $cliffArgs -Project $project
            if ([string]::IsNullOrWhiteSpace($currentSection)) {
                throw "git-cliff did not generate a current changelog section for project '$project'."
            }

            $finalContent = Join-ChangelogContent `
                -Prefix $existingParts.Prefix `
                -CurrentSection $currentSection `
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
