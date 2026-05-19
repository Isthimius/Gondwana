# Scripts

This folder contains PowerShell helper scripts for building, publishing, and releasing Gondwana projects. All scripts require **PowerShell 5.1** or later.

---

## Scripts

### `Setup-Gondwana-Dev.ps1`

Idempotent one-shot setup script for new contributors. Run it once after cloning the repository to install everything needed to build, run, and develop Gondwana. Safe to re-run — install/restore steps also refresh to the latest available versions where applicable.

**What it does:**
1. Verifies Git is available on `PATH`.
2. Checks for the .NET 8 SDK; installs it via `winget` if missing (Windows only).
3. Restores local .NET tools (`nbgv`) from `.config/dotnet-tools.json` with cache bypass.
4. Restores NuGet packages for the solution with dependency reevaluation.
5. Builds the solution in `Release` configuration.
6. Installs/updates the `Gondwana.Cli` global tool (`gondwana`).
7. Installs `Gondwana.Templates` (`gondwana-winforms`, `gondwana-avalonia`, `gondwana-wasm`) and applies template updates when already installed.
8. Installs the `dotnet wasm-tools` workload for WebAssembly support and updates installed workloads when it is already present.
9. Checks for SDL2 native binaries (required by `Gondwana.Input.SDL2`) and prints install guidance if missing.
10. Checks for LibVLC native binaries (required by `Gondwana.Video`); installs VLC via `winget` if missing on Windows.
11. Ensures `git-cliff` is installed; updates it via `winget` when available on Windows.
12. Checks whether `butler` (itch.io) is installed and prints manual install guidance if missing.
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

### `Publish-Gondwana-Wasm.ps1`

Builds and publishes a Gondwana project for browser (WASM) deployment.

**What it does:**
1. Optionally installs/updates the `wasm-tools` .NET workload.
2. Runs `dotnet publish -f net8.0-browser` against the specified project.
3. Outputs the path to the generated `AppBundle` directory on success.

**Prerequisites:**
- .NET 8 SDK

**Parameters:**

| Parameter | Description | Default |
|---|---|---|
| `-Project` | Path to the `.csproj` file or a directory containing exactly one `.csproj`. | Current directory (`.`) |
| `-Configuration` | Build configuration (`Release`, `Debug`, etc.). | `Release` |
| `-SkipWorkload` | Skip `dotnet workload install wasm-tools` (useful when the workload is already installed). | — |

**Examples:**
```powershell
# Publish from the current directory
.\Publish-Gondwana-Wasm.ps1

# Publish a specific project directory
.\Publish-Gondwana-Wasm.ps1 -Project .\src\MyGame

# Skip workload install and use Debug configuration
.\Publish-Gondwana-Wasm.ps1 -SkipWorkload -Configuration Debug
```

---

### `Deploy-Gondwana-Itch.ps1`

