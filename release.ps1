param(
    [string]$Remote = "origin",
    [string]$RequiredBranch = "master"
)

$ErrorActionPreference = "Stop"

function Require-Command {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Write-Host "Required command '$Name' was not found on PATH."

        $answer = Read-Host "Would you like to install '$Name' globally via npm? [Y/N]"
        if ($answer -match '^[Yy]$') {
            & npm install -g $Name
            if ($LASTEXITCODE -ne 0) {
                throw "npm install -g $Name failed."
            }
            if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
                throw "Command '$Name' still not found after npm install."
            }
        }
        else {
            throw "Required command '$Name' was not found on PATH."
        }
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

Require-Command git
Require-Command nbgv

# Ensure we're inside a git repo
git rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -ne 0) {
    throw "This script must be run inside a git repository."
}

# Get version from Nerdbank.GitVersioning
$versionInfo = nbgv get-version -f json | ConvertFrom-Json
$version = $versionInfo.NuGetPackageVersion

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not determine NuGetPackageVersion from nbgv."
}

$tagName = "v$version"

Write-Host ""
Write-Host "Resolved version: $version"
Write-Host "Resolved tag: $tagName"
Write-Host ""

# ----------------------------------------
# HARD CONFIRMATION
# ----------------------------------------
$confirmation = Read-Host "This will deploy version $tagName. Once deployed to NuGet, this cannot be undone. Are you sure you want to deploy? Type DEPLOY to confirm"

if ($confirmation -cne "DEPLOY") {
    Write-Host "Deployment cancelled."
    exit 1
}

# ----------------------------------------
# PRE-FLIGHT CHECKS
# ----------------------------------------

# Ensure clean working tree
git diff --quiet
if ($LASTEXITCODE -ne 0) {
    throw "Working tree has unstaged changes."
}

git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    throw "Working tree has staged but uncommitted changes."
}

# Ensure correct branch
$currentBranch = git branch --show-current
if ($currentBranch -ne $RequiredBranch) {
    throw "You must be on '$RequiredBranch' to create a release tag. Current branch: $currentBranch"
}

Write-Host "Pre-flight checks passed."

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
Write-Host "Done. Tag '$tagName' pushed to '$Remote'."
Write-Host "GitHub Actions will take it from here."