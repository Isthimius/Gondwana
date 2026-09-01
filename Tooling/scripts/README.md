# Scripts

This folder contains PowerShell helper scripts for building, publishing, and releasing Gondwana projects. All scripts require **PowerShell 5.1** or later.

- [`Setup-Gondwana-Dev.ps1`](#setup-gondwana-devps1)
- [`Reinstall-Gondwana-Cli.ps1`](#reinstall-gondwana-clips1)
- [`Reinstall-Gondwana-Templates.ps1`](#reinstall-gondwana-templatesps1)
- [`Generate-Project-Changelogs.ps1`](#generate-project-changelogsps1)
- [`Generate-Root-Changelog.ps1`](#generate-root-changelogps1)
- [`Changelog-ProjectGroups.ps1`](#changelog-projectgroupsps1-support-file)
- [`release.ps1`](#releaseps1)

---

## Scripts

### `Setup-Gondwana-Dev.ps1`

Idempotent one-shot setup script for new contributors to the Gondwana project. Run it once after cloning the repository to install everything needed to build, run, and develop Gondwana. Safe to re-run — install/restore steps also refresh to the latest available versions where applicable while retaining a newer already-installed `Gondwana.Templates` package.

**What it does:**
1. Verifies Git is available on `PATH`.
2. Checks for the .NET 8 SDK; installs it via `winget` if missing (Windows only).
3. Restores local .NET tools (`nbgv`) from `.config/dotnet-tools.json` with cache bypass.
4. Restores NuGet packages for the solution with dependency reevaluation.
5. Builds the solution in `Release` configuration.
6. Installs/updates the `Gondwana.Cli` global tool (`gondwana`).
7. Installs `Gondwana.Templates` (`gondwana-winforms`, `gondwana-avalonia`, `gondwana-wasm`) when missing, otherwise checks for template updates and keeps a newer already-installed local package instead of downgrading it.
8. Installs the `dotnet wasm-tools` workload for WebAssembly support and updates installed workloads when it is already present.
9. Checks for SDL2 native binaries (required by `Gondwana.Input.SDL2`) and prints install guidance (including the official SDL releases page) if missing.
10. Checks for LibVLC native binaries (required by `Gondwana.Video`); installs VLC via `winget` if missing on Windows.
11. Ensures `git-cliff` is installed; updates it via `winget` when available on Windows.
12. Installs `butler` (itch.io) by downloading the latest binary from the [broth CDN](https://itch.io/docs/butler/installing.html), trying `broth.itch.ovh` first and `broth.itch.zone` as fallback, and extracting it to `%LOCALAPPDATA%\itch\butler` (Windows) or `~/.itch/butler` (Linux/macOS). Adds the directory to the current session PATH and prints a reminder to add it permanently. Prints `butler login` instructions after install.
13. Runs `gondwana doctor` to confirm the final environment state.

**Prerequisites:**
- Git on `PATH`
- PowerShell 5.1 or later

**Parameters:**

| Parameter | Description | Default |
|---|---|---|
| `-SkipBuild` | Skip step 5 (`dotnet build`). Restores packages and tools only. | — |
| `-SkipOptional` | Skip steps 8–12 (wasm-tools, SDL2, LibVLC, git-cliff, butler). | — |

**Examples:**
```powershell
# Full setup — run from anywhere inside the cloned repository
.\Setup-Gondwana-Dev.ps1

# Skip building the solution
.\Setup-Gondwana-Dev.ps1 -SkipBuild

# Install core tools only (no WASM workload, SDL2, LibVLC, git-cliff, or butler)
.\Setup-Gondwana-Dev.ps1 -SkipOptional
```

---

### `Reinstall-Gondwana-Cli.ps1`

Packs `Tooling/Gondwana.Cli` and reinstalls the global `gondwana` tool from an isolated local package source. Useful for repeated local CLI testing when the package version has not changed.

**What it does:**
1. Packs `Tooling/Gondwana.Cli/Gondwana.Cli.csproj` into a local package feed.
2. Uninstalls the existing global `Gondwana.Cli` tool when present.
3. Reinstalls `Gondwana.Cli` globally from the freshly packed local package feed using `--source`, `--no-http-cache`, and a temporary `NUGET_PACKAGES` directory so the local package is chosen deterministically.
4. Prints the installed `gondwana --version` output when available in the current shell.

**Prerequisites:**
- .NET 8 SDK
- PowerShell 5.1 or later

**Parameters:**

| Parameter | Description | Default |
|---|---|---|
| `-Configuration` | Build configuration passed to `dotnet pack`. | `Release` |
| `-PackageOutput` | Local package-feed directory. Relative paths are resolved from the repository root. | `.local-nuget` |

**Examples:**
```powershell
# Repack and reinstall the local CLI from the repository default package feed
.\Reinstall-Gondwana-Cli.ps1

# Use a different build configuration
.\Reinstall-Gondwana-Cli.ps1 -Configuration Debug

# Use a custom local package-feed directory
.\Reinstall-Gondwana-Cli.ps1 -PackageOutput artifacts\local-tools
```

---

### `Reinstall-Gondwana-Templates.ps1`

Packs `Tooling/Gondwana.Templates` and reinstalls the exact freshly packed template package from an isolated local package source. Useful for repeated local template iteration before a package version is published to NuGet.

**What it does:**
1. Packs `Tooling/Gondwana.Templates/Gondwana.Templates.csproj` into a local package feed.
2. Detects the exact version that was just packed.
3. Uninstalls the existing `Gondwana.Templates` package when present.
4. Reinstalls that exact packed version using `dotnet new install Gondwana.Templates@<packed-version> --add-source <feed> --force` and a temporary `NUGET_PACKAGES` directory so the local package is chosen deterministically.
5. Prints the installed template package version and the currently available Gondwana templates.

**Prerequisites:**
- .NET 8 SDK
- PowerShell 5.1 or later

**Parameters:**

| Parameter | Description | Default |
|---|---|---|
| `-Configuration` | Build configuration passed to `dotnet pack`. | `Release` |
| `-PackageOutput` | Local package-feed directory. Relative paths are resolved from the repository root. | `.local-nuget` |

**Examples:**
```powershell
# Repack and reinstall the local templates from the repository default package feed
.\Reinstall-Gondwana-Templates.ps1

# Use a different build configuration
.\Reinstall-Gondwana-Templates.ps1 -Configuration Debug

# Use a custom local package-feed directory
.\Reinstall-Gondwana-Templates.ps1 -PackageOutput artifacts\local-templates
```

---

### `Generate-Project-Changelogs.ps1`

Generates a `CHANGELOG.md` for each library project using [`git-cliff`](https://git-cliff.org/), filtering commits by changed file paths so each project only shows the changes that affected it. This is the standard monorepo approach described in the git-cliff docs. `release.ps1` invokes this script as part of the release flow, and `.github/workflows/changelog-master.yml` refreshes the running unreleased sections after non-changelog pushes to `master` before opening/updating an automation PR that is configured for auto-merge.

**What it does:**
1. Iterates over the default set of library/tooling projects (all `Gondwana.*` projects and `Tooling/*` projects; Demos and `Gondwana.Tests` are excluded).
2. Filters each project's history with `git-cliff --include-path "Project/**/*"`.
3. If a project has no `CHANGELOG.md` (or it is empty), generates the complete project history: existing Git tags become versioned sections and current untagged commits are included as `[Unreleased]`, unless `-Tag` is supplied.
4. If a project already has a changelog, replaces any leading `[Unreleased]` section and regenerates the current commits since the latest tag. Without `-Tag`, those commits are prepended under `[Unreleased]`; with `-Tag`, they are prepended under that version.
5. Writes through a temporary file so a failed `git-cliff` run does not destroy the existing changelog, and reports all project failures at the end.

> A single commit that touches multiple projects will appear in each matching project changelog — correct behaviour for a monorepo.
>
> Released history in an existing project changelog is treated as authoritative and is not regenerated during normal refreshes. The repository's canonical release history remains the root `CHANGELOG.md`, which includes all release changes across projects.

**Prerequisites:**
- [`git-cliff`](https://git-cliff.org/) on `PATH` — install with `winget install --id orhun.git-cliff`.
- A `cliff.toml` config file at the repository root.

**Parameters:**

| Parameter | Description | Default |
|---|---|---|
| `-Tag` | Version tag to stamp on the current unreleased commits (e.g. `v1.2.3`). When omitted, they remain under `[Unreleased]`. | — |
| `-PreviewOnly` | Print generated output to the console without writing any files. | — |
| `-Projects` | Override the default project list using paths relative to the repository root. | All library/tooling projects |
| `-CliffConfigPath` | Path to the `cliff.toml` config. Relative paths are resolved from the repository root. | `cliff.toml` |

**Examples:**
```powershell
# Refresh running [Unreleased] sections for all projects
.\Generate-Project-Changelogs.ps1

# Preview the current generated output without touching disk
.\Generate-Project-Changelogs.ps1 -PreviewOnly

# Freeze the current unreleased commits under a release version
.\Generate-Project-Changelogs.ps1 -Tag v1.2.3

# Refresh a subset of projects only
.\Generate-Project-Changelogs.ps1 -Projects @("Gondwana", "Gondwana.WinForms")
```

---

### `Generate-Root-Changelog.ps1`

Regenerates only the repository-level `CHANGELOG.md`'s leading derived section while preserving all existing released history exactly. Its entries are grouped by project/area in the same format used by release notes. `.github/workflows/changelog-master.yml` runs this script alongside `Generate-Project-Changelogs.ps1` after non-changelog pushes to `master` before opening/updating an automation PR that is configured for auto-merge.

**What it does:**
1. Loads the project/area definitions from `Changelog-ProjectGroups.ps1`.
2. Uses `git-cliff` to collect commits since the latest tag for each matching project/area.
3. Replaces any leading generated or manually edited `[Unreleased]` section.
4. Preserves the file header and every existing versioned section’s contents, only normalizing whitespace around the inserted current section.

> The canonical root `CHANGELOG.md` must already exist and contain a recognized `[Unreleased]` or versioned release heading. Unlike the project generator, this script deliberately does not bootstrap missing root history.

**Prerequisites:**
- [`git-cliff`](https://git-cliff.org/) on `PATH` — install with `winget install --id orhun.git-cliff`.
- A `cliff.toml` config file at the repository root.

**Parameters:**

| Parameter | Description | Default |
|---|---|---|
| `-Tag` | Version tag to stamp on the current unreleased commits (e.g. `v1.2.3`). When omitted, they remain under `[Unreleased]`. | — |
| `-PreviewOnly` | Print the complete resulting root changelog without modifying the file. | — |
| `-SectionOnly` | Internal mode used by `release.ps1` to return only the generated current section without modifying the file. | — |
| `-ChangelogPath` | Path to the root changelog. Relative paths are resolved from the repository root. | `CHANGELOG.md` |
| `-CliffConfigPath` | Path to the `cliff.toml` config. Relative paths are resolved from the repository root. | `cliff.toml` |

**Examples:**
```powershell
# Refresh the grouped root [Unreleased] section
.\Generate-Root-Changelog.ps1

# Preview the complete resulting root changelog without touching disk
.\Generate-Root-Changelog.ps1 -PreviewOnly

# Convert the root [Unreleased] section into a versioned release section
.\Generate-Root-Changelog.ps1 -Tag v1.2.3

# Use a non-default root changelog path
.\Generate-Root-Changelog.ps1 -ChangelogPath docs\CHANGELOG.md
```

---

### `Changelog-ProjectGroups.ps1` (support file)

Defines the project/area headings and `git-cliff` include paths used to build the grouped root changelog. It is dot-sourced by `Generate-Root-Changelog.ps1`; it is not intended to be executed directly.

Keeping these definitions in one support file ensures that automatic `[Unreleased]` updates and versioned release generation use identical headings and path filters. A commit that matches multiple groups intentionally appears under each matching heading.

---

### `release.ps1`

Creates a new versioned release of Gondwana: updates the changelog, commits it, creates a Git tag, and atomically pushes the branch and tag to trigger the GitHub Actions release workflow, which then publishes all packages to NuGet.

**What it does:**
1. Validates that the working tree is clean, on the correct branch, and in sync with the remote.
2. Runs `Gondwana.Tests` unit tests and stops immediately if any test fails.
3. Resolves the next version using [`nbgv`](https://github.com/dotnet/Nerdbank.GitVersioning) (Nerdbank.GitVersioning).
4. Previews the new changelog section generated by [`git-cliff`](https://git-cliff.org/) and prompts for confirmation.
5. Runs `Generate-Root-Changelog.ps1` with the resolved version tag, replacing the root `[Unreleased]` block with the versioned release section.
6. Runs `Generate-Project-Changelogs.ps1` with the same tag to replace each project's running `[Unreleased]` section with its versioned release section.
7. Commits all changelog updates, creates a `vX.Y.Z` Git tag, and atomically pushes the branch and tag so neither remote ref is updated unless both updates succeed.

> **This is a destructive operation.** Once a version is published to NuGet it cannot be undone. Use `-PreviewOnly` to inspect the release notes before committing.

**Prerequisites:**
- [.NET SDK 8.0+](https://dotnet.microsoft.com/download) on `PATH`.
- [`git`](https://git-scm.com/) on `PATH`.
- [`nbgv`](https://github.com/dotnet/Nerdbank.GitVersioning) on `PATH` — install with `dotnet tool install -g nbgv`.
- [`git-cliff`](https://git-cliff.org/) on `PATH` — install with `winget install --id orhun.git-cliff`.
- A `cliff.toml` config file at the repository root.

**Parameters:**

| Parameter | Description | Default |
|---|---|---|
| `-Remote` | Git remote name. | `origin` |
| `-RequiredBranch` | Branch that must be checked out before tagging. | `master` |
| `-ChangelogPath` | Path to the changelog file, relative to the repository root or absolute. | `CHANGELOG.md` |
| `-CliffConfigPath` | Path to the `git-cliff` config file, relative to the repository root or absolute. | `cliff.toml` |
| `-PreviewOnly` | Generate and display the release notes preview without making any changes. | — |

**Examples:**
```powershell
# Preview the release notes for the next version without making any changes
.\release.ps1 -PreviewOnly

# Create a release (prompts for confirmation before proceeding)
.\release.ps1

# Target a different remote or branch
.\release.ps1 -Remote upstream -RequiredBranch main
```
