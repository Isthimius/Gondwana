Gondwana includes repository-maintenance tooling for developer setup, local CLI/template iteration, changelog generation, release orchestration, API documentation publishing, and Wiki synchronization.

Most of it lives under:

```text
Tooling/scripts/
├── Changelog-ProjectGroups.ps1
├── Generate-Project-Changelogs.ps1
├── Generate-Root-Changelog.ps1
├── Reinstall-Gondwana-Cli.ps1
├── Reinstall-Gondwana-Templates.ps1
├── Setup-Gondwana-Dev.ps1
├── Test-ChangelogConfiguration.ps1
├── release.ps1
├── api-docs/
│   ├── build.py
│   ├── install-doxygen.sh
│   ├── publish.py
│   ├── publish.sh
│   ├── source_filter.py
│   └── validation/test scripts
└── wiki-sync/
    ├── baseline.json
    ├── sync.py
    └── test_sync.py
```

These are **maintainer and contributor tools**. They are not part of the Gondwana runtime and are not required by games that consume Gondwana through NuGet.

Their purpose is to make repository operations repeatable instead of relying on somebody remembering the right sequence of `dotnet`, Git, NuGet, `git-cliff`, Doxygen, or GitHub commands.

---

## Contents

- [Which tool should I use?](#which-tool-should-i-use)
- [Running the tools](#running-the-tools)
- [Setup-Gondwana-Dev.ps1](#setup-gondwana-devps1)
- [Reinstall-Gondwana-Cli.ps1](#reinstall-gondwana-clips1)
- [Reinstall-Gondwana-Templates.ps1](#reinstall-gondwana-templatesps1)
- [Changelog tooling](#changelog-tooling)
- [release.ps1](#releaseps1)
- [API documentation tooling](#api-documentation-tooling)
- [Wiki synchronization tooling](#wiki-synchronization-tooling)
- [Safety and failure behavior](#safety-and-failure-behavior)
- [Mental model](#mental-model)

---

# Which tool should I use?

| Goal | Tool |
|---|---|
| Set up a new Gondwana development machine | `Setup-Gondwana-Dev.ps1` |
| Test changes to the `gondwana` CLI locally | `Reinstall-Gondwana-Cli.ps1` |
| Test changes to Gondwana `dotnet new` templates locally | `Reinstall-Gondwana-Templates.ps1` |
| Generate or preview project-specific changelogs | `Generate-Project-Changelogs.ps1` |
| Generate or preview the grouped root changelog | `Generate-Root-Changelog.ps1` |
| Configure which projects generate which changelog outputs | `Changelog-ProjectGroups.ps1` |
| Validate changelog configuration and generator behavior | `Test-ChangelogConfiguration.ps1` |
| Prepare and start an official Gondwana release | `release.ps1` |
| Build or publish versioned API documentation | `api-docs/` scripts |
| Synchronize `docs/wiki` with the GitHub Wiki | `wiki-sync/sync.py` |

A useful distinction is:

```text
DEVELOPMENT ENVIRONMENT
└── Setup-Gondwana-Dev.ps1

LOCAL ITERATION
├── Reinstall-Gondwana-Cli.ps1
└── Reinstall-Gondwana-Templates.ps1

CHANGELOG / RELEASE MANAGEMENT
├── Changelog-ProjectGroups.ps1
├── Generate-Root-Changelog.ps1
├── Generate-Project-Changelogs.ps1
├── Test-ChangelogConfiguration.ps1
└── release.ps1

DOCUMENTATION AUTOMATION
├── api-docs/
└── wiki-sync/
```

`Changelog-ProjectGroups.ps1` is configuration and is normally dot-sourced by the changelog tooling rather than run directly.

The `api-docs/` and `wiki-sync/` scripts are primarily workflow implementation details. They can be run locally for validation, but normal publication and synchronization are performed by GitHub Actions.

---

# Running the tools

From the repository root, PowerShell scripts can be invoked with their repository-relative paths:

```powershell
.\Tooling\scripts\Setup-Gondwana-Dev.ps1
```

or:

```powershell
.\Tooling\scripts\release.ps1 -PreviewOnly
```

The maintainer PowerShell scripts resolve the repository root from their own location so they do not depend heavily on the caller's current working directory.

The general PowerShell requirement is **PowerShell 5.1 or later**. The isolated changelog integration suite is intended to run under **PowerShell 7**.

Python tooling under `api-docs/` requires Python 3; `wiki-sync/` requires Python 3.11+. The API-documentation validation suite also uses Node.js for browser-facing JavaScript tests and Doxygen 1.14.0 for the real documentation build smoke test.

---

# `Setup-Gondwana-Dev.ps1`

This is the bootstrap script for a Gondwana development environment.

Its intended use is:

> Clone Gondwana, run one script, and get the machine as close as possible to ready for development.

It is designed to be **idempotent**: rerunning it should restore, update, or verify existing dependencies rather than assuming a completely empty machine.

Basic usage:

```powershell
.\Tooling\scripts\Setup-Gondwana-Dev.ps1
```

## What it sets up

The script currently performs thirteen setup or verification steps:

1. verifies Git is available on `PATH`
2. verifies the .NET 8 SDK, installing it through `winget` on Windows when possible
3. restores repository-local .NET tools such as Nerdbank.GitVersioning
4. restores NuGet packages for the solution with dependency reevaluation
5. builds `Gondwana.sln` in Release configuration unless `-SkipBuild` is supplied
6. installs or updates the global `Gondwana.Cli` tool
7. installs or updates `Gondwana.Templates`, while retaining a newer already-installed local version instead of forcing a downgrade
8. installs or updates the `wasm-tools` workload
9. checks for SDL2 native binaries and prints guidance if they are missing
10. checks for LibVLC and can install VLC through `winget` on Windows
11. verifies `git-cliff` and can install/update it through `winget` on Windows
12. installs `butler` from itch.io's Broth CDN when missing and adds its install directory to the current shell's `PATH`
13. runs `gondwana doctor` when the command is available

Steps 8 through 12 are optional-development dependencies and can be skipped together.

## Parameters

| Parameter | Purpose |
|---|---|
| `-SkipBuild` | Skip the Release build |
| `-SkipOptional` | Skip WASM, SDL2, LibVLC, `git-cliff`, and Butler setup |

Examples:

```powershell
# Full setup
.\Tooling\scripts\Setup-Gondwana-Dev.ps1

# Restore/install without building the solution
.\Tooling\scripts\Setup-Gondwana-Dev.ps1 -SkipBuild

# Core setup only
.\Tooling\scripts\Setup-Gondwana-Dev.ps1 -SkipOptional
```

---

# `Reinstall-Gondwana-Cli.ps1`

This script supports local development of:

```text
Tooling/Gondwana.Cli
```

without publishing a new NuGet package first.

It:

1. packs `Gondwana.Cli` into a local package feed
2. uninstalls the currently installed global tool when present
3. reinstalls the freshly packed package from that local source
4. removes the existing `gondwana.cli` entry from the active NuGet package cache before reinstalling so the just-built package is selected even when the package version has not changed
5. displays `gondwana --version` when available in the current shell

That explicit package-cache removal matters during development because repeatedly rebuilding the same package version with different contents violates the normal assumption that a NuGet package ID/version pair is immutable.

## Parameters

| Parameter | Purpose | Default |
|---|---|---|
| `-Configuration` | Build configuration passed to `dotnet pack` | `Release` |
| `-PackageOutput` | Local package-feed directory | `.local-nuget` |

Examples:

```powershell
.\Tooling\scripts\Reinstall-Gondwana-Cli.ps1

.\Tooling\scripts\Reinstall-Gondwana-Cli.ps1 -Configuration Debug

.\Tooling\scripts\Reinstall-Gondwana-Cli.ps1 `
    -PackageOutput artifacts\local-tools
```

---

# `Reinstall-Gondwana-Templates.ps1`

This is the template-package equivalent of the CLI reinstall script.

It supports local iteration on:

```text
Tooling/Gondwana.Templates
```

without publishing a new template package.

It:

1. packs `Gondwana.Templates`
2. identifies the exact package version that was just built
3. uninstalls the currently registered Gondwana template package when present
4. installs that exact local version with `dotnet new install`
5. uses an isolated temporary `NUGET_PACKAGES` directory to avoid stale package-cache reuse
6. prints the installed package version and available Gondwana templates
7. restores the caller's previous `NUGET_PACKAGES` environment and removes the temporary cache

## Parameters

| Parameter | Purpose | Default |
|---|---|---|
| `-Configuration` | Build configuration passed to `dotnet pack` | `Release` |
| `-PackageOutput` | Local package-feed directory | `.local-nuget` |

Examples:

```powershell
.\Tooling\scripts\Reinstall-Gondwana-Templates.ps1

.\Tooling\scripts\Reinstall-Gondwana-Templates.ps1 -Configuration Debug

.\Tooling\scripts\Reinstall-Gondwana-Templates.ps1 `
    -PackageOutput artifacts\local-templates
```

The practical difference between the two reinstall scripts is the .NET installation mechanism:

| Script | Installation mechanism |
|---|---|
| `Reinstall-Gondwana-Cli.ps1` | `dotnet tool` |
| `Reinstall-Gondwana-Templates.ps1` | `dotnet new` |

---

# Changelog tooling

Gondwana maintains two related changelog views:

```text
root CHANGELOG.md
    └── canonical repository-wide grouped history

project CHANGELOG.md files
    └── package/project-specific histories
```

The changelog configuration and generators were refactored so that one metadata file now controls both output types.

## `Changelog-ProjectGroups.ps1`

`Changelog-ProjectGroups.ps1` is the single authoritative changelog metadata source.

Each configured entry can independently choose whether it receives its own project changelog and whether its changes appear in the grouped root changelog.

Typical entry:

```powershell
[pscustomobject]@{
    Path = "Tooling/SomeProject"
    RootName = "Tooling / SomeProject"
    GenerateChangelog = $true
    IncludeInRootChangelog = $false
}
```

The two output flags are independent:

| `GenerateChangelog` | `IncludeInRootChangelog` | Result |
|---|---|---|
| `$true` | `$true` | Project CHANGELOG and root entries |
| `$true` | `$false` | Project CHANGELOG only |
| `$false` | `$true` | Root entries only |
| `$false` | `$false` | Neither output |

For ordinary projects, `Path` is the repository-relative project folder and also supplies the default Git history filter.

Root-only areas such as **Build / Repository** can use `Path = $null` plus explicit `IncludePaths` globs. Such entries must not request a project changelog.

Disabling project generation does not delete an existing historical `CHANGELOG.md`; it simply stops generating that output going forward.

When adding a new deployable project, register it here deliberately rather than relying on a generator's hard-coded project list.

## `Generate-Project-Changelogs.ps1`

This script generates and refreshes project-specific changelogs using `git-cliff`.

It now loads `Changelog-ProjectGroups.ps1` and processes configured entries where:

```text
GenerateChangelog = $true
```

The optional `-Projects` parameter is a **selection of configured project paths**. It is no longer an escape hatch for arbitrary unregistered folders. Unknown paths are rejected and disabled project-changelog entries are not generated.

For a project with no changelog yet, the script can bootstrap its complete history from Git tags plus the current untagged range.

For an existing changelog, released history is treated as authoritative. The generator replaces only the leading current-development section and leaves versioned history untouched.

Without `-Tag`, current commits are generated under:

```markdown
# [Unreleased]
```

With `-Tag vX.Y.Z`, the current range is written under that release version instead.

Useful commands:

```powershell
# Refresh all enabled project changelogs
.\Tooling\scripts\Generate-Project-Changelogs.ps1

# Preview without writing
.\Tooling\scripts\Generate-Project-Changelogs.ps1 -PreviewOnly

# Generate a subset of configured projects
.\Tooling\scripts\Generate-Project-Changelogs.ps1 `
    -Projects @("Gondwana", "Gondwana.WinForms")

# Freeze current changes into a release section
.\Tooling\scripts\Generate-Project-Changelogs.ps1 -Tag v2.6.0
```

Parameters:

| Parameter | Purpose |
|---|---|
| `-Tag` | Stamp the current range with a version instead of `[Unreleased]` |
| `-PreviewOnly` | Print generated output without writing files |
| `-Projects` | Narrow generation to configured project paths |
| `-CliffConfigPath` | Use a different `cliff.toml` configuration |

The script writes through temporary files and reports project failures after processing, avoiding destruction of existing changelogs when `git-cliff` fails.

## `Generate-Root-Changelog.ps1`

The root generator produces the leading grouped section of the repository-level `CHANGELOG.md`.

It loads entries where:

```text
IncludeInRootChangelog = $true
```

independently of whether those entries also generate their own project changelogs.

It preserves existing released history and replaces only the current derived section. It deliberately does **not** bootstrap a missing root changelog; the canonical root file must already exist.

When no configured root-visible changes exist, generation is a no-op. In that case it does not invent an empty release section or rewrite the existing file merely to remove a stale block.

Useful commands:

```powershell
# Refresh the grouped root [Unreleased] section
.\Tooling\scripts\Generate-Root-Changelog.ps1

# Preview the complete resulting root changelog
.\Tooling\scripts\Generate-Root-Changelog.ps1 -PreviewOnly

# Freeze the current grouped range into a release section
.\Tooling\scripts\Generate-Root-Changelog.ps1 -Tag v2.6.0
```

Parameters:

| Parameter | Purpose |
|---|---|
| `-Tag` | Stamp the current grouped range with a release version |
| `-PreviewOnly` | Print the complete resulting root changelog without writing it |
| `-SectionOnly` | Return only the generated current section; used internally by `release.ps1` |
| `-ChangelogPath` | Use a different root changelog path |
| `-CliffConfigPath` | Use a different `cliff.toml` configuration |

## `Test-ChangelogConfiguration.ps1`

This integration suite exists specifically to protect the metadata-driven changelog behavior.

Run it with PowerShell 7 and `git-cliff` on `PATH`:

```powershell
.\Tooling\scripts\Test-ChangelogConfiguration.ps1
```

It creates isolated temporary Git history and exercises, among other things:

- all four combinations of `GenerateChangelog` and `IncludeInRootChangelog`
- missing project-changelog bootstrap
- preview safety
- configured-project selection policy
- PR-link behavior
- repeated/idempotent refreshes
- tagged generation
- released-history preservation
- root no-op behavior when only excluded changes exist

The weekly changelog workflow runs this suite before touching the repository's real changelogs.

## Weekly changelog refresh

The old push-driven `changelog-master.yml` model has been retired.

Current running `[Unreleased]` sections are maintained by:

```text
.github/workflows/changelog-weekly.yml
```

It runs once each Sunday night in US Eastern time and can also be started manually from the Actions tab.

The workflow:

1. checks out current `master`
2. installs `git-cliff`
3. runs `Test-ChangelogConfiguration.ps1`
4. runs both changelog generators
5. stages only `CHANGELOG.md` files
6. exits successfully when there is nothing to change
7. commits the refresh directly to `master` when needed
8. uses a lease-protected push so it fails instead of overwriting a concurrent update to `master`

The workflow deliberately requires a token that can trigger downstream workflows; the default Actions token is not used for that push.

Normal development therefore looks like:

```text
merged development accumulates on master
              ↓
weekly/manual changelog refresh
              ↓
validate changelog configuration
              ↓
refresh root + project [Unreleased] sections
              ↓
commit directly to master only if files changed
```

This avoids rewriting changelogs on every pull request or every push while still keeping the running development history reasonably current.

---

# `release.ps1`

`release.ps1` is the local release orchestrator.

It prepares the release and pushes the release tag; it does **not** directly publish NuGet packages itself. Publication is performed by GitHub Actions after the tag reaches GitHub.

The script:

1. verifies the repository is clean, on the required branch, and synchronized with the remote
2. runs the Gondwana unit tests and stops on failure
3. resolves the version with Nerdbank.GitVersioning (`nbgv`)
4. generates a root release-notes preview with `git-cliff`
5. requires explicit deployment confirmation for a real release
6. runs `Generate-Root-Changelog.ps1 -Tag vX.Y.Z`
7. runs `Generate-Project-Changelogs.ps1 -Tag vX.Y.Z`
8. commits enabled changelog outputs
9. verifies that the NBGV version did not unexpectedly change after the changelog commit
10. creates the version tag
11. atomically pushes the branch and tag so neither remote ref is updated unless both updates succeed

After the tag arrives, `.github/workflows/release.yml` performs the remote build and publication, including package publishing and stable API-documentation publication.

Use preview mode before a release:

```powershell
.\Tooling\scripts\release.ps1 -PreviewOnly
```

Preview mode still fetches remote state, requires a clean working tree, runs the Gondwana unit tests, resolves the version, and generates the root release-notes preview. It skips the required-branch/alignment enforcement, changelog writes, deployment confirmation, commit/tag creation, and push so maintainers can validate the release path from a non-release branch.

For a real release:

```powershell
.\Tooling\scripts\release.ps1
```

The script requires the maintainer to type:

```text
DEPLOY
```

before making release changes.

Parameters:

| Parameter | Default | Purpose |
|---|---|---|
| `-Remote` | `origin` | Git remote used for synchronization and push |
| `-RequiredBranch` | `master` | Branch required for a normal release |
| `-ChangelogPath` | `CHANGELOG.md` | Root changelog location |
| `-CliffConfigPath` | `cliff.toml` | `git-cliff` configuration |
| `-PreviewOnly` | off | Validate and preview without deployment |

The release flow is intentionally conservative because a published NuGet package version is immutable.

---

# API documentation tooling

`Tooling/scripts/api-docs/` implements Gondwana's versioned Doxygen documentation system.

The published structure is:

| URL | Meaning |
|---|---|
| `/api/` | Redirect to the highest published stable release |
| `/api/latest/` | Current `master` development documentation |
| `/api/vX.Y.Z/` | Immutable documentation snapshot for a stable release |

The important implementation scripts are:

### `build.py`

Builds the Doxygen site for a specified version/source tree.

It:

- generates the Doxygen header and injects Gondwana's API-version selector
- keeps documentation configuration and presentation assets tied to the current tooling checkout even when source comes from a historical tag
- treats UTF-8 as the default source encoding
- uses `source_filter.py` for supported transcoding of legacy Windows-1252 C# files without rewriting the source snapshots
- validates generated HTML as strict UTF-8 before publication

### `source_filter.py`

Acts as Doxygen's supported `INPUT_FILTER` for legacy source encodings.

Valid UTF-8 passes through unchanged; Windows-1252 input is transcoded to UTF-8 on stdout.

### `install-doxygen.sh`

Installs the pinned official Linux x64 **Doxygen 1.14.0** archive used by CI and verifies its SHA-256 checksum and executable version.

This avoids relying on a distribution's potentially older or unpinned Doxygen package.

### `publish.py`

Applies a built documentation tree to a Pages staging tree while enforcing Gondwana's publication policy:

- `api/latest/` is replaceable development output
- `api/vX.Y.Z/` release snapshots are create-once and retained unchanged
- `api/versions.json` is rebuilt from canonical published releases
- `api/index.html` is updated only by release publication so an old rerun cannot downgrade the stable redirect

### `publish.sh`

Wraps publication against the `gh-pages` branch.

It fetches the current branch, applies the scoped update in a disposable worktree, and performs a normal fast-forward push. On concurrent publication it refetches and reapplies the update rather than replacing the entire branch.

## Workflow usage

Development API docs are published by:

```text
.github/workflows/docs.yml
```

for relevant changes on `master` and on manual dispatch. That manual dispatch path still checks out `master` explicitly and only republishes `/api/latest/`; it does not publish a stable `api/vX.Y.Z/` snapshot or update the stable `/api/` redirect.

Stable release docs are published by the API-docs job in:

```text
.github/workflows/release.yml
```

using the triggering canonical `vX.Y.Z` release tag. Correspondingly, manual/local publication through `publish.py` and `publish.sh` is restricted to `latest` or canonical `vX.Y.Z` targets, and only canonical release publication updates `api/index.html`.

The historical one-time API archive migration is complete. Its executable rebuild tooling was retired; `history-validation.md` remains as the retained migration record.

## Local validation

See `Tooling/scripts/api-docs/README.md` for the full validation matrix and publishing rules.

Common checks include:

```sh
python3 -m unittest discover -s Tooling/scripts/api-docs -p 'test_*.py' -v
node --check docs/doxy/api-versions.js
bash -n Tooling/scripts/api-docs/publish.sh
python3 Tooling/scripts/api-docs/build.py \
    --version 'Development (master) - 2.6.0-test' \
    --output /tmp/gondwana-api-build
```

---

# Wiki synchronization tooling

`Tooling/scripts/wiki-sync/sync.py` is the shared implementation behind Gondwana's bidirectional repository/Wiki synchronization.

The two supported editing surfaces are:

```text
docs/wiki/
GitHub Wiki UI
```

The normal workflows are:

```text
.github/workflows/wiki-sync.yml
.github/workflows/wiki-reconcile-weekly.yml
```

The event-driven workflow handles normal synchronization. The weekly reconciliation workflow is a safety net for missed events, deletions, incomplete rename events, interrupted runs, or other drift.

## `sync.py`

The script compares actual Git content from both sides and supports both directions:

| Direction | Authoritative source | Destination |
|---|---|---|
| `repo-to-wiki` | current `master` `docs/wiki` | GitHub Wiki |
| `wiki-to-repo` | current GitHub Wiki | shared automation branch/PR |

Wiki-to-repository imports use the shared:

```text
automation/wiki-sync
```

branch and PR titles beginning with:

```text
docs(wiki):
```

Conflicting two-sided edits are not silently resolved. The workflow fails so a maintainer can choose the correct version through the normal review process.

## `baseline.json`

This state file records the synchronization checkpoint used to identify the common acknowledged content between the repository and Wiki histories.

It lives outside `docs/**` so Wiki-import PRs are not accidentally skipped by documentation-only CI path filters.

## `test_sync.py`

Run the isolated Git-based synchronization tests with:

```console
python3 -m unittest discover -s Tooling/scripts/wiki-sync -p 'test_*.py' -v
```

For operational details—including manual directional sync, conflict recovery, token requirements, checkpoints, PR behavior, and release/changelog exclusions—see:

```text
docs/wiki/README.md
```

That repository-only README is intentionally excluded from publication to the GitHub Wiki.

---

# Safety and failure behavior

The PowerShell maintenance scripts generally fail fast and check the exit status of external commands.

That matters for:

- builds
- tests
- Git operations
- package installation
- changelog generation
- releases

The local reinstall scripts mostly modify disposable developer state and can normally be rerun after a failure.

The release and publication tools operate on persistent repository or public package state and therefore include stronger validation, preview, conflict/race detection, or explicit confirmation.

In particular:

- use `-PreviewOnly` before a real release
- changelog automation refuses to overwrite a moved `master`
- API publication preserves immutable release snapshots
- Wiki synchronization fails on ambiguous two-sided edits rather than choosing one silently

---

# Mental model

The repository tooling now falls into four main layers:

```text
DEVELOPMENT ENVIRONMENT
└── Setup-Gondwana-Dev.ps1
        prepares the machine

LOCAL ITERATION
├── Reinstall-Gondwana-Cli.ps1
└── Reinstall-Gondwana-Templates.ps1
        test unpublished local packages

CHANGELOG / RELEASE MANAGEMENT
├── Changelog-ProjectGroups.ps1
│       defines independent project/root changelog policy
├── Generate-Root-Changelog.ps1
│       maintains repository-wide history
├── Generate-Project-Changelogs.ps1
│       maintains project-specific history
├── Test-ChangelogConfiguration.ps1
│       protects generator/configuration behavior
└── release.ps1
        freezes current history into a version and starts deployment

DOCUMENTATION AUTOMATION
├── api-docs/
│       builds and publishes versioned API documentation
└── wiki-sync/
        keeps docs/wiki and the GitHub Wiki synchronized
```

Around those scripts, GitHub Actions supplies the automation boundaries:

```text
Sunday night / manual dispatch
        ↓
changelog-weekly.yml
        ↓
validate + refresh [Unreleased]

relevant master API changes
        ↓
docs.yml
        ↓
publish /api/latest/

repository or Wiki documentation changes
        ↓
wiki-sync.yml
        ↓
bidirectional Wiki synchronization

weekly Wiki safety net
        ↓
wiki-reconcile-weekly.yml
        ↓
full-tree reconciliation

release tag
        ↓
release.yml
        ↓
packages + stable API documentation
```

The key distinction is:

> **Setup** prepares the developer.  
> **Reinstall** scripts support unpublished local iteration.  
> **Changelog** tooling derives development and release history from Git using shared metadata.  
> **Release** freezes that history into a version and starts publication.  
> **Documentation automation** keeps API and Wiki documentation synchronized and publishable.

None of these tools are required to run a Gondwana game. They exist to make development, release engineering, and documentation maintenance predictable and repeatable.
