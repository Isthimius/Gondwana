param(
    [string]$Remote = "origin",
    [string]$RequiredBranch = "master",
    [string]$ChangelogPath = "CHANGELOG.md",
    [string]$CliffConfigPath = "cliff.toml",
    [switch]$PreviewOnly
)

$ErrorActionPreference = "Stop"

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

    $testProjectPath = Join-Path $RepoRoot "Gondwana.Tests/Gondwana.Tests.csproj"
    if (-not (Test-Path $testProjectPath)) {
        throw "Expected test project was not found at $testProjectPath."
    }

    Write-Host "Running Gondwana.Tests unit tests..."
    & dotnet test $testProjectPath --configuration Release --nologo /p:EnableWindowsTargeting=true
    if ($LASTEXITCODE -ne 0) {
        throw "Gondwana.Tests unit tests failed. Aborting deployment."
    }
}

Require-Command git "Install Git for Windows, then reopen your terminal."
Require-Command dotnet "Install .NET SDK 8.0+, then reopen your terminal."
Require-Command nbgv "Install with: dotnet tool install -g nbgv"
Require-Command git-cliff "Install with: winget install git-cliff"

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

# Ensure correct branch.
$currentBranch = git branch --show-current
if ($currentBranch -ne $RequiredBranch) {
    throw "You must be on '$RequiredBranch' to create a release tag. Current branch: $currentBranch"
}

# Refresh remote branch/tag state before checks.
Invoke-Git @("fetch", "--prune", "--tags", "--force", $Remote)

# Ensure local branch is not behind remote.
$localHead = git rev-parse HEAD
$remoteHead = git rev-parse "$Remote/$RequiredBranch"
if ($localHead -ne $remoteHead) {
    throw "Local '$RequiredBranch' is not aligned with '$Remote/$RequiredBranch'. Pull/rebase first, then retry."
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

# ----------------------------------------
# RELEASE NOTES PREVIEW
# ----------------------------------------

# Preview by prepending to a temp copy of the changelog so the output matches
# exactly what --prepend will write (no header block, just the new section body).
$tempChangelog = Join-Path $env:TEMP "gondwana-changelog-preview-$tagName.md"
if (Test-Path $ChangelogPath) {
    Copy-Item $ChangelogPath $tempChangelog
}
else {
    New-Item -ItemType File -Path $tempChangelog -Force | Out-Null
}

& git-cliff --config $CliffConfigPath --repository $repoRoot --unreleased --tag $tagName --prepend $tempChangelog
if ($LASTEXITCODE -ne 0) {
    throw "git-cliff failed while generating release notes preview."
}

Write-Host "Release notes preview from git-cliff:"
Write-Host "-------------------------------------"
# Show only the new section (everything before the next top-level release heading).
# Matches both git-cliff format (# [version]) and the legacy repo format (# vX.Y.Z).
$inSection = $false
foreach ($line in (Get-Content $tempChangelog)) {
    if ($line -match '^# (\[|v\d)') {
        if ($inSection) { break }
        $inSection = $true
    }
    if ($inSection) { Write-Host $line }
}
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

$changelogIsNew = (-not (Test-Path $ChangelogPath)) -or ((Get-Item $ChangelogPath).Length -eq 0)

if ($changelogIsNew) {
    # First-ever changelog: use --output so the "# Changelog" header is written.
    & git-cliff --config $CliffConfigPath --repository $repoRoot --tag $tagName --output $ChangelogPath
}
else {
    # Existing changelog: prepend only the new section body, preserving history.
    & git-cliff --config $CliffConfigPath --repository $repoRoot --unreleased --tag $tagName --prepend $ChangelogPath
}
if ($LASTEXITCODE -ne 0) {
    throw "git-cliff failed while updating $ChangelogPath."
}

# Commit CHANGELOG.md only if it actually changed.
Invoke-Git @("add", $ChangelogPath)
git diff --cached --quiet -- $ChangelogPath
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
    Write-Host "$ChangelogPath did not change. No changelog commit created."
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
