#Requires -Version 5.1
<#
.SYNOPSIS
    Regenerates the root changelog's leading current section.

.DESCRIPTION
    Builds the root changelog section as a project-grouped summary of commits
    since the latest Git tag. Existing released history is preserved exactly.
    Without -Tag the derived section is [Unreleased]; with -Tag it becomes the
    supplied versioned release section.
#>
param(
    [string]$Tag,
    [switch]$PreviewOnly,
    [switch]$SectionOnly,
    [string]$ChangelogPath = "CHANGELOG.md",
    [string]$CliffConfigPath = "cliff.toml"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Item (Join-Path $PSScriptRoot '../..')).FullName
$groupsPath = Join-Path $PSScriptRoot "Changelog-ProjectGroups.ps1"
. $groupsPath

if (-not [System.IO.Path]::IsPathRooted($ChangelogPath)) {
    $ChangelogPath = Join-Path $repoRoot $ChangelogPath
}
if (-not [System.IO.Path]::IsPathRooted($CliffConfigPath)) {
    $CliffConfigPath = Join-Path $repoRoot $CliffConfigPath
}

if (-not (Test-Path $ChangelogPath)) {
    throw "The canonical root changelog does not exist at '$ChangelogPath'."
}
if (-not (Test-Path $CliffConfigPath)) {
    throw "Missing cliff.toml at '$CliffConfigPath'."
}
if (-not (Get-Command git-cliff -ErrorAction SilentlyContinue)) {
    throw "git-cliff was not found on PATH. Install with: winget install --id orhun.git-cliff"
}

function Get-GeneratedSectionBody {
    param([Parameter(Mandatory = $true)][string]$GeneratedChangelogPath)

    $content = Get-Content $GeneratedChangelogPath -Raw
    if ([string]::IsNullOrWhiteSpace($content)) {
        return ""
    }

    $heading = [regex]::Match(
        $content,
        '(?m)^#\s+(?:\[Unreleased\]|\[?v?\d+\.\d+\.\d+\]?).*$'
    )
    if (-not $heading.Success) {
        return ""
    }

    $bodyStart = $heading.Index + $heading.Length
    $nextHeading = [regex]::Match(
        $content.Substring($bodyStart),
        '(?m)^#\s+(?:\[Unreleased\]|\[?v?\d+\.\d+\.\d+\]?).*$'
    )
    $bodyLength = if ($nextHeading.Success) { $nextHeading.Index } else { $content.Length - $bodyStart }
    return $content.Substring($bodyStart, $bodyLength).Trim()
}

function Get-ProjectBody {
    param([Parameter(Mandatory = $true)][object]$ProjectGroup)

    $tempPath = Join-Path ([System.IO.Path]::GetTempPath()) `
        ("gondwana-root-changelog-" + [Guid]::NewGuid().ToString("N") + ".md")

    $arguments = @(
        "--config", $CliffConfigPath,
        "--repository", ".",
        "--unreleased",
        "--strip", "header"
    )
    if ($Tag) {
        $arguments += "--tag", $Tag
    }
    foreach ($includePath in $ProjectGroup.IncludePaths) {
        # Keep the glob attached to the option so PowerShell cannot expand it.
        $arguments += "--include-path=$includePath"
    }
    $arguments += "--output", $tempPath

    Push-Location $repoRoot
    try {
        & git-cliff @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "git-cliff failed for root changelog group '$($ProjectGroup.Name)'."
        }
        $body = Get-GeneratedSectionBody -GeneratedChangelogPath $tempPath
    }
    finally {
        Pop-Location
        if (Test-Path $tempPath) {
            Remove-Item $tempPath -Force
        }
    }

    if ([string]::IsNullOrWhiteSpace($body)) {
        return ""
    }

    # Project headings belong immediately below the release heading.
    return ($body -replace '(?m)^(#{1,5})\s+', '#$1 ')
}

function New-CurrentSection {
    $lines = @()
    if ($Tag) {
        $lines += "# [$Tag] - $(Get-Date -Format 'yyyy-MM-dd')"
    }
    else {
        $lines += "# [Unreleased]"
    }

    $hasChanges = $false
    foreach ($projectGroup in $ProjectChangelogGroups) {
        $body = Get-ProjectBody -ProjectGroup $projectGroup
        if ([string]::IsNullOrWhiteSpace($body)) {
            continue
        }

        $hasChanges = $true
        $lines += ""
        $lines += "## $($projectGroup.Name)"
        $lines += ""
        $lines += $body
    }

    if (-not $hasChanges) {
        return ""
    }

    if ($Tag) {
        $previousTag = git -C $repoRoot tag --sort=-version:refname |
            Where-Object { $_ -ne $Tag } |
            Select-Object -First 1
        if ($previousTag) {
            $lines += ""
            $lines += "Full Changelog: https://github.com/Isthimius/Gondwana/compare/$previousTag...$Tag"
        }
    }

    return ($lines -join "`n").Trim()
}

function Join-RootChangelog {
    param(
        [Parameter(Mandatory = $true)][string]$ExistingContent,
        [Parameter(Mandatory = $true)][string]$CurrentSection
    )

    $headingPattern = '(?m)^#\s+\[(?<Version>Unreleased|v?\d+\.\d+\.\d+)\](?:\s|$)'
    $matches = [regex]::Matches($ExistingContent, $headingPattern)
    if ($matches.Count -eq 0) {
        throw "No root changelog release heading was found in '$ChangelogPath'."
    }

    $prefix = $ExistingContent.Substring(0, $matches[0].Index)
    if ($matches[0].Groups['Version'].Value -eq 'Unreleased') {
        $releasedHistory = if ($matches.Count -gt 1) {
            $ExistingContent.Substring($matches[1].Index)
        }
        else {
            ""
        }
    }
    else {
        $releasedHistory = $ExistingContent.Substring($matches[0].Index)
    }

    $newLine = if ($ExistingContent.Contains("`r`n")) { "`r`n" } else { "`n" }
    $content = $prefix
    if (-not $content.EndsWith($newLine + $newLine)) {
        $content = $content.TrimEnd() + $newLine + $newLine
    }
    $content += ($CurrentSection -replace "`r?`n", $newLine)
    if (-not [string]::IsNullOrEmpty($releasedHistory)) {
        $content += $newLine + $newLine + $releasedHistory
    }
    else {
        $content += $newLine
    }
    return $content
}

$currentSection = New-CurrentSection
if ([string]::IsNullOrWhiteSpace($currentSection)) {
    throw "No root changelog entries were generated."
}

if ($SectionOnly) {
    Write-Output $currentSection
    exit 0
}

$existingContent = Get-Content $ChangelogPath -Raw
$finalContent = Join-RootChangelog -ExistingContent $existingContent -CurrentSection $currentSection

if ($PreviewOnly) {
    Write-Host $finalContent
    exit 0
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($ChangelogPath, $finalContent, $utf8NoBom)
Write-Host "Root changelog updated."
