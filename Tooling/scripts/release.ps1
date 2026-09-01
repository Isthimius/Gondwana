param(
    [string]$Remote = "origin",
    [string]$RequiredBranch = "master",
    [string]$ChangelogPath = "CHANGELOG.md",
    [string]$CliffConfigPath = "cliff.toml",
    [switch]$PreviewOnly
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Project changelog groups — one entry per project/area.
# Each group collects commits that touched any of its IncludePaths.
# Duplicates across groups are intentional; a commit touching both
# Gondwana/**/* and Tooling/Gondwana.Cli/**/* appears under both.
# ---------------------------------------------------------------------------
$ProjectChangelogGroups = @(
    [pscustomobject]@{
        Name         = "Gondwana"
        IncludePaths = @("Gondwana/**/*")
    },
    [pscustomobject]@{
        Name         = "Gondwana.Audio.Browser"
        IncludePaths = @("Gondwana.Audio.Browser/**/*")
    },
    [pscustomobject]@{
        Name         = "Gondwana.Audio.Midi"
        IncludePaths = @("Gondwana.Audio.Midi/**/*")
    },
    [pscustomobject]@{
        Name         = "Gondwana.Avalonia"
        IncludePaths = @("Gondwana.Avalonia/**/*")
    },
    [pscustomobject]@{
        Name         = "Gondwana.Avalonia.Hosting"
        IncludePaths = @("Gondwana.Avalonia.Hosting/**/*")
    },
    [pscustomobject]@{
        Name         = "Gondwana.Blazor"
        IncludePaths = @("Gondwana.Blazor/**/*")
    },
    [pscustomobject]@{
        Name         = "Gondwana.Blazor.Hosting"
        IncludePaths = @("Gondwana.Blazor.Hosting/**/*")
    },
    [pscustomobject]@{
        Name         = "Gondwana.Hosting"
        IncludePaths = @("Gondwana.Hosting/**/*")
    },
    [pscustomobject]@{
        Name         = "Gondwana.Input.SDL2"
        IncludePaths = @("Gondwana.Input.SDL2/**/*")
    },
    [pscustomobject]@{
        Name         = "Gondwana.Video"
        IncludePaths = @("Gondwana.Video/**/*")
    },
    [pscustomobject]@{
        Name         = "Gondwana.Widgets"
        IncludePaths = @("Gondwana.Widgets/**/*")
    },
    [pscustomobject]@{
        Name         = "Gondwana.WinForms"
        IncludePaths = @("Gondwana.WinForms/**/*")
    },
    [pscustomobject]@{
        Name         = "Gondwana.WinForms.Hosting"
        IncludePaths = @("Gondwana.WinForms.Hosting/**/*")
    },
    [pscustomobject]@{
        Name         = "Tooling / Gondwana.Cli"
        IncludePaths = @("Tooling/Gondwana.Cli/**/*")
    },
    [pscustomobject]@{
        Name         = "Tooling / Gondwana.Tooling.Studio"
        IncludePaths = @("Tooling/Gondwana.Tooling.Studio/**/*")
    },
    [pscustomobject]@{
        Name         = "Tooling / Gondwana.Templates"
        IncludePaths = @("Tooling/Gondwana.Templates/**/*")
    },
    [pscustomobject]@{
        Name         = "Tooling / Gondwana.Tooling.Assets.WinForms"
        IncludePaths = @("Tooling/Gondwana.Tooling.Assets.WinForms/**/*")
    },
    [pscustomobject]@{
        Name         = "Build / Repository"
        IncludePaths = @(
            ".github/**/*",
            "Solution Items/**/*",
            "Directory.Build.props",
            "Directory.Build.targets",
            "Directory.Packages.props",
            "version.json",
            "global.json",
            "NuGet.config",
            "cliff.toml",
            ".editorconfig",
            ".gitignore",
            "*.sln",
            "README.md"
        )
    }
)

function Require-Command {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [string]$InstallHint
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        if ([string]::IsNullOrWhiteSpace($InstallHint)) {
            throw "Required command '$Name' was not found on PATH."
        }

        throw "Required command '$Name' was not found on PATH. $InstallHint"
    }
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Args
    )

    & git @Args
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($Args -join ' ')"
    }
}

function Get-NbgvPackageVersion {
    $versionInfo = nbgv get-version -f json | ConvertFrom-Json
    $version = $versionInfo.NuGetPackageVersion

    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Could not determine NuGetPackageVersion from nbgv."
    }

    return $version
}

