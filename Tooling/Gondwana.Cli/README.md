# Gondwana CLI

Developer CLI for the [Gondwana Game Engine](https://github.com/Isthimius/Gondwana).

## Installation

```bash
dotnet tool install --global Gondwana.Cli
```

## Commands

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

1 issue found.
```

Checks performed:
- Git installed and version
- .NET SDK installed and version
- `nbgv` local tool restored
- Gondwana CLI global tool installed
- Gondwana templates (`gondwana-winforms`, `gondwana-avalonia`, `gondwana-blazor`) installed
- `wasm-tools` .NET workload installed
- `git-cliff` installed
- `butler` installed (from `PATH` or the default user install directory used by `gondwana doctor --fix`)
- SkiaSharp native binaries
- SDL2 native binaries (for `Gondwana.Input.SDL2`; system-wide runtime available from [libsdl-org/SDL releases](https://github.com/libsdl-org/SDL/releases))
- LibVLC (for `Gondwana.Video`)

Pass `--fix` to automatically resolve issues that have a known fix:

```bash
gondwana doctor --fix
```

Currently auto-fixable:
- **Gondwana CLI** not installed → runs `dotnet tool install -g Gondwana.Cli`
- **Gondwana Templates** missing → runs `dotnet new install Gondwana.Templates`; if already installed, `--fix` runs `dotnet new update` instead so newer local template packages are retained rather than downgraded
- **wasm-tools** not installed → runs `dotnet workload install wasm-tools`
- **git-cliff** on Windows → runs `winget install/upgrade --id orhun.git-cliff`
- **butler** not installed → downloads the latest binary from the [itch.io broth CDN](https://itch.io/docs/butler/installing.html), trying `broth.itch.ovh` first and `broth.itch.zone` as fallback, then installs it to `%LOCALAPPDATA%\itch\butler` (Windows) or `~/.itch/butler` (Linux/macOS). Run `butler login` after installation to authenticate with itch.io.

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

Both commands accept an optional `--backbuffer` / `-b` flag to choose the rendering backbuffer:

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

Scaffolds a new Gondwana project that targets **browser/WASM** using Blazor WebAssembly.

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
- `GameRenderSurface.razor` — thin Blazor component wrapping `BlazorBitmapRenderSurfaceComponent`
- `MyGameHost.cs` — `BlazorGameHost` subclass with `// TODO` stubs for assets, scene setup, and input
- `wwwroot/index.html` — the Blazor host page
- `assets/README.txt` — instructions for adding sprites and other assets

After scaffolding, start the Blazor dev server:

```bash
cd MyGame
dotnet run
```

Build and publish for deployment:

```bash
cd MyGame
dotnet workload install wasm-tools   # one-time per machine
dotnet publish -c Release
# Output: bin/Release/net8.0/publish/wwwroot/
```

Or use the CLI shorthand:

```bash
cd MyGame
gondwana publish blazor
```

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

Installs the `wasm-tools` workload (unless `--skip-workload`) then starts the Blazor WebAssembly dev server, opening the game in the default browser.

```bash
gondwana run blazor
gondwana run blazor --project ./src/MyGame
gondwana run blazor --skip-workload
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Debug` | Build configuration. |
| `--skip-workload` | | `false` | Skip `dotnet workload install wasm-tools`. |

Equivalent to `dotnet run --project <path> -c <configuration>` for a `Microsoft.NET.Sdk.BlazorWebAssembly` project. The Blazor dev server starts automatically and opens the game in the browser at the address printed to the console.

---

### `gondwana publish blazor`

Builds and publishes the Blazor WebAssembly project in the current directory (or `--project`).

```bash
gondwana publish blazor
gondwana publish blazor --project ./src/MyGame
gondwana publish blazor --skip-workload
gondwana publish blazor --configuration Debug
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--skip-workload` | | `false` | Skip `dotnet workload install wasm-tools`. |

The published output is placed at `bin/<Configuration>/net8.0/publish/wwwroot/`.
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

Publishes the project for `net8.0-browser` (unless `--skip-build`) and packages the AppBundle contents as an itch.io-ready zip with `index.html` at the zip root.

```bash
gondwana publish itch
gondwana publish itch --project ./src/MyGame
gondwana publish itch --skip-build
gondwana publish itch --output ./artifacts/MyGame-itch.zip
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--output <path>` | `-o` | `bin/<Configuration>/net8.0-browser/browser-wasm/<ProjectName>-itch.zip` | Output zip path. |
| `--skip-build` | | `false` | Skip the dotnet publish step and package an existing AppBundle. |
| `--skip-workload` | | `false` | Skip `dotnet workload install wasm-tools` during the publish step. |

On success, the command prints the zip path as a plain line (machine-friendly).

---

### `gondwana deploy`

Deploys the project for browser/WASM to a static web host. This is the default form of `gondwana deploy`.

```bash
gondwana deploy --web-root ./dist/MyGame
gondwana deploy --project ./src/MyGame --web-root ./dist/MyGame
gondwana deploy --remote-host deploy@example.com --remote-path /var/www/html/mygame
gondwana deploy --skip-build --web-root ./dist/MyGame
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--web-root <path>` | | *(none)* | Local destination directory for the publish wwwroot contents. |
| `--remote-host <user@host>` | | *(none)* | SSH remote, used with `--remote-path`. |
| `--remote-path <path>` | | *(none)* | Remote destination path, used with `--remote-host`. |
| `--skip-build` | | `false` | Skip the dotnet publish step and deploy an existing publish output. |
| `--skip-workload` | | `false` | Skip `dotnet workload install wasm-tools` during the publish step. |
| `--no-mirror` | | `false` | Do not remove stale files from the destination (no mirroring). By default the destination is mirrored (stale files are deleted). |
Specify either `--web-root` or `--remote-host` + `--remote-path`, not both.

Remote deployment uses `rsync -avz --delete` (requires `rsync` on `PATH`). Pass `--no-mirror` to omit `--delete`.

After a successful deployment, the command reminds you of the HTTP headers your server must send on every request for .NET WASM threading (`SharedArrayBuffer`) to work:

```text
Cross-Origin-Opener-Policy:   same-origin
Cross-Origin-Embedder-Policy: require-corp
```

The site must also be served over HTTPS.

On success, the command prints the deploy destination as a plain line (machine-friendly): the absolute local path when using `--web-root`, or `user@host:/remote/path/` when using `--remote-host`/`--remote-path`.

---

### `gondwana deploy blazor`

Alias of `gondwana deploy`; same behavior and options.

---

### `gondwana deploy itch`

Publishes the project for `net8.0-browser` (unless `--skip-build`), packages the AppBundle, and uploads it to itch.io using `butler`.

```bash
gondwana deploy itch --itch-game user/mygame
gondwana deploy itch --project ./src/MyGame --itch-game user/mygame
gondwana deploy itch --itch-game user/mygame --itch-channel html5-beta
gondwana deploy itch --skip-build --itch-game user/mygame
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--itch-game <user/game>` | | *(required)* | The itch.io game slug. |
| `--itch-channel <name>` | | `html5` | The itch.io release channel name. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--skip-build` | | `false` | Skip the dotnet publish step and deploy an existing AppBundle. |
| `--skip-workload` | | `false` | Skip `dotnet workload install wasm-tools` during the publish step. |

Prerequisites:
- `butler` on `PATH`
- `butler login` already completed
- the itch.io game already exists

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

#### Asset type mapping

`gondwana assets pack` maps file extensions to `AssetTypes` values. The `--type-map` flag is optional.
The tool resolves the config in this order, using built-in defaults if nothing is found:

1. The path given to `--type-map <file>` (if supplied)
2. `gondwana-asset-types.json` in the **current working directory**
3. `gondwana-asset-types.json` next to the `gondwana` executable (the shipped default)
4. **Built-in defaults** (always available — no config file required)

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

## License

MIT — see [LICENSE](https://github.com/Isthimius/Gondwana/blob/master/LICENSE)
