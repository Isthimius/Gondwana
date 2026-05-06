# Scripts

This folder contains PowerShell helper scripts for building, publishing, and releasing Gondwana projects. All scripts require **PowerShell 5.1** or later.

---

## Scripts

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

### `release.ps1`

Creates a new versioned release of Gondwana: updates the changelog, commits it, creates a Git tag, and pushes everything to trigger the GitHub Actions release workflow.

**What it does:**
1. Validates that the working tree is clean, on the correct branch, and in sync with the remote.
2. Resolves the next version using [`nbgv`](https://github.com/dotnet/Nerdbank.GitVersioning) (Nerdbank.GitVersioning).
3. Previews the new changelog section generated by [`git-cliff`](https://git-cliff.org/) and prompts for confirmation.
4. Prepends the new section to `CHANGELOG.md` and commits it.
5. Creates and pushes a `vX.Y.Z` Git tag to trigger the GitHub Actions release workflow.

> **This is a destructive operation.** Once a version is published to NuGet it cannot be undone. Use `-PreviewOnly` to inspect the release notes before committing.

**Prerequisites:**
- [`git`](https://git-scm.com/) on `PATH`.
- [`nbgv`](https://github.com/dotnet/Nerdbank.GitVersioning) on `PATH` — install with `dotnet tool install -g nbgv`.
- [`git-cliff`](https://git-cliff.org/) on `PATH` — install with `winget install git-cliff`.
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
