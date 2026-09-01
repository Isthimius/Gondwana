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

function Invoke-RootChangelogGeneration {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TagName,

        [Parameter(Mandatory = $true)]
        [string]$CliffConfigPath,

        [Parameter(Mandatory = $true)]
        [string]$ChangelogPath
    )

    $rootChangelogScript = Join-Path $PSScriptRoot "Generate-Root-Changelog.ps1"
    if (-not (Test-Path $rootChangelogScript)) {
        throw "Root changelog script not found at '$rootChangelogScript'."
    }

    Write-Host ""
    Write-Host "Updating root changelog..."

    & $rootChangelogScript `
        -Tag $TagName `
        -ChangelogPath $ChangelogPath `
        -CliffConfigPath $CliffConfigPath

    if ($LASTEXITCODE -ne 0) {
        throw "Generate-Root-Changelog.ps1 failed."
    }
}

Require-Command git "Install Git for Windows, then reopen your terminal."
Require-Command dotnet "Install .NET SDK 8.0+, then reopen your terminal."
Require-Command nbgv "Install with: dotnet tool install -g nbgv"
Require-Command git-cliff "Install with: winget install --id orhun.git-cliff"

# Resolve relative paths against the repo root (two levels above this script:
# Tooling/scripts/ -> Tooling/ -> root) so the script works correctly when invoked
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
# CHANGELOG PREVIEW
# ---------------------------------------------------------------------------

function Get-RootChangelogSection {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TagName,

        [Parameter(Mandatory = $true)]
        [string]$CliffConfigPath,

        [Parameter(Mandatory = $true)]
        [string]$ChangelogPath
    )

    $rootChangelogScript = Join-Path $PSScriptRoot "Generate-Root-Changelog.ps1"
    $section = & $rootChangelogScript `
        -Tag $TagName `
        -SectionOnly `
        -ChangelogPath $ChangelogPath `
        -CliffConfigPath $CliffConfigPath

    if ($LASTEXITCODE -ne 0) {
        throw "Generate-Root-Changelog.ps1 failed while previewing release notes."
    }

    return ($section -join [Environment]::NewLine)
}
# ----------------------------------------
# RELEASE NOTES PREVIEW
# ----------------------------------------

$releaseSection = Get-RootChangelogSection `
    -TagName $tagName `
    -CliffConfigPath $CliffConfigPath `
    -ChangelogPath $ChangelogPath

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

Invoke-RootChangelogGeneration `
    -TagName $tagName `
    -ChangelogPath $ChangelogPath `
    -CliffConfigPath $CliffConfigPath

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
}
else {
    Write-Host "Tag $tagName does not exist. Creating it."
}

# Create tag at current HEAD
Invoke-Git @("tag", $tagName)

# Publish the release commit and tag as one transaction. If either ref is
# rejected, an atomic push leaves both remote refs unchanged, preventing a
# versioned changelog section from reaching the branch without its Git tag.
$branchRefSpec = "HEAD:refs/heads/$RequiredBranch"
if ($remoteTagExists) {
    $tagRefSpec = "+refs/tags/${tagName}:refs/tags/${tagName}"
}
else {
    $tagRefSpec = "refs/tags/${tagName}:refs/tags/${tagName}"
}

Invoke-Git @("push", "--atomic", $Remote, $branchRefSpec, $tagRefSpec)

Write-Host ""
Write-Host "Done. Atomically pushed $RequiredBranch and '$tagName' to '$Remote', then handed off to GitHub Actions."