Packages a Gondwana WASM build and uploads it to [itch.io](https://itch.io) via the `butler` CLI tool.

**What it does:**
1. Optionally builds the project by calling `Publish-Gondwana-Wasm.ps1`.
2. Zips the contents of the `AppBundle` directory with `index.html` at the root.
3. Pushes the zip to the specified itch.io game and channel using `butler`.

**Prerequisites:**
- [`butler`](https://itch.io/docs/butler/) installed and on `PATH`, authenticated via `butler login`.
- The game must already exist on itch.io.

**Parameters:**

| Parameter | Description | Default |
|---|---|---|
| `-Project` | Path to the `.csproj` file or a directory containing exactly one `.csproj`. | Current directory (`.`) |
| `-ItchGame` | **Required.** The itch.io game slug in `user/game` form (e.g. `isthimius/mygame`). | — |
| `-ItchChannel` | The itch.io release channel name. | `html5` |
| `-Configuration` | Build configuration. | `Release` |
| `-SkipBuild` | Skip the `dotnet publish` step and use an existing `AppBundle`. | — |
| `-SkipWorkload` | Skip `dotnet workload install wasm-tools` during the publish step. | — |

**Examples:**
```powershell
# Full build and deploy
.\Deploy-Gondwana-Itch.ps1 -ItchGame "isthimius/mygame"

# Deploy to a different channel without rebuilding
.\Deploy-Gondwana-Itch.ps1 -ItchGame "isthimius/mygame" -SkipBuild -ItchChannel "html5-beta"

# Build from a specific project directory
.\Deploy-Gondwana-Itch.ps1 -Project .\src\MyGame -ItchGame "isthimius/mygame"
```

---

### `Deploy-Gondwana-Website.ps1`

Publishes a Gondwana WASM build to a personal static website — either a local web root or a remote server via `rsync`.

**What it does:**
1. Optionally builds the project by calling `Publish-Gondwana-Wasm.ps1`.
2. Copies the `AppBundle` contents to the specified destination, replacing stale files.
   - On Windows (local): uses `robocopy /MIR`.
   - On Linux/macOS (local) or any remote: uses `rsync --delete`.

> **Important:** Your web server must send the following HTTP headers on every response for .NET WASM threading (`SharedArrayBuffer`) to work:
> ```
> Cross-Origin-Opener-Policy:   same-origin
> Cross-Origin-Embedder-Policy: require-corp
> ```
> The site must also be served over **HTTPS**.

**Prerequisites:**
- For remote deployment: `rsync` on `PATH` (available via WSL, macOS, Git Bash, or native Linux).

**Parameters:**

| Parameter | Description | Default |
|---|---|---|
| `-Project` | Path to the `.csproj` file or a directory containing exactly one `.csproj`. | Current directory (`.`) |
| `-WebRoot` | Local destination directory. Required when not using `-RemoteHost`. | — |
| `-RemoteHost` | SSH remote in `user@host` form for rsync deployment. Requires `-RemotePath`. | — |
| `-RemotePath` | Remote destination path (e.g. `/var/www/html/mygame`). Required with `-RemoteHost`. | — |
| `-Configuration` | Build configuration. | `Release` |
| `-SkipBuild` | Skip the `dotnet publish` step and use an existing `AppBundle`. | — |
| `-SkipWorkload` | Skip `dotnet workload install wasm-tools` during the publish step. | — |

**Examples:**
```powershell
# Copy to a local IIS / nginx web root
.\Deploy-Gondwana-Website.ps1 -WebRoot "C:\inetpub\wwwroot\mygame"

# Deploy to a remote server via rsync (Linux/macOS/WSL)
.\Deploy-Gondwana-Website.ps1 -RemoteHost "deploy@mysite.com" -RemotePath "/var/www/html/mygame"

# Skip build if AppBundle already exists
.\Deploy-Gondwana-Website.ps1 -WebRoot "C:\inetpub\wwwroot\mygame" -SkipBuild
```

---

### `Generate-Project-Changelogs.ps1`

Generates a `CHANGELOG.md` for each library project using [`git-cliff`](https://git-cliff.org/), filtering commits by changed file paths so each project only shows the changes that affected it. This is the standard monorepo approach described in the git-cliff docs. It is also called automatically by `release.ps1` as part of every release.

**What it does:**
1. Iterates over the default set of library/tooling projects (all `Gondwana.*` projects and `Tooling/*` projects; Demos and `Gondwana.Tests` are excluded).
2. Runs `git-cliff --include-path "Project/**/*"` for each project.
3. Writes the result to `<ProjectFolder>/CHANGELOG.md`, using `--output` for new files and `--prepend` to add a new section to existing ones.
4. Reports all failures at the end rather than stopping on the first.

> A single commit that touches multiple projects will appear in each matching project changelog — correct behaviour for a monorepo.

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

Creates a new versioned release of Gondwana: updates the changelog, commits it, creates a Git tag, and pushes everything to trigger the GitHub Actions release workflow.

**What it does:**
1. Validates that the working tree is clean, on the correct branch, and in sync with the remote.
2. Runs `Gondwana.Tests` unit tests and stops immediately if any test fails.
3. Resolves the next version using [`nbgv`](https://github.com/dotnet/Nerdbank.GitVersioning) (Nerdbank.GitVersioning).
4. Previews the new changelog section generated by [`git-cliff`](https://git-cliff.org/) and prompts for confirmation.
5. Prepends the new section to `CHANGELOG.md` and commits it.
6. Creates and pushes a `vX.Y.Z` Git tag to trigger the GitHub Actions release workflow.

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