function Invoke-UnitTests {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    $testProjectPath = Join-Path $RepoRoot "Testing/Gondwana.Tests/Gondwana.Tests.csproj"
    if (-not (Test-Path $testProjectPath)) {
        throw "Expected test project was not found at $testProjectPath."
    }

    Write-Host "Running Gondwana.Tests unit tests..."
    & dotnet test $testProjectPath --configuration Release --nologo /p:EnableWindowsTargeting=true
    if ($LASTEXITCODE -ne 0) {
        throw "Gondwana.Tests unit tests failed. Aborting deployment."
    }
}

function Invoke-ProjectChangelogGeneration {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TagName,

        [Parameter(Mandatory = $true)]
        [string]$CliffConfigPath
    )

    $projectChangelogScript = Join-Path $PSScriptRoot "Generate-Project-Changelogs.ps1"
    if (-not (Test-Path $projectChangelogScript)) {
        throw "Project changelog script not found at '$projectChangelogScript'."
    }

    Write-Host ""
    Write-Host "Updating per-project changelogs..."

    & $projectChangelogScript `
        -Tag $TagName `
        -CliffConfigPath $CliffConfigPath

    if ($LASTEXITCODE -ne 0) {
        throw "Generate-Project-Changelogs.ps1 failed."
    }
}

Require-Command git "Install Git for Windows, then reopen your terminal."
Require-Command dotnet "Install .NET SDK 8.0+, then reopen your terminal."
Require-Command nbgv "Install with: dotnet tool install -g nbgv"
Require-Command git-cliff "Install with: winget install --id orhun.git-cliff"

# Resolve relative paths against the repo root (two levels above this script:
# Solution Items/scripts/ → root) so the script works correctly when invoked
# from any working directory inside (or outside) the repo.
$repoRoot = (Get-Item (Join-Path $PSScriptRoot '../..')).FullName
if (-not [System.IO.Path]::IsPathRooted($CliffConfigPath)) {
    $CliffConfigPath = Join-Path $repoRoot $CliffConfigPath
}
if (-not [System.IO.Path]::IsPathRooted($ChangelogPath)) {
    $ChangelogPath = Join-Path $repoRoot $ChangelogPath
}

if (-not (Test-Path $CliffConfigPath)) {
    throw "Missing $CliffConfigPath. Add cliff.toml to the repository root before releasing."
}

# Ensure we're inside a git repo.
git rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -ne 0) {
    throw "This script must be run inside a git repository."
}

# ----------------------------------------
# PRE-FLIGHT CHECKS
# ----------------------------------------

# Ensure correct branch (skipped in -PreviewOnly mode to allow previewing from any branch).
$currentBranch = git branch --show-current
if (-not $PreviewOnly -and $currentBranch -ne $RequiredBranch) {
    throw "You must be on '$RequiredBranch' to create a release tag. Current branch: $currentBranch"
}

# Refresh remote branch/tag state before checks.
$fetchArgs = "fetch", "--prune", "--tags", "--force", $Remote
Invoke-Git -Args $fetchArgs

# Ensure local branch is not behind remote (skipped in -PreviewOnly mode).
if (-not $PreviewOnly) {
    $localHead = git rev-parse HEAD
    $remoteHead = git rev-parse "$Remote/$RequiredBranch"
    if ($localHead -ne $remoteHead) {
        throw "Local '$RequiredBranch' is not aligned with '$Remote/$RequiredBranch'. Pull/rebase first, then retry."
    }
}

# Ensure clean working tree before generating the changelog.
git diff --quiet
if ($LASTEXITCODE -ne 0) {
    throw "Working tree has unstaged changes. Commit or stash them first."
}

git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    throw "Working tree has staged but uncommitted changes. Commit or unstage them first."
}

Invoke-UnitTests -RepoRoot $repoRoot

$version = Get-NbgvPackageVersion
$tagName = "v$version"

Write-Host ""
Write-Host "Resolved version: $version"
Write-Host "Resolved tag: $tagName"
Write-Host ""

# ---------------------------------------------------------------------------
# CHANGELOG HELPER FUNCTIONS
# ---------------------------------------------------------------------------

function Get-GeneratedReleaseBody {
    param(
        [Parameter(Mandatory = $true)]
        [string]$GeneratedChangelogPath
    )

    $lines = @(Get-Content $GeneratedChangelogPath)

    if ($lines.Count -eq 0) {
        return ""
    }

    # Find the first release heading.
    # Handles formats like:
    #   # v1.2.3
    #   # [v1.2.3]
    #   ## v1.2.3
    #   ## [1.2.3]
    $startIndex = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*#+\s+\[?v?\d+\.\d+\.\d+') {
            $startIndex = $i
            break
        }
    }

    if ($startIndex -lt 0) {
        return ""
    }

    $bodyLines = @()

    for ($i = $startIndex + 1; $i -lt $lines.Count; $i++) {
        # Stop if another release heading appears.
        if ($lines[$i] -match '^\s*#+\s+\[?v?\d+\.\d+\.\d+') {
            break
        }

        $bodyLines += $lines[$i]
    }

    $body = ($bodyLines -join [Environment]::NewLine).Trim()

    if ([string]::IsNullOrWhiteSpace($body)) {
        return ""
    }

    return $body
}

