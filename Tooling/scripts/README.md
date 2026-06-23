# Scripts

This folder contains PowerShell helper scripts for building, publishing, and releasing Gondwana projects. All scripts require **PowerShell 5.1** or later.

- [`Setup-Gondwana-Dev.ps1`](#setup-gondwana-devps1)
- [`Reinstall-Gondwana-Cli.ps1`](#reinstall-gondwana-clips1)
- [`Reinstall-Gondwana-Templates.ps1`](#reinstall-gondwana-templatesps1)
- [`Generate-Project-Changelogs.ps1`](#generate-project-changelogsps1)
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

Generates a `CHANGELOG.md` for each library project using [`git-cliff`](https://git-cliff.org/), filtering commits by changed file paths so each project only shows the changes that affected it. This is the standard monorepo approach described in the git-cliff docs. `release.ps1` invokes this script as part of the release flow after updating the root changelog.

**What it does:**
1. Iterates over the default set of library/tooling projects (all `Gondwana.*` projects and `Tooling/*` projects; Demos and `Gondwana.Tests` are excluded).
2. Runs `git-cliff --include-path "Project/**/*"` for each project.
3. Writes the result to `<ProjectFolder>/CHANGELOG.md`, using `--output` for new files and `--prepend` to add a new section to existing ones.
4. Reports all failures at the end rather than stopping on the first.

> A single commit that touches multiple projects will appear in each matching project changelog — correct behaviour for a monorepo.
>
> The repository's canonical release history remains the root `CHANGELOG.md`, which includes all release changes across projects.

**Prerequisites:**
- [`git-cliff`](https://git-cliff.org/) on `PATH` — install with `winget install --id orhun.git-cliff`.
- A `cliff.toml` config file at the repository root.

**Parameters:**

| Parameter | Description | Default |
|---|---|---|
| `-Tag` | Version tag to stamp on unreleased commits (e.g. `v1.2.3`). | — |
| `-Unreleased` | Pass `--unreleased` to git-cliff; only commits since the last tag are shown. | — |
| `-PreviewOnly` | Print generated sections to the console without writing any files. | — |
| `-Projects` | Override the default project list (relative paths from repo root). | All library projects |
| `-CliffConfigPath` | Path to the `cliff.toml` config. Relative paths are resolved from the repo root. | `cliff.toml` |

**Examples:**
```powershell
# Preview unreleased changes for all projects without touching disk
.\Generate-Project-Changelogs.ps1 -Unreleased -PreviewOnly

# Write changelogs for all projects (unreleased commits)
.\Generate-Project-Changelogs.ps1 -Unreleased

# Generate changelogs for a specific release tag
.\Generate-Project-Changelogs.ps1 -Tag v1.2.3 -Unreleased

# Generate changelogs for a subset of projects only
.\Generate-Project-Changelogs.ps1 -Projects @("Gondwana", "Gondwana.WinForms") -Unreleased
```

---

### `release.ps1`

Creates a new versioned release of Gondwana: updates the changelog, commits it, creates a Git tag, and pushes everything to trigger the GitHub Actions release workflow, which then publishes all packages to NuGet.

**What it does:**
1. Validates that the working tree is clean, on the correct branch, and in sync with the remote.
2. Runs `Gondwana.Tests` unit tests and stops immediately if any test fails.
3. Resolves the next version using [`nbgv`](https://github.com/dotnet/Nerdbank.GitVersioning) (Nerdbank.GitVersioning).
4. Previews the new changelog section generated by [`git-cliff`](https://git-cliff.org/) and prompts for confirmation.
5. Prepends the new section to `CHANGELOG.md`.
6. Runs `Generate-Project-Changelogs.ps1` to update per-project `CHANGELOG.md` files.
7. Commits all changelog updates and creates/pushes a `vX.Y.Z` Git tag to trigger the GitHub Actions release workflow.

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
| `-ChangelogPath` | Path to the changelog file, relative to the script or absolute. | `CHANGELOG.md` |
| `-CliffConfigPath` | Path to the `git-cliff` config file, relative to the script or absolute. | `cliff.toml` |
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
