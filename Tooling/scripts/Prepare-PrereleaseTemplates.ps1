#Requires -Version 5.1
<#
.SYNOPSIS
    Pin Gondwana package references in prerelease project templates.
.DESCRIPTION
    Changes only Gondwana.* references in template MyGame.csproj files in the
    disposable packaging checkout. Stable versions leave templates unchanged.
    Run this before dotnet pack; never commit the generated edits.
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$TemplateRoot = (Join-Path $PSScriptRoot '../Gondwana.Templates/templates')
)

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$') {
    throw "Invalid SemVer package version '$Version'."
}
if ($Version -notmatch '-') {
    Write-Host "Stable $Version; retaining floating Gondwana 2.* template references."
    return
}

$projects = @(Get-ChildItem -LiteralPath $TemplateRoot -Filter MyGame.csproj -File -Recurse)
if ($projects.Count -ne 3) {
    throw "Expected exactly three Gondwana project templates under '$TemplateRoot'; found $($projects.Count)."
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
$pattern = '(<PackageReference\s+Include="Gondwana(?:\.[^"]+)?"\s+Version=")2\.\*(")'
foreach ($project in $projects) {
    $original = [IO.File]::ReadAllText($project.FullName)
    $matches = [regex]::Matches($original, $pattern)
    if ($matches.Count -eq 0) {
        throw "No floating Gondwana package references found in $($project.FullName)."
    }
    $replacement = '${1}' + $Version + '${2}'
    $updated = [regex]::Replace($original, $pattern, $replacement)
    if ([regex]::IsMatch($updated, $pattern)) {
        throw "Unpinned Gondwana package reference remains in $($project.FullName)."
    }
    [IO.File]::WriteAllText($project.FullName, $updated, $utf8)
    Write-Host "Pinned $($matches.Count) Gondwana dependencies in $($project.FullName) to $Version."
}
