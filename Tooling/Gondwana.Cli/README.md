# Gondwana CLI

Developer CLI for the [Gondwana Game Engine](https://github.com/Isthimius/Gondwana).

## Installation

```bash
dotnet tool install --global Gondwana.Cli
```

## Commands

### Project health and package management

`doctor` checks the machine and toolchain. `check` checks an existing project.
All three project commands below accept `-p|--project <csproj-or-directory>`;
without it, the current directory must contain exactly one `.csproj`.

```bash
gondwana check
gondwana check --project ./src/MyGame
gondwana check --fix
gondwana upgrade --dry-run
gondwana upgrade --version 2.6.0 --dry-run
gondwana upgrade --version 2.6.0 -p ./src/MyGame/MyGame.csproj
gondwana add widgets
gondwana add hosting --project ./src/MyGame
```

`check` reports project name, locally declared frameworks, detected platform,
Gondwana package and project references, version mismatches, optional hosting,
missing literal file references, browser configuration, asset bundles, and `.gts`
files. It excludes `bin`, `obj`, `.git`, `.vs`, `node_modules`, and directory links
from asset discovery. Failures return exit code 1; warnings alone return 0.
Unreadable scan directories or entries produce warnings; other directories are still scanned.
Conditional/imported settings and potentially build-generated resources are not
treated as definite errors. Protected bundles produce a warning; inspect them with
`assets validate --password` separately.

`check --fix` aligns older Gondwana package references to the project's explicitly
declared stable **Gondwana core** version when no Gondwana reference is newer.
It prints the changes and reruns checks. It never downgrades another reference,
selects a release track, adds optional hosting automatically, or rewrites source.
When no unambiguous fix exists, use `upgrade --version` or edit the project manually.

`upgrade` without `--version` queries nuget.org and selects the newest stable
version available for **every referenced Gondwana package**. It refuses automatic
downgrades and changes from a prerelease track. `--version` accepts an explicit
version (including prereleases), works offline, and leaves package availability
and framework compatibility to the next normal NuGet restore. Only referenced
Gondwana packages change. `--dry-run` prints each package's old/new version and
owning file without writing anything.

Package edits support version attributes, `<Version>` children, simple shared
version properties, `VersionOverride`, and straightforward central package
management in the nearest `Directory.Packages.props`. Central edits affect every
project consuming that central entry; their file path appears in the upgrade
preview. Whitespace, comments, BOM, and unrelated package references are preserved
as far as XML serialization permits. No restore/build runs implicitly.

For safety, edits refuse explicit MSBuild imports, conditional or duplicate
Gondwana references, unresolved/ranged versions, Update-only package references,
mixed source/package references, and properties also used by unrelated content.
This is literal project inspection, not full MSBuild evaluation. Source-reference-only
projects can be checked but must manage package versions in their source repository.

| Feature | Package selected on current master |
| --- | --- |
| `widgets` | `Gondwana.Widgets` |
| `audio` | `Gondwana.Audio.Browser` for Blazor; `Gondwana.Audio.NAudio` for Windows-targeted WinForms or other desktop projects; cross-platform Avalonia requires an explicitly chosen compatible backend |
| `midi` | `Gondwana.Audio.Midi` for Windows-targeted WinForms or other desktop projects; unavailable for Blazor and cross-platform desktop targets |
| `gamepad` | `Gondwana.Input.SDL2` (desktop; requires native SDL2) |
| `video` | `Gondwana.Video` (desktop; requires native LibVLC) |
| `video-widgets` | `Gondwana.Video.Widgets` (optional desktop Widget bridge; requires native LibVLC) |
| `hosting` | `Gondwana.WinForms.Hosting`, `Gondwana.Avalonia.Hosting`, or `Gondwana.Blazor.Hosting`, based on an unambiguous adapter |

`add` uses the existing aligned Gondwana version and makes no change if the
feature is already referenced. It refuses to guess a version for non-Gondwana
projects. It adds references only; application setup remains in your code.

A normal `net8.0` Avalonia project is left unchanged by `add audio` or `add midi`.
The command explains that no cross-platform desktop audio backend is automatically
selected. Explicitly choose/configure a compatible backend; the CLI never makes
an Avalonia project Windows-only to install audio. A multi-target desktop project
must target Windows in every declared framework for automatic NAudio/MIDI selection.

### Asset inspection and validation

```bash
gondwana assets pack ./Assets ./game.assets
gondwana pack ./Assets ./game.assets
gondwana assets list ./game.assets
gondwana assets inspect ./game.assets
gondwana assets validate ./game.assets
gondwana assets validate ./game.assets --password secret
gondwana assets unpack ./game.assets ./Extracted
gondwana assets unpack ./game.assets ./Extracted --overwrite --type Image
```

Top-level `pack` remains an alias with its existing options and overwrite behavior.
`assets extract` remains available as an alias for `assets unpack`, and
`assets generate-keys` is registered alongside the other asset commands.
`list` and `inspect` display native asset type, name/path, and uncompressed bytes;
`inspect` also shows the bundle path, total entry count, and bundle file size.
Listing and inspection check archive structure and keys but skip the full integrity-data
pass; use `assets validate` for that check. The engine still loads entry data once.
`list`, `inspect`, and `unpack` accept `-t|--type` and `-p|--password`.

Validation checks ZIP integrity/decryption, native entry keys, duplicate keys,
portable safe paths, and packed GTS definitions using their containing bundle for
image references. Other binary assets are integrity checked, not decoded as media.
Invalid bundles return 1. Extraction preflights all paths and refuses traversal,
symbolic links/junctions, ambiguous destination names, and existing files unless
`--overwrite` is supplied. Paths are preserved using the names stored in the
bundle (the engine writes lowercase names). A failed preflight writes no files;
an I/O failure during extraction can leave already-extracted files.

### Tilesheet inspection and validation

```bash
gondwana tilesheet info ./Assets/forest.gts
gondwana tilesheet validate ./Assets/forest.gts
```

These commands use the engine's GTS serializer and shared layout validator.
Information includes image source/header dimensions, region and tile dimensions,
frame grid sizes, explicit frame metadata counts, collision defaults, overhang,
mask presence, and alpha handling. Validation reports malformed definitions,
missing/unreadable image sources, ambiguous image references, region bounds,
invalid dimensions/padding/margins, negative overhang, inverted collision
rectangles, duplicate region names/frame coordinates, invalid collision types,
and frame metadata outside the grid. Negative collision insets are valid expansion;
zero-sized collision geometry is permitted. Image header inspection does not
validate every encoded pixel. No runtime tilesheet is registered or changed.

During CLI inspection, loose image/bundle paths must stay within the GTS directory.
Packed definitions use the containing bundle and resolve relative paths within its
directory. Absolute paths, parent traversal, and links are rejected before opening
the referenced image or bundle. This is stricter than runtime engine loading so
inspecting an untrusted definition cannot open arbitrary external files.
A loose definition with only `Image.AssetEntryName` requires
bundle context: validate the packed bundle instead. An externally referenced
protected image bundle cannot currently receive a separate password. Failures
return 1 and produce actionable diagnostics; successful validation returns 0.

### Serve an existing published browser build

```bash
gondwana publish blazor
gondwana serve
gondwana serve --project ./src/MyGame --port 5001 --no-open
gondwana serve --configuration Release --framework net8.0-browser
gondwana serve --root ./artifacts/publish/wwwroot --no-open
```

`serve` locates `bin/Release/<framework>/publish/wwwroot` (including RID layouts)
and opens `http://localhost:5000` in the browser. Use `--port` to choose another
port, `--no-open` to suppress browser launch, `-c|--configuration` (default Release),
or `-f|--framework` for a multi-target project. `--root` accepts a custom published
wwwroot containing `index.html`. Discovery never selects an ordinary build's
wwwroot and **never publishes automatically**. Missing output suggests
`gondwana publish blazor`.

The built-in .NET HTTP server listens on localhost, sends `Cross-Origin-Opener-Policy:
same-origin` and `Cross-Origin-Embedder-Policy: require-corp`, serves WASM with the
correct MIME type, supports GET/HEAD and local absolute base-href subpaths, and
stops cleanly with Ctrl+C. It needs no Node or Python installation. It serves the
uncompressed published files; production compression negotiation, SPA fallback,
HTTPS, and remote hosting belong to a production server. Use `run blazor` for the
development server instead.
Files are indexed at startup; requests can only select an indexed published file.
Restart `serve` after publishing files with new names. Directory and file links
are excluded from the index, and link ancestry is checked again before serving.

---

### `gondwana doctor`

Validates your local Gondwana development environment.

```
Gondwana Doctor

Git                  OK  git version 2.47.0.windows.2
.NET SDK             OK  10.0.201
nbgv                 OK  3.9.50
Gondwana CLI         OK  1.2.0
Gondwana Templates   OK  Gondwana.Templates 1.2.0 (gondwana-winforms, gondwana-avalonia, gondwana-blazor)
wasm-tools           OK  10.0.300
git-cliff            OK  git-cliff 2.10.0
butler               OK  v17.0.0, built on ...
SkiaSharp            OK  3.119.2 (NuGet cache)
SDL2                 OK  1.0.82 (SDL2.dll)
LibVLC               Not checked
```

Checks performed:
- Git installed and version
- .NET SDK installed and version
- `nbgv` local tool restored
- Gondwana CLI global tool installed
- Gondwana templates (`gondwana-winforms`, `gondwana-avalonia`, `gondwana-blazor`) installed
- `wasm-tools` .NET workload installed when needed for Blazor/WASM work; if the current directory is not a Blazor project, a missing workload is reported only as an optional warning
- `git-cliff` installed
- `butler` installed (from `PATH` or the default user install directory used by `gondwana doctor --fix`)
- SkiaSharp native binaries
- SDL2 native binaries (for `Gondwana.Input.SDL2`; system-wide runtime available from [libsdl-org/SDL releases](https://github.com/libsdl-org/SDL/releases))
- LibVLC (for optional desktop `Gondwana.Video`). In a project directly referencing Video,
  doctor provides app-local deployment guidance; it cannot verify that project's published
  native output. Windows apps should add `VideoLAN.LibVLC.Windows`, macOS apps a compatible
  `VideoLAN.LibVLC.Mac`, and Linux deployments distribution LibVLC and codec plugins.
  See [Video deployment and smoke checks](../../Gondwana.Video/README.md).

Pass `--fix` to automatically resolve issues that have a known fix:

```bash
gondwana doctor --fix
```

Currently auto-fixable:
- **Gondwana CLI** not installed → runs `dotnet tool install -g Gondwana.Cli`
- **Gondwana Templates** missing → runs `dotnet new install Gondwana.Templates`; if already installed, `--fix` runs `dotnet new update` instead so newer local template packages are retained rather than downgraded
- **wasm-tools** missing while working in a Blazor/WASM project → runs `dotnet workload install wasm-tools`
- **git-cliff** on Windows → runs `winget install/upgrade --id orhun.git-cliff`
- **butler** not installed → downloads the latest binary from the [itch.io broth CDN](https://itch.io/docs/butler/installing.html), trying `broth.itch.ovh` first and `broth.itch.zone` as fallback, then installs it to `%LOCALAPPDATA%\itch\butler` (Windows) or `~/.itch/butler` (Linux/macOS). On Windows, `doctor --fix` also adds Butler's install directory to the persistent user `PATH`; open a new terminal before invoking `butler` directly. Run `butler login` after installation to authenticate with itch.io.

After applying fixes, the checks are re-run and the updated results are displayed.

---

### `gondwana new winforms <name>`

Scaffolds a new WinForms Gondwana project.

```bash
gondwana new winforms MyGame
```

Equivalent to `dotnet new gondwana-winforms -n MyGame` but with cleaner output.
If a `.sln` already exists in the output directory, adds the generated project to it; otherwise creates `MyGame.sln` and adds the project.

---

### `gondwana new avalonia <name>`

Scaffolds a new Avalonia Gondwana project (Windows, macOS, Linux).

```bash
gondwana new avalonia MyGame
```

Equivalent to `dotnet new gondwana-avalonia -n MyGame` but with cleaner output.
If a `.sln` already exists in the output directory, adds the generated project to it; otherwise creates `MyGame.sln` and adds the project.

An optional `--output` / `-o` flag can be used to specify the output directory:

```bash
gondwana new avalonia MyGame -o ./projects/MyGame
gondwana new winforms MyGame -o ./projects/MyGame
```

Both desktop commands accept an optional `--backbuffer` / `-b` flag to choose the rendering backbuffer:

| Value | Description |
|---|---|
| `bitmap` | CPU-based bitmap backbuffer using SkiaSharp (default). Available on the platforms supported by the selected template. |
| `gpu` | GPU-accelerated OpenGL backbuffer. Requires an OpenGL-capable desktop target. |

```bash
gondwana new winforms MyGame --backbuffer gpu
gondwana new avalonia MyGame -b gpu
```

---

### `gondwana new blazor <name>`

Scaffolds a new Gondwana project that targets **browser/WASM** using Blazor WebAssembly and Gondwana's GPU-backed WebGL renderer.

```bash
gondwana new blazor MyGame
```

Equivalent to `dotnet new gondwana-blazor -n MyGame`.
If a `.sln` already exists in the output directory, adds the generated project to it; otherwise creates `MyGame.sln` and adds the project.

An optional `--output` / `-o` flag can be used to specify the output directory:

```bash
gondwana new blazor MyGame -o ./projects/MyGame
```

The scaffolded project contains:

- `MyGame.csproj` — `Microsoft.NET.Sdk.BlazorWebAssembly` targeting `net8.0-browser`, with `Gondwana.Blazor`, `Gondwana.Blazor.Hosting`, `Gondwana.Audio.Browser`, and `Microsoft.AspNetCore.Components.WebAssembly` package references
- `Program.cs` — Blazor WebAssembly entry point that imports the Gondwana browser audio module (`/gondwana-audio.js`)
- `App.razor` — root Blazor app component
- `Pages/Index.razor` — the default page hosting the game canvas
- `GameRenderSurface.razor` — responsive GPU canvas with logical-resolution configuration and loading/error overlays
- `MyGameHost.cs` — `BlazorGpuGameHost` subclass with `// TODO` stubs for assets, scene setup, and input
- `wwwroot/index.html` — pre-WASM loading page styled by `wwwroot/css/startup.css`
- `assets/README.txt` — instructions for adding sprites and other assets

The generated Blazor project uses WebGL as the normal rendering path. Gondwana retains its CPU bitmap Blazor renderer as a compatibility/diagnostic path, but the CLI does not expose it as a normal project option.

After scaffolding, start the Blazor dev server:

```bash
cd MyGame
gondwana run blazor
```

Build and publish for deployment:

```bash
cd MyGame
gondwana publish blazor
# Output: bin/Release/net8.0-browser/publish/wwwroot/
```

The CLI checks whether `wasm-tools` is already installed and installs it only when needed. Use `--skip-workload` when the environment is already managed externally.

---

### `gondwana run`

Runs the desktop build of the project in the current directory (or `--project`).

```bash
gondwana run
gondwana run --project ./src/MyGame
gondwana run --configuration Release
gondwana run --framework net8.0
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Debug` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Target framework to run. Required for multi-target projects. |

Equivalent to `dotnet run --project <path> -c <configuration>`.

---

### `gondwana run blazor`

Ensures the `wasm-tools` workload is available (unless `--skip-workload`) and starts the Blazor WebAssembly dev server, opening the GPU-backed WebGL game in the default browser.

```bash
gondwana run blazor
gondwana run blazor --project ./src/MyGame
gondwana run blazor --framework net8.0-browser
gondwana run blazor --skip-workload
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Debug` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Browser target framework. Required only when auto-detection is ambiguous. |
| `--skip-workload` | | `false` | Skip checking/installing `wasm-tools`. |

Equivalent to `dotnet run --project <path> -c <configuration> -f <framework>` for the resolved Blazor WebAssembly target. The dev server starts automatically and opens the game in the browser at the address printed to the console.

---

### `gondwana publish blazor`

Builds and publishes the Blazor WebAssembly/WebGL project in the current directory (or `--project`).

```bash
gondwana publish blazor
gondwana publish blazor --project ./src/MyGame
gondwana publish blazor --framework net8.0-browser
gondwana publish blazor --base-href /games/mygame/
gondwana publish blazor --base-href ./
gondwana publish blazor --skip-workload
gondwana publish blazor --configuration Debug
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Browser target framework. Required only when auto-detection is ambiguous. |
| `--base-href <href>` | | *(unchanged)* | Rewrite the published `index.html` `<base href>` for subdirectory/static hosting. Values such as `/games/mygame/` and `./` are supported. |
| `--skip-workload` | | `false` | Skip checking/installing `wasm-tools`. |

The published output is placed at `bin/<Configuration>/<BrowserTargetFramework>/publish/wwwroot/`.
On success, the command prints the wwwroot path as a plain line (machine-friendly).
If publish succeeds but the wwwroot output cannot be located, a warning is printed.

For packaging or deployment, see also:
- `gondwana publish itch` — create an itch.io-ready zip from the publish output
- `gondwana deploy` / `gondwana deploy blazor` — copy/rsync the wwwroot to a static web host
- `gondwana deploy itch` — upload the publish output to itch.io via `butler`

---

### `gondwana publish`

Publishes the **desktop** build of the project in the current directory (or `--project`).

```bash
gondwana publish
gondwana publish --project ./src/MyGame
gondwana publish --runtime win-x64
gondwana publish --framework net8.0 --self-contained
gondwana publish --runtime win-x64 --self-contained --publish-single-file
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Desktop target framework to publish. Required only when multiple non-browser target frameworks exist. |
| `--runtime <rid>` | `-r` | *(none)* | Runtime identifier such as `win-x64`, `linux-x64`, or `osx-arm64`. |
| `--output <path>` | `-o` | *(dotnet default)* | Publish output directory. |
| `--self-contained` | | `false` | Publish as self-contained. |
| `--publish-single-file` | | `false` | Publish as a single-file executable. |

Equivalent to `dotnet publish <path> -c <configuration> -f <framework>` with optional `-r <rid>`, `-o <path>`, `--self-contained`, and `/p:PublishSingleFile=true`.

On success, the command prints the publish output directory as a plain line (machine-friendly) when it can be located.

---

### `gondwana publish itch`

Publishes the Blazor WebAssembly/WebGL project (unless `--skip-build`) and packages the published `wwwroot` contents as an itch.io-ready zip with `index.html` at the zip root.

```bash
gondwana publish itch
gondwana publish itch --project ./src/MyGame
gondwana publish itch --framework net8.0-browser
gondwana publish itch --skip-build
gondwana publish itch --base-href ./
gondwana publish itch --output ./artifacts/MyGame-itch.zip
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Browser target framework. |
| `--output <path>` | `-o` | beside the browser publish output | Output zip path. |
| `--base-href <href>` | | *(unchanged)* | Optional `<base href>` override applied before packaging. Gondwana does not assume an itch.io-specific value automatically. |
| `--skip-build` | | `false` | Skip the dotnet publish step and package an existing Blazor publish output. |
| `--skip-workload` | | `false` | Skip checking/installing `wasm-tools` during the publish step. |

On success, the command prints the zip path as a plain line (machine-friendly).

---

### `gondwana deploy`

Deploys the Blazor WebAssembly/WebGL project to a static web host. This is the default form of `gondwana deploy`.

```bash
gondwana deploy --web-root ./dist/MyGame
gondwana deploy --project ./src/MyGame --web-root ./dist/MyGame
gondwana deploy --remote-host deploy@example.com --remote-path /var/www/html/mygame --base-href /mygame/
gondwana deploy --skip-build --web-root ./dist/MyGame --base-href ./
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Browser target framework. |
| `--base-href <href>` | | *(unchanged)* | Rewrite the deployed `index.html` `<base href>` to match the public URL path. |
| `--web-root <path>` | | *(none)* | Local destination directory for the publish wwwroot contents. |
| `--remote-host <user@host>` | | *(none)* | SSH remote, used with `--remote-path`. |
| `--remote-path <path>` | | *(none)* | Remote destination path, used with `--remote-host`. |
| `--skip-build` | | `false` | Skip the dotnet publish step and deploy an existing Blazor publish output. |
| `--skip-workload` | | `false` | Skip checking/installing `wasm-tools` during the publish step. |
| `--no-mirror` | | `false` | Do not remove stale files from the destination (no mirroring). By default the destination is mirrored (stale files are deleted). |

Specify either `--web-root` or `--remote-host` + `--remote-path`, not both.

Remote deployment uses `rsync -avz --delete` (requires `rsync` on `PATH`). Pass `--no-mirror` to omit `--delete`.

If the game is hosted below the web-site root, use `--base-href` to match the public URL path. For example, a game served at `https://example.com/games/mygame/` should normally use `--base-href /games/mygame/`. A relative value such as `./` is also supported where appropriate.

Gondwana's default Blazor/WebGL path is single-threaded and does **not** require COOP/COEP headers. Those headers become relevant only if an application explicitly enables .NET WASM multithreading/`SharedArrayBuffer` support.

---

### `gondwana deploy blazor`

Alias of `gondwana deploy`; same behavior and options.

---

### `gondwana deploy itch`

Publishes the Blazor WebAssembly/WebGL project (unless `--skip-build`), packages the published `wwwroot`, and uploads it to itch.io using `butler`.

```bash
gondwana deploy itch --itch-game user/mygame
gondwana deploy itch --project ./src/MyGame --itch-game user/mygame
gondwana deploy itch --itch-game user/mygame --itch-channel html5-beta
gondwana deploy itch --skip-build --itch-game user/mygame
gondwana deploy itch --itch-game user/mygame --base-href ./
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--itch-game <user/game>` | | *(required)* | The itch.io game slug. |
| `--itch-channel <name>` | | `html5` | The itch.io release channel name. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Browser target framework. |
| `--base-href <href>` | | *(unchanged)* | Optional `<base href>` override. No itch.io-specific value is assumed automatically. |
| `--skip-build` | | `false` | Skip the dotnet publish step and deploy an existing Blazor publish output. |
| `--skip-workload` | | `false` | Skip checking/installing `wasm-tools` during the publish step. |

Prerequisites:
- `butler` installed (on `PATH` or in the standard itch.io installation location)
- an existing itch.io game matching `--itch-game`

Butler manages authentication; `gondwana deploy itch` does not run a separate
login step. For local use, run `butler login` once. Butler stores credentials
and also prompts for login if `butler push` requires it. Later deployments
normally need no additional login. For CI/CD, supply `BUTLER_API_KEY` as a
secret environment variable; never print or commit the key. See
[Butler's authentication guide](https://itch.io/docs/butler/login.html).

If an upload fails, check Butler's output for the cause. For authentication
failures, retry `butler login` locally or verify the CI secret; other
failures may have unrelated causes.

On success, the command prints the game URL as a plain line (machine-friendly).

---

### `gondwana templates`

Manage Gondwana project templates.

```bash
gondwana templates install   # Install Gondwana.Templates, or check for updates if already installed
gondwana templates update    # Check installed templates for updates without downgrading newer local versions
gondwana templates list      # List installed Gondwana templates
```

---

### `gondwana pack <source> <output>`

Shorthand for `gondwana assets pack`. Packs a directory of files into an asset bundle.

```bash
gondwana pack ./Assets ./game.assets
gondwana pack ./Assets ./game.assets --append
gondwana pack ./Assets ./game.assets --type-map my-types.json
gondwana pack ./Assets ./game.assets --password secret
gondwana pack ./Assets ./game.assets --password secret --encrypt
```

See [`gondwana assets pack`](#gondwana-assets) for the full list of options.

---

### `gondwana assets`

Pack, inspect, and extract Gondwana asset files (`.gaf`).

```bash
# Pack a directory of files into an asset bundle
gondwana assets pack ./Assets ./game.assets

# Pack using a custom type-map config
gondwana assets pack ./Assets ./game.assets --type-map my-types.json

# Pack with password protection
gondwana assets pack ./Assets ./game.assets --password secret

# Pack with AES-256 encryption (requires --password)
gondwana assets pack ./Assets ./game.assets --password secret --encrypt

# List all assets in a bundle
gondwana assets list ./game.assets

# List assets in a password-protected bundle
gondwana assets list ./game.assets --password secret

# Extract all assets from a bundle
gondwana assets extract ./game.assets ./Extracted

# Extract from a password-protected bundle
gondwana assets extract ./game.assets ./Extracted --password secret

# Generate a C# constants class for asset keys
gondwana assets generate-keys ./game.assets
gondwana assets generate-keys ./game.assets -o AssetKeys.cs -n MyGame.Assets

# Generate keys from a password-protected bundle
gondwana assets generate-keys ./game.assets --password secret

# Generate keys and include a Load() method for the bundle
gondwana assets generate-keys ./game.assets --include-loader -o AssetKeys.cs -n MyGame.Assets
```

The `generate-keys` command produces a file like:

```csharp
public static class AssetKeys
{
    public const string PlayerSprite = "sprites/player.png";
    public const string ThemeMusic = "audio/theme.ogg";
}
```

With `--include-loader`, a `Load()` factory method is also emitted:

```csharp
using Gondwana.Assets;

public static class AssetKeys
{
    public const string PlayerSprite = "sprites/player.png";
    public const string ThemeMusic = "audio/theme.ogg";

    /// <summary>Loads the <c>game.assets</c> asset bundle.
    /// The <paramref name="password"/> is only required for password-protected or encrypted bundles.</summary>
    public static AssetsFile Load(string? password = null)
        => AssetsFile.LoadOrCreate("game.assets", password);
}
```

This lets you load the bundle and retrieve assets entirely through the generated class:

```csharp
using var assets = AssetKeys.Load();
var sprite = assets[AssetTypes.Image, AssetKeys.PlayerSprite];

// Or, for a password-protected bundle:
using var assets = AssetKeys.Load("mypassword");
```


### Semantic packing

`gondwana assets pack` (also `gondwana pack`) automatically recognizes `.gts`, `.gani`, `.gscn`, `.gspr`, and `.gsnd` as their corresponding definition asset types. It reads every member through the Core serializers and writes one canonical document per input file. It does not split members or change their logical identities.

Before writing, packing resolves image/audio resources and GTS/GANI/GSCN dependencies across the complete inventory. Explicit authoring sources select a physical document and logical member. When source metadata is absent, a unique matching logical member in the final inventory supplies it; ambiguous matches fail. Physical references are transformed through `DefinitionReferenceRelocator`. `NextCycleKey` remains a logical reference; cycle relationships are allowed. URI audio resources remain unchanged.

By default dependencies must be in the planned package. Use `--allow-external` only when deliberately retaining existing loose files or external GAF dependencies. Explicit external targets must exist, and referenced typed entries and logical members are checked. This option does not search for unspecified external dependencies or recursively package external documents. External GAFs use the supplied password. External paths are rebased relative to the final output where possible. Binary media are not decoded during preflight.

`--append` analyzes the final inventory, including existing entries. An incoming file replaces its entire matching typed entry, never appends members to an existing document. Omitted loose dependencies under the source root can resolve to matching existing typed entries. Retained documents are checked against replacements, so removing a referenced member fails. Unaffected entry contents are preserved; existing AES encryption is retained.

Packing validates before writing, builds a temporary sibling archive, and replaces the destination only after successful writing and validation. Failure or cancellation leaves an existing output intact. The output itself is excluded when inside the source directory. Without `--append`, a nonempty invocation replaces the package completely; an empty input leaves it unchanged. Input files are not modified. `assets unpack` performs the corresponding semantic export described below.


### Semantic unpack and selective export

`gondwana assets unpack game.gaf Extracted` exports native definition documents with references transformed for their new locations. `assets extract` is an alias for the same operation. Ordinary image, audio, and other leaf entries retain their original bytes. Every member of a native document stays in the file named by its physical entry, in the original order; logical names and keys do not become filenames.

```sh
# Export everything, converting included dependencies to relative loose paths
gondwana assets unpack game.gaf Extracted
# Export tilesheets; images remain referenced in game.gaf
gondwana assets unpack game.gaf ExportedSheets --type TilesheetDefinition
# Export one exact typed document, including all its logical members
gondwana assets extract game.gaf ExportedScene --type SceneDefinition --entry Scenes/level.gscn
# Permit replacement of existing output files
gondwana assets unpack game.gaf Extracted --overwrite --password secret
```

`--entry` matches an exact GAF entry name, case-insensitively; it does not match basenames. If the name occurs in several types, supply `--type`. No match is an error. A type filter without matches exports nothing.

When both a definition and its dependency are selected, the dependency becomes a loose reference relative to the exported document. For example, `Tilesheets/terrain.gts` can reference `../Images/terrain.png`. Dependencies not selected remain packed in the original archive, with its path rebased relative to the new document. This includes GTS images whose source archive was previously implicit. References to external loose files or other GAFs retain their original targets and are rebased. URI audio is unchanged; audio format hints are retained, including for extensionless exported files.

The archive is validated and the selected documents' immediate dependency targets and logical members are checked before writing. External archives use the supplied password. Export does not download URIs or recursively export/audit external dependency graphs. Core applications must still load required archives and register dependent tilesheets, animation cycles, and scenes in their usual order; exporting does not change runtime initialization requirements.

All destinations are checked before staging. Unsafe paths, symlink/junction ancestry, case-insensitive destination collisions, file/directory conflicts, and attempts to overwrite the source GAF are rejected. Existing output requires `--overwrite`. Prepared files are staged, and replaced files are backed up during the multi-file commit. On an ordinary failure or cancellation, completed writes are rolled back and transaction files are cleaned up. If rollback fails, the error identifies retained recovery files.

Multi-file export is **not globally atomic**: a crash, power loss, disk failure, or concurrent filesystem modification can interrupt commit or recovery. Do not modify the output tree concurrently. Archive contents are buffered during preflight, and binary media are not decoded. A complete self-contained export works without the original GAF; a partial export must retain any archives/resources still referenced. Export is a copy operation and never rewrites the source GAF or project-wide inbound references.

#### Asset type mapping

`gondwana assets pack` maps file extensions to `AssetTypes` values. The `--type-map` flag is optional.
The tool resolves the config in this order, using built-in defaults if nothing is found:

1. The path given to `--type-map <file>` (if supplied)
2. `gondwana-asset-types.json` in the **current working directory**
3. `gondwana-asset-types.json` next to the `gondwana` executable (the shipped default)
4. **Built-in defaults** (always available — no config file required)

The selected configuration overlays built-in defaults: omitted extensions retain their mappings. Explicit non-native overrides are supported; `--type` applies only to unknown extensions. Native extensions are reserved for `TilesheetDefinition` (`gts`), `AnimationDefinition` (`gani`), `SceneDefinition` (`gscn`), `SpriteDefinition` (`gspr`), and `AudioDefinition` (`gsnd`). Unknown enum names, malformed values, duplicate extensions/type keys, and conflicting native mappings fail clearly.

The JSON format is an object whose keys are `AssetTypes` names and whose values are arrays of
file extensions (without a leading dot):

```json
{
  "Image":  ["png", "jpg", "jpeg", "bmp", "gif", "webp", "tiff", "ico"],
  "Audio":  ["wav", "mp3", "ogg", "flac", "aac", "wma", "mid", "midi"],
  "Video":  ["mp4", "avi", "mkv", "mov", "wmv", "webm", "m4v"],
  "Cursor": ["cur", "ani"],
  "Font":   ["ttf", "otf", "woff", "woff2"],
  "Svg":    ["svg"]
}
```

Copy `gondwana-asset-types.json` from the tool installation directory into your project to
customize extension mappings without affecting other projects.

---

### `gondwana info`

Displays information about the Gondwana project in the current directory.

```
Project: MyGame
Framework: net8.0
Host: WinForms
Gondwana: 2.2.0
Adapters:
  - Gondwana.WinForms
  - Gondwana.Audio.Midi
Assets:
  - Assets/game.assets
```

---

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/api/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Cli/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Audio.NAudio` --- Windows audio backend for the common audio API
-   `Gondwana.Audio.Browser` --- Browser-based audio playback support
-   `Gondwana.Audio.Midi` --- Windows MIDI playback through the NAudio backend
-   `Gondwana.Avalonia` --- Avalonia rendering and input adapters
-   `Gondwana.Avalonia.Hosting` --- Avalonia-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Blazor` --- Blazor WebAssembly rendering and input adapters, including the GPU-backed WebGL path and bitmap compatibility renderer
-   `Gondwana.Blazor.Hosting` --- Blazor-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Hosting` --- Standard platform-agnostic scaffolding for initializing and running Gondwana games
-   `Gondwana.Input.SDL2` --- SDL2-based input handling
-   `Gondwana.Video` --- Video playback support
-   `Gondwana.Video.Widgets` --- Optional interactive video Widgets with opt-in dragging
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays
-   `Gondwana.WinForms` --- WinForms rendering and input adapters
-   `Gondwana.WinForms.Hosting` --- WinForms-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Templates` --- Project templates for quickly starting new Gondwana games

## License

MIT

## Reference-aware moves

`gondwana assets move <source> <destination> --scope <directory-or-gaf>` previews a true move. Add `--commit` to execute. Use `--source-entry`, `--destination-entry`, and `--type` for packed endpoints; use `--archive` for a whole-GAF move. Each archive may use its own password from `--credentials <json-file>` (absolute archive path to password). Missing/ambiguous dependencies, incomplete scans, collisions, and stale plans fail without retiring originals. Save and close affected editors before committing.

See [Assets Files — reference-aware moves](https://github.com/Isthimius/Gondwana/wiki/Assets-Files#reference-aware-rename-and-move) for all storage transitions, scope boundaries, encryption, rollback and interrupted-commit recovery. Outside-scope references and arbitrary C# are not rewritten. Existing pack/unpack semantics are unchanged.

## Reference-aware deletion

`gondwana assets delete <source> --scope <directory-or-archive>` previews deletion. Add `--commit` to execute; add `--force` only to authorize the listed surviving references becoming invalid. Force alone never writes. Use `--source-entry` with `--type` for packed entries, or `--archive` for whole GAF/ZIP deletion. Per-archive credentials use the same `--credentials` JSON format as moves. Exit codes: 0 success, 2 blocked by surviving references, 1 invalid input/incomplete discovery or execution failure. No dependent resources are automatically deleted.

See [Assets Files — reference-aware deletion](https://github.com/Isthimius/Gondwana/wiki/Assets-Files#reference-aware-deletion) for scope, credentials, batching, force semantics, and recovery guarantees.

## Asset command safety options

These options authorize different operations; none is a universal validation bypass:

| Option | Commands | Meaning |
| --- | --- | --- |
| `--commit` | move, delete | Execute a plan; without it these commands only preview |
| `--force` | delete | Permit the listed surviving references to become dangling; still requires `--commit` to write |
| `--overwrite` | unpack/extract | Replace existing loose output files with rollback protection |
| `--append` | pack | Retain existing entries, replacing entire incoming typed entries after dependency validation |
| `--allow-external` | pack | Permit validated external dependencies; does not permit missing/ambiguous targets |
| `--archive` | move, delete | Select a whole GAF/ZIP rather than a typed entry |

Pack and unpack write on invocation; they do not use move/delete's preview/commit switches. Move rejects existing destinations rather than accepting an overwrite switch. Packed entry identity includes its type and full entry name; `.gaf` and `.zip` packages follow the same archive rules. Logical member names never substitute for physical entry selectors.

Move/delete use per-archive `--credentials` maps. Pack/unpack use `--password`; it is also used for external archives they inspect. Move returns 0 on success and 1 on failure. Delete additionally returns 2 when surviving references block a safe preview/commit. Force does not bypass incomplete discovery, invalid paths, missing credentials, or stale plans.

See [Reference Relocation](https://github.com/Isthimius/Gondwana/wiki/Reference-Relocation#shared-tooling-and-safety) for scope, manual crash recovery, open-document restrictions, and deliberate limitations.