function ConvertTo-ChildHeadings {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Markdown
    )

    # Demote headings by one level:
    #   ## Added  -> ### Added
    #   ### Fixed -> #### Fixed
    #
    # This lets project headings sit above git-cliff's category headings.
    return ($Markdown -replace '(?m)^(#{1,5})\s+', '#$1 ')
}

function Get-ProjectChangelogBody {
    param(
        [Parameter(Mandatory = $true)]
        [object]$ProjectGroup,

        [Parameter(Mandatory = $true)]
        [string]$TagName,

        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,

        [Parameter(Mandatory = $true)]
        [string]$CliffConfigPath
    )

    $safeName = ($ProjectGroup.Name -replace '[^a-zA-Z0-9.-]', '-')
    $tempProjectChangelog = Join-Path $env:TEMP "gondwana-$safeName-$TagName.md"

    if (Test-Path $tempProjectChangelog) {
        Remove-Item $tempProjectChangelog -Force
    }

    $cliffArgs = @(
        "--config", $CliffConfigPath,
        "--repository", $RepoRoot,
        "--unreleased",
        "--tag", $TagName
    )

    foreach ($includePath in $ProjectGroup.IncludePaths) {
        $cliffArgs += @("--include-path", $includePath)
    }

    $cliffArgs += @("--output", $tempProjectChangelog)

    & git-cliff @cliffArgs
    if ($LASTEXITCODE -ne 0) {
        throw "git-cliff failed while generating changelog section for '$($ProjectGroup.Name)'."
    }

    $body = Get-GeneratedReleaseBody -GeneratedChangelogPath $tempProjectChangelog

    if ([string]::IsNullOrWhiteSpace($body)) {
        return ""
    }

    return ConvertTo-ChildHeadings -Markdown $body
}

function New-GroupedReleaseSection {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$ProjectGroups,

        [Parameter(Mandatory = $true)]
        [string]$TagName,

        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,

        [Parameter(Mandatory = $true)]
        [string]$CliffConfigPath
    )

    $today = Get-Date -Format "yyyy-MM-dd"
    $lines = @()
    $lines += "# [$TagName] - $today"

    $hasProjectChanges = $false

    foreach ($projectGroup in $ProjectGroups) {
        $body = Get-ProjectChangelogBody `
            -ProjectGroup $projectGroup `
            -TagName $TagName `
            -RepoRoot $RepoRoot `
            -CliffConfigPath $CliffConfigPath

        if ([string]::IsNullOrWhiteSpace($body)) {
            continue
        }

        $hasProjectChanges = $true

        $lines += ""
        $lines += "## $($projectGroup.Name)"
        $lines += ""
        $lines += $body
    }

    if (-not $hasProjectChanges) {
        return ""
    }

    # Append the Full Changelog comparison link once at the very bottom.
    $previousTag = git tag --sort=-version:refname |
        Where-Object { $_ -ne $TagName } |
        Select-Object -First 1
    if ($previousTag) {
        $lines += ""
        $lines += "Full Changelog: https://github.com/Isthimius/Gondwana/compare/$previousTag...$TagName"
    }

    return (($lines -join [Environment]::NewLine).Trim() + [Environment]::NewLine + [Environment]::NewLine)
}

function Write-GroupedChangelog {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ChangelogPath,

        [Parameter(Mandatory = $true)]
        [string]$ReleaseSection
    )

    $changelogIsNew = (-not (Test-Path $ChangelogPath)) -or ((Get-Item $ChangelogPath).Length -eq 0)

    if ($changelogIsNew) {
        $content = @(
            "# Changelog",
            "",
            "All notable changes to this project will be documented in this file.",
            "",
            $ReleaseSection.TrimEnd()
        ) -join [Environment]::NewLine
    }
    else {
        $existingContent = Get-Content $ChangelogPath -Raw

        # Insert before the first existing release heading, preserving any file header (e.g. "# Changelog").
        $releaseHeadingPattern = '(?m)^#\s+\[?v?\d+\.\d+\.\d+'
        $match = [regex]::Match($existingContent, $releaseHeadingPattern)

        if ($match.Success) {
            $before = $existingContent.Substring(0, $match.Index).TrimEnd()
            $after  = $existingContent.Substring($match.Index).TrimStart()

            if ([string]::IsNullOrWhiteSpace($before)) {
                $content = $ReleaseSection + $after
            }
            else {
                $content = $before + [Environment]::NewLine + [Environment]::NewLine +
                           $ReleaseSection +
                           $after
            }
        }
        else {
            # Fallback: no existing release heading — append after existing content.
            $content = $existingContent.TrimEnd() +
                       [Environment]::NewLine + [Environment]::NewLine +
                       $ReleaseSection.TrimEnd() +
                       [Environment]::NewLine
        }
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($ChangelogPath, $content, $utf8NoBom)
}

