# How to Release Gondwana

A pragmatic checklist for maintainers publishing alpha, beta, release-candidate (RC), or stable Gondwana versions. The implementation details live in [[Tooling Scripts]] and [[GitHub Automation and Workflows]].

> **Warning:** Running the actual release script pushes a Git tag that triggers public NuGet publishing. The `-PreviewOnly` flag is the dry run. Never run a release as a test.

## 1. Choose and commit a version

Edit `version.json` on a branch, review it through a PR, and merge to `master`. Set `version` to a literal three-part SemVer version with an optional prerelease suffix; leave `assemblyVersion` numeric.

| Intended release | `version` |
| --- | --- |
| First beta | `2.6.2-beta.1` |
| Second beta | `2.6.2-beta.2` |
| Release candidate | `2.6.2-rc.1` |
| Final stable | `2.6.2` |

Never reuse a published version. Older Git tags, GitHub Releases, and NuGet packages remain available even when a beta/RC changelog section is replaced.

## 2. Verify readiness

- Confirm required fixes are merged, `master` is current, and CI is passing.
- Confirm the nightly `package-binaries.yml` validation (or run it manually via Actions).
- Verify release-specific behavior such as Blazor template/native asset packaging when relevant.
- Install Git, .NET SDK, `nbgv` (`dotnet tool install -g nbgv`), and `git-cliff` (`winget install --id orhun.git-cliff`).
- On `master`, inspect `nbgv get-version -v NuGetPackageVersion`; it must match the intended version exactly.

## 3. Preview and publish

From a clean, synchronized `master` working tree:

```powershell
git switch master
git pull --ff-only
nbgv get-version -v NuGetPackageVersion
.\Tooling\scripts\release.ps1 -PreviewOnly
```

Check the generated release notes and version tag. When correct:

```powershell
.\Tooling\scripts\release.ps1
```

Type `DEPLOY` only after reviewing the preview. The script runs the core tests, regenerates changelogs, commits them, creates the version tag, and atomically pushes the release commit and tag. GitHub Actions then builds, packages, and publishes.

## 4. Verify publication

Inspect **Actions → Release** and the resulting GitHub Release, ZIP asset, and NuGet packages. Confirm the tag, package versions, and release-note body agree.

- **Alpha/beta/RC:** GitHub marks it as a prerelease; the latest stable Release stays unchanged. Stable `/api/` documentation is not republished. Beta templates pin `Gondwana.*` dependencies to that exact beta package version.
- **Stable:** GitHub marks it as a normal release and updates the immutable versioned API documentation and stable `/api/` redirect.

To install a prerelease explicitly (replace the example version):

```powershell
dotnet add package Gondwana --version 2.6.2-beta.1
dotnet tool install -g Gondwana.Cli --version 2.6.2-beta.1
dotnet new install Gondwana.Templates@2.6.2-beta.1
```

If the global CLI is already installed, use `dotnet tool update -g Gondwana.Cli --version 2.6.2-beta.1` instead of `install`. For generated beta projects, keep the matching pinned prerelease package references until intentionally upgrading.

## Changelog policy

The root and project changelogs always regenerate from **the preceding stable version tag**. `[Unreleased]` and any `-alpha`, `-beta`, or `-rc` changelog sections are temporary: each new prerelease or stable release replaces them. Previously published stable sections are preserved. The final stable section is the authoritative history of changes since the last stable version.

If something fails, do not force-move the tag or publish the same NuGet version again. Fix the issue and use a new prerelease identifier (e.g. `-beta.2`), or investigate the existing workflow run. For technical details, see [[Tooling Scripts]] and [[GitHub Automation and Workflows]].
