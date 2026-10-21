# Shared release boundaries for the root/project generators and release publisher.
$ChangelogHeadingPattern = '(?m)^#[ \t]+(?:\[(?<Version>Unreleased|v?\d+\.\d+\.\d+)\]|(?<Version>v?\d+\.\d+\.\d+))(?=[ \t\r\n]|$)[^\r\n]*(?:\r?\n|$)'

function Get-ChangelogRelease {
    param([string]$RepoRoot, [string]$Tag)

    if ($Tag -notmatch '^v?\d+\.\d+\.\d+$') {
        throw "Expected a stable release tag (vX.Y.Z), got '$Tag'."
    }

    $version = [version]$Tag.TrimStart('v')
    $tags = @(git -C $RepoRoot tag --merged HEAD --sort=-version:refname --list 'v[0-9]*')
    if ($LASTEXITCODE -ne 0) { throw 'Could not read reachable release tags.' }
    $previousTag = $tags | Where-Object {
        $_ -match '^v\d+\.\d+\.\d+$' -and [version]$_.Substring(1) -lt $version
    } | Select-Object -First 1
    $rangeArguments = @()
    if ($previousTag) { $rangeArguments = @('--', "$previousTag..HEAD") }

    # Do not let an earlier attempt at this release become its own baseline.
    # An explicit range includes both the original changes and any retry fixes.
    [pscustomobject]@{
        PreviousTag = $previousTag
        Arguments = @('--tag', $Tag, '--ignore-tags', ('^v?' + [regex]::Escape($Tag.TrimStart('v')) + '$'))
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
        if (($i -eq 0 -and $version -eq 'Unreleased') -or
            ($Tag -and $version.TrimStart('v') -eq $Tag.TrimStart('v'))) {
            continue
        }

        $end = if ($i + 1 -lt $headings.Count) { $headings[$i + 1].Index } else { $Content.Length }
        [void]$history.Append($Content.Substring($heading.Index, $end - $heading.Index))
    }

    [pscustomobject]@{
        Prefix = $Content.Substring(0, $headings[0].Index)
        ReleasedHistory = $history.ToString()
    }
}
