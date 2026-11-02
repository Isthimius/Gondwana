# Shared release boundaries for the root/project generators and release publisher.
# Prereleases are published artifacts but transient changelog sections. Only stable
# vX.Y.Z tags establish permanent changelog boundaries.
$ChangelogVersionPattern = 'v?\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?'
$ChangelogTagPattern = '^' + $ChangelogVersionPattern + '$'
$ChangelogPrereleaseTagPattern = '^v?\d+\.\d+\.\d+-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*$'
$ChangelogHeadingPattern = '(?m)^#[ \t]+(?:\[(?<Version>Unreleased|' + $ChangelogVersionPattern + ')\]|(?<Version>' + $ChangelogVersionPattern + '))(?=[ \t\r\n]|$)[^\r\n]*(?:\r?\n|$)'

function Get-ReachableStableTags {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $tags = @(git -C $RepoRoot tag --merged HEAD --sort=-version:refname --list 'v[0-9]*')
    if ($LASTEXITCODE -ne 0) { throw 'Could not read reachable release tags.' }
    return @($tags | Where-Object { $_ -match '^v\d+\.\d+\.\d+$' })
}

function Get-ChangelogUnreleased {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $previousTag = @(Get-ReachableStableTags -RepoRoot $RepoRoot) | Select-Object -First 1
    $rangeArguments = @()
    if ($previousTag) { $rangeArguments = @('--', "$previousTag..HEAD") }
    return [pscustomobject]@{
        PreviousTag = $previousTag
        Arguments = @('--unreleased', '--ignore-tags', $ChangelogPrereleaseTagPattern)
        BootstrapArguments = @('--ignore-tags', $ChangelogPrereleaseTagPattern)
        RangeArguments = $rangeArguments
    }
}

function Get-ChangelogRelease {
    param([Parameter(Mandatory = $true)][string]$RepoRoot, [Parameter(Mandatory = $true)][string]$Tag)

    if ($Tag -cnotmatch $ChangelogTagPattern) {
        throw "Expected a release tag (vX.Y.Z or vX.Y.Z-prerelease), got '$Tag'."
    }

    $version = [version](($Tag.TrimStart('v') -split '-')[0])
    $previousTag = @(Get-ReachableStableTags -RepoRoot $RepoRoot) | Where-Object {
        [version]$_.Substring(1) -lt $version
    } | Select-Object -First 1
    $rangeArguments = @()
    if ($previousTag) { $rangeArguments = @('--', "$previousTag..HEAD") }

    # Ignore all earlier prerelease tags and this release's tag so retries
    # regenerate the entire range, even when the tag exists locally.
    $targetTagPattern = '^v?' + [regex]::Escape($Tag.TrimStart('v')) + '$'
    $ignoredTags = '(' + $targetTagPattern + '|' + $ChangelogPrereleaseTagPattern + ')'
    $arguments = @('--tag', $Tag, '--ignore-tags', $ignoredTags)
    return [pscustomobject]@{
        PreviousTag = $previousTag
        Arguments = $arguments
        SectionArguments = $arguments
        RangeArguments = $rangeArguments
    }
}

function Split-ChangelogContent {
    param([AllowEmptyString()][string]$Content, [string]$Tag)

    $headings = [regex]::Matches($Content, $ChangelogHeadingPattern)
    if ($headings.Count -eq 0) {
        return [pscustomobject]@{ Prefix = $Content; ReleasedHistory = '' }
    }

    $history = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $headings.Count; $i++) {
        $heading = $headings[$i]
        $version = $heading.Groups['Version'].Value
        $isTemporary = $version -eq 'Unreleased' -or $version -match $ChangelogPrereleaseTagPattern
        $isTarget = $Tag -and $version.TrimStart('v') -eq $Tag.TrimStart('v')
        if ($isTemporary -or $isTarget) { continue }

        $end = if ($i + 1 -lt $headings.Count) { $headings[$i + 1].Index } else { $Content.Length }
        [void]$history.Append($Content.Substring($heading.Index, $end - $heading.Index))
    }

    return [pscustomobject]@{
        Prefix = $Content.Substring(0, $headings[0].Index)
        ReleasedHistory = $history.ToString()
    }
}
