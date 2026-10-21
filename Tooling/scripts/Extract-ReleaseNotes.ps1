#Requires -Version 5.1
<#
.SYNOPSIS
    Extracts one unambiguous versioned section for a GitHub Release.
#>
param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [string]$ChangelogPath = 'CHANGELOG.md',
    [string]$OutputPath = 'RELEASE_NOTES.md'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Changelog-Common.ps1')

if ($Tag -notmatch '^v?\d+\.\d+\.\d+$') { throw "Invalid release tag '$Tag'." }
$content = Get-Content -LiteralPath $ChangelogPath -Raw
$headings = [regex]::Matches($content, $ChangelogHeadingPattern)
$matching = @($headings | Where-Object { $_.Groups['Version'].Value.TrimStart('v') -eq $Tag.TrimStart('v') })
if ($matching.Count -ne 1) {
    throw "Expected exactly one changelog section for '$Tag'; found $($matching.Count)."
}
if ($headings[0].Index -ne $matching[0].Index) {
    throw "The leading changelog section does not match release tag '$Tag'."
}

$start = $matching[0].Index
$end = if ($headings.Count -gt 1) { $headings[1].Index } else { $content.Length }
$notes = $content.Substring($start, $end - $start).TrimEnd() + "`n"
if ($notes -notmatch '(?m)^-\s+\S') { throw "No release entries were found for '$Tag'." }

$outputFile = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
[IO.File]::WriteAllText($outputFile, $notes, (New-Object System.Text.UTF8Encoding($false)))
