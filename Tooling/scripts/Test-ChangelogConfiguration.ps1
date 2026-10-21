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
    Copy-Item "$PSScriptRoot/Changelog-Common.ps1" "$fixture/Tooling/scripts"
    Copy-Item "$PSScriptRoot/Extract-ReleaseNotes.ps1" "$fixture/Tooling/scripts"
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
        $metadata += "[pscustomobject]@{ Path = '$name'; RootName = '$name'; GenerateChangelog = `$$generate; IncludeInRootChangelog = `$$include; IncludePaths = @('$name/**/*') }`n"
    }
    $metadata += ')'
    Set-Content Tooling/scripts/Changelog-ProjectGroups.ps1 $metadata
    Invoke-TestGit add .
    Invoke-TestGit commit -qm 'feat: initial fixture'
    $initialSection = (& ./Tooling/scripts/Generate-Root-Changelog.ps1 -SectionOnly -Tag v1.0.0) -join "`n"
    Assert ($initialSection.Contains('Initial fixture')) 'First release without earlier tags lost its commits'
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
    $taggedRoot = Get-Content CHANGELOG.md -Raw
    $taggedProject = Get-Content Both/CHANGELOG.md -Raw
    & $root -Tag v2.0.0
    & $projects -Tag v2.0.0
    Assert ((Get-Content CHANGELOG.md -Raw) -ceq $taggedRoot) 'Untagged release retry duplicated root notes'
    Assert ((Get-Content Both/CHANGELOG.md -Raw) -ceq $taggedProject) 'Untagged release retry duplicated project notes'
    Invoke-TestGit add .
    Invoke-TestGit commit -qm 'docs: checkpoint [skip release notes]'
    Invoke-TestGit tag v2.0.0
    & $root -Tag v2.0.0
    & $projects -Tag v2.0.0
    Assert ((Get-Content CHANGELOG.md -Raw) -ceq $taggedRoot) 'Retry with tag at HEAD lost root entries'
    Assert ((Get-Content Both/CHANGELOG.md -Raw) -ceq $taggedProject) 'Retry with tag at HEAD lost project entries'
    Remove-Item ProjectOnly/CHANGELOG.md
    & $projects -Projects @('ProjectOnly') -Tag v2.0.0
    $bootstrapped = Get-Content ProjectOnly/CHANGELOG.md -Raw
    Assert ($bootstrapped.Contains('Update ProjectOnly')) 'Bootstrap on an existing release tag lost original changes'
    Assert ([regex]::Matches($bootstrapped, '(?m)^# v2\.0\.0\b').Count -eq 1) 'Tagged bootstrap duplicated release'

    # Reproduce 2.6.0: an existing tag plus a later fix and duplicate headings.
    Add-Content Both/code.txt 'retry fix'
    Invoke-TestGit add .
    Invoke-TestGit commit -qm 'fix: retry Both (#124)'
    [IO.File]::WriteAllText("$fixture/CHANGELOG.md", "# Changelog`r`n`r`n# [v2.0.0] - 2000-01-02`r`n`r`n- Retry only`r`n`r`n" + $taggedRoot.Substring($taggedRoot.IndexOf('# [v2.0.0]')))
    [IO.File]::WriteAllText("$fixture/Both/CHANGELOG.md", "# Changelog`r`n`r`n# [v2.0.0] - 2000-01-02`r`n`r`n- Retry only`r`n`r`n" + $taggedProject.Substring($taggedProject.IndexOf('# v2.0.0')))
    # A newer tag on unrelated history must not become the compare baseline.
    $orphan = 'unrelated' | & git commit-tree (& git rev-parse 'HEAD^{tree}')
    if ($LASTEXITCODE -ne 0) { throw 'Could not create unrelated tag fixture' }
    Invoke-TestGit tag v99.0.0 $orphan
    $beforePreview = Get-Content CHANGELOG.md -Raw
    & $root -Tag v2.0.0 -PreviewOnly
    & $projects -Tag v2.0.0 -PreviewOnly
    Assert ((Get-Content CHANGELOG.md -Raw) -ceq $beforePreview) 'Retry preview changed root notes'
    & $root -Tag v2.0.0
    & $projects -Tag v2.0.0
    $retryRoot = Get-Content CHANGELOG.md -Raw
    $retryProject = Get-Content Both/CHANGELOG.md -Raw
    foreach ($retry in @($retryRoot, $retryProject)) {
        Assert ($retry.Contains('Update Both')) 'Retry lost the original release changes'
        Assert ($retry.Contains('Retry Both')) 'Retry lost the follow-up fix'
        Assert ([regex]::Matches($retry, '(?m)^# (?:\[v2\.0\.0\]|v2\.0\.0)(?=[ \r\n])').Count -eq 1) 'Retry retained duplicate release sections'
    }
    Assert ($retryRoot.EndsWith($rootHistory)) 'Retry changed older root history bytes'
    Assert ($retryProject.EndsWith($projectHistory)) 'Retry changed older project history bytes'
    Assert ($retryRoot.Contains('compare/v1.0.0...v2.0.0')) 'Retry chose the wrong previous tag'
    & $root -Tag v2.0.0
    & $projects -Tag v2.0.0
    Assert ((Get-Content CHANGELOG.md -Raw) -ceq $retryRoot) 'Repeated retry changed root notes'
    Assert ((Get-Content Both/CHANGELOG.md -Raw) -ceq $retryProject) 'Repeated retry changed project notes'
    Invoke-TestGit tag -d v99.0.0

    $extract = './Tooling/scripts/Extract-ReleaseNotes.ps1'
    & $extract -Tag v2.0.0
    $releaseNotes = Get-Content RELEASE_NOTES.md -Raw
    Assert ($releaseNotes.Contains('Update Both') -and $releaseNotes.Contains('Retry Both')) 'Extraction lost full release entries'
    Assert (-not $releaseNotes.Contains('Released root text')) 'Extraction included older history'
    foreach ($badContent in @(
        $retryRoot.Replace('[v2.0.0]', '[v3.0.0]'),
        ($retryRoot + "`n# [v2.0.0] - 2000-01-02`n`n- Duplicate`n"),
        ("# [Unreleased]`n`n- Future change`n`n" + $retryRoot),
        "# [v2.0.0] - 2000-01-02`n`nNo entries.`n"
    )) {
        [IO.File]::WriteAllText("$fixture/CHANGELOG.md", $badContent)
        $rejected = $false
        try { & $extract -Tag v2.0.0 } catch { $rejected = $true }
        Assert $rejected 'Publisher accepted duplicate, mismatched, unreleased, or empty notes'
        Assert ((Get-Content RELEASE_NOTES.md -Raw) -ceq $releaseNotes) 'Failed extraction overwrote release notes'
    }
    [IO.File]::WriteAllText("$fixture/CHANGELOG.md", $retryRoot)
    Invoke-TestGit add .
    Invoke-TestGit commit -qm 'docs: checkpoint retry [skip release notes]'
    Invoke-TestGit tag -f v2.0.0
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
    Write-Host 'PASS: configurations, previews, bootstrap, selection, history, retries, duplicate repair, release extraction, PR links, excluded-only no-op.'
}
finally {
    if ((Get-Location).Path -eq $fixture) { Pop-Location }
    $env:CHANGELOG_PR_NUMBER = $oldNumber
    $env:CHANGELOG_PR_COMMITS = $oldCommits
    Remove-Item $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
