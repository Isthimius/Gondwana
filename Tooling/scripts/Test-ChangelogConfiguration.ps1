#Requires -Version 7.0
# Integration tests use real git-cliff and isolated Git history, never the checkout.
$ErrorActionPreference = 'Stop'
function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Invoke-TestGit {
    & git @args | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "git failed: $args" }
}
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('gondwana-config-' + [guid]::NewGuid().ToString('N'))
$oldNumber = $env:CHANGELOG_PR_NUMBER
$oldCommits = $env:CHANGELOG_PR_COMMITS
try {
    New-Item "$fixture/Tooling/scripts" -ItemType Directory -Force | Out-Null
    Copy-Item "$PSScriptRoot/Generate-*-Changelog*.ps1" "$fixture/Tooling/scripts"
    Copy-Item "$PSScriptRoot/../../cliff.toml" $fixture
    Push-Location $fixture
    Invoke-TestGit init --quiet
    Invoke-TestGit config user.name 'Test'
    Invoke-TestGit config user.email 'test@example.com'
    $rootHistory = "# [v1.0.0] - 2000-01-01`r`n`r`nReleased root text — preserved.`r`n"
    $projectHistory = "# v1.0.0 - January 01, 2000`r`n`r`nReleased project text — preserved.`r`n"
    [IO.File]::WriteAllText("$fixture/CHANGELOG.md", "# Changelog`r`n`r`n$rootHistory")
    $metadata = '$ChangelogProjects = @(' + "`n"
    foreach ($name in @('Both', 'ProjectOnly', 'RootOnly', 'Neither')) {
        New-Item $name -ItemType Directory | Out-Null
        Set-Content "$name/code.txt" 'initial'
        $generate = $name -in @('Both', 'ProjectOnly')
        $include = $name -in @('Both', 'RootOnly')
        $metadata += "[pscustomobject]@{ Path = '$name'; RootName = '$name'; GenerateChangelog = `$$generate; IncludeInRootChangelog = `$$include }`n"
    }
    $metadata += ')' + "`n" + (Get-Content "$PSScriptRoot/Changelog-ProjectGroups.ps1" -Raw).Split('# Root-only areas')[1].Insert(0, '# Root-only areas')
    Set-Content Tooling/scripts/Changelog-ProjectGroups.ps1 $metadata
    Invoke-TestGit add .
    Invoke-TestGit commit -qm 'feat: initial fixture'
    Invoke-TestGit tag v1.0.0
    foreach ($name in @('Both', 'ProjectOnly', 'RootOnly', 'Neither')) {
        Add-Content "$name/code.txt" 'changed'
        Invoke-TestGit add .
        Invoke-TestGit commit -qm "feat: update $name"
    }
    $env:CHANGELOG_PR_NUMBER = '123'
    $env:CHANGELOG_PR_COMMITS = '|' + ((& git rev-list v1.0.0..HEAD) -join '|') + '|'
    $root = './Tooling/scripts/Generate-Root-Changelog.ps1'
    $projects = './Tooling/scripts/Generate-Project-Changelogs.ps1'
    & $root -PreviewOnly
    & $projects -PreviewOnly
    Assert (-not (Test-Path Both/CHANGELOG.md)) 'Preview wrote a project file'
    Assert ((Get-Content CHANGELOG.md -Raw).EndsWith($rootHistory)) 'Preview changed root history'
    & $root
    & $projects
    $content = Get-Content CHANGELOG.md -Raw
    foreach ($name in @('Both', 'ProjectOnly', 'RootOnly', 'Neither')) {
        Assert ((Test-Path "$name/CHANGELOG.md") -eq ($name -in @('Both', 'ProjectOnly'))) "Wrong project output for $name"
        Assert (($content -match "(?m)^## $name\r?$") -eq ($name -in @('Both', 'RootOnly'))) "Wrong root output for $name"
    }
    Assert ($content.Contains('https://github.com/Isthimius/Gondwana/pull/123')) 'Missing PR link'
    Assert ($content.EndsWith($rootHistory)) 'Root released history changed'
    Assert ((Get-Content Both/CHANGELOG.md -Raw) -match '# v1.0.0') 'Bootstrap lost tagged history'
    # Existing custom history is authoritative, including CRLF and punctuation.
    [IO.File]::WriteAllText("$fixture/Both/CHANGELOG.md", "# Changelog`r`n`r`n# [Unreleased]`r`n`r`nStale`r`n`r`n$projectHistory")
    & $projects
    Assert ((Get-Content Both/CHANGELOG.md -Raw).EndsWith($projectHistory)) 'Project released history changed'
    $first = Get-Content Both/CHANGELOG.md -Raw
    & $projects
    & $root
    Assert ((Get-Content Both/CHANGELOG.md -Raw) -ceq $first) 'Project refresh not idempotent'
    Assert ((Get-Content CHANGELOG.md -Raw) -ceq $content) 'Root refresh not idempotent'
    Set-Content Neither/CHANGELOG.md 'Existing disabled history'
    & $projects -Projects @('RootOnly', 'Neither')
    Assert ((Get-Content Neither/CHANGELOG.md).Trim() -ceq 'Existing disabled history') 'Disabled history was modified'
    Assert (-not (Test-Path RootOnly/CHANGELOG.md)) 'Selection bypassed disabled generation'
    $rejected = $false
    try { & $projects -Projects @('Unknown') } catch { $rejected = $true }
    Assert $rejected 'Unknown project was accepted'
    & $projects -Tag v2.0.0
    & $root -Tag v2.0.0
    Assert ((Get-Content Both/CHANGELOG.md -Raw).EndsWith($projectHistory)) 'Tagged project changed history'
    Assert ((Get-Content CHANGELOG.md -Raw).EndsWith($rootHistory)) 'Tagged root changed history'
    Assert ((Get-Content CHANGELOG.md -Raw) -notmatch '\[Unreleased\]') 'Tagged root retained unreleased'
    Remove-Item ProjectOnly/CHANGELOG.md
    & $projects -Projects @('ProjectOnly') -Tag v2.0.0
    Assert ((Get-Content ProjectOnly/CHANGELOG.md -Raw) -match '# v2.0.0') 'Tagged bootstrap missing version'
    Assert ((Get-Content ProjectOnly/CHANGELOG.md -Raw) -notmatch '\[Unreleased\]') 'Tagged bootstrap retained unreleased'
    Invoke-TestGit add .
    Invoke-TestGit commit -qm 'docs: checkpoint [skip release notes]'
    Invoke-TestGit tag v2.0.0
    # Each excluded-only change must no-op independently, even with stale Unreleased.
    foreach ($name in @('ProjectOnly', 'Neither')) {
        Add-Content "$name/code.txt" 'excluded change'
        Invoke-TestGit add .
        Invoke-TestGit commit -qm "fix: excluded $name"
        [IO.File]::WriteAllText("$fixture/CHANGELOG.md", "# Changelog`n`n# [Unreleased]`n`nExisting derived text`n`n$rootHistory")
        $before = [Convert]::ToBase64String([IO.File]::ReadAllBytes("$fixture/CHANGELOG.md"))
        & $root
        & $root -PreviewOnly
        & $root -Tag v3.0.0
        $section = @(& $root -SectionOnly -Tag v3.0.0)
        Assert ($section.Count -eq 0) 'Empty root returned release text'
        Assert ([Convert]::ToBase64String([IO.File]::ReadAllBytes("$fixture/CHANGELOG.md")) -ceq $before) 'Empty root changed bytes'
    }
    Write-Host 'PASS: four configurations, previews, bootstrap, selection, history, idempotence, tags, PR links, excluded-only no-op.'
}
finally {
    if ((Get-Location).Path -eq $fixture) { Pop-Location }
    $env:CHANGELOG_PR_NUMBER = $oldNumber
    $env:CHANGELOG_PR_COMMITS = $oldCommits
    Remove-Item $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