# ----------------------------------------
# RELEASE NOTES PREVIEW
# ----------------------------------------

$releaseSection = New-GroupedReleaseSection `
    -ProjectGroups $ProjectChangelogGroups `
    -TagName $tagName `
    -RepoRoot $repoRoot `
    -CliffConfigPath $CliffConfigPath

if ([string]::IsNullOrWhiteSpace($releaseSection)) {
    throw "No changelog entries were generated for $tagName."
}

Write-Host "Release notes preview from git-cliff:"
Write-Host "-------------------------------------"
Write-Host $releaseSection
Write-Host "-------------------------------------"
Write-Host ""

if ($PreviewOnly) {
    Write-Host "Preview only. No changelog, commit, tag, or push performed."
    exit 0
}

# ----------------------------------------
# HARD CONFIRMATION
# ----------------------------------------

$confirmation = Read-Host "This will update $ChangelogPath, commit it, deploy version $tagName, and push a release tag. Once deployed to NuGet, this cannot be undone. (Run with -PreviewOnly to preview without deploying.) Type DEPLOY to confirm"

if ($confirmation -cne "DEPLOY") {
    Write-Host "Deployment cancelled."
    exit 1
}

# ----------------------------------------
# CHANGELOG UPDATE
# ----------------------------------------

Write-GroupedChangelog `
    -ChangelogPath $ChangelogPath `
    -ReleaseSection $releaseSection

Invoke-ProjectChangelogGeneration `
    -TagName $tagName `
    -CliffConfigPath $CliffConfigPath

# Stage and commit changelog updates.
Invoke-Git @("add", "--", $ChangelogPath)
$projectChangelogPaths = @(
    Get-ChildItem -Path $repoRoot -Directory -Filter "Gondwana*" -ErrorAction SilentlyContinue | ForEach-Object {
        $changelogFile = Join-Path $_.FullName "CHANGELOG.md"
        if (Test-Path -LiteralPath $changelogFile) { $changelogFile }
    }

    Get-ChildItem -Path (Join-Path $repoRoot "Tooling") -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $changelogFile = Join-Path $_.FullName "CHANGELOG.md"
        if (Test-Path -LiteralPath $changelogFile) { $changelogFile }
    }
) | Sort-Object -Unique

if ($projectChangelogPaths.Count -gt 0) {
    Invoke-Git (@("add", "--") + $projectChangelogPaths)
}
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    Invoke-Git @("commit", "-m", "docs: update changelog for $tagName")
    Invoke-Git @("push", $Remote, $RequiredBranch)

    # Re-resolve version after the changelog commit. Stable NBGV releases should remain unchanged.
    $versionAfterChangelogCommit = Get-NbgvPackageVersion
    if ($versionAfterChangelogCommit -ne $version) {
        throw "Version changed after the changelog commit: before=$version after=$versionAfterChangelogCommit. Aborting before tagging."
    }
}
else {
    Write-Host "No changelog changes detected. No changelog commit created."
}

# ----------------------------------------
# TAG HANDLING
# ----------------------------------------

# Refresh tags from remote
Invoke-Git @("fetch", "--tags", "--force", $Remote)

# Check local tag
$localTagExists = $false
git rev-parse -q --verify "refs/tags/$tagName" *> $null
if ($LASTEXITCODE -eq 0) {
    $localTagExists = $true
}

# Check remote tag
$remoteTagExists = $false
$remoteCheck = git ls-remote --tags $Remote "refs/tags/$tagName"
if (-not [string]::IsNullOrWhiteSpace($remoteCheck)) {
    $remoteTagExists = $true
}

if ($localTagExists -or $remoteTagExists) {
    Write-Host "Tag $tagName already exists. Replacing it."

    if ($localTagExists) {
        Invoke-Git @("tag", "-d", $tagName)
    }

    if ($remoteTagExists) {
        Invoke-Git @("push", $Remote, ":refs/tags/$tagName")
    }
}
else {
    Write-Host "Tag $tagName does not exist. Creating it."
}

# Create tag at current HEAD
Invoke-Git @("tag", $tagName)

# Push tag
Invoke-Git @("push", $Remote, $tagName)

Write-Host ""
Write-Host "Done. Updated $ChangelogPath, pushed '$tagName' to '$Remote', and handed off to GitHub Actions."