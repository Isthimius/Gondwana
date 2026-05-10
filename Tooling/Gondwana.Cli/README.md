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
Gondwana Templates   OK  gondwana-winforms, gondwana-avalonia, gondwana-wasm found
wasm-tools           OK  wasm-tools installed
SkiaSharp            OK  found in NuGet global cache
SDL2                 Missing native library
LibVLC               Not checked

1 issue found.
```

Checks performed:
- Git installed and version
- .NET SDK installed and version
- `nbgv` local tool restored
- Gondwana CLI global tool installed
- Gondwana templates (`gondwana-winforms`, `gondwana-avalonia`, `gondwana-wasm`) installed
- `wasm-tools` .NET workload installed
- SkiaSharp native binaries
- SDL2 native binaries (for `Gondwana.Input.SDL2`)
- LibVLC (for `Gondwana.Video`)

Pass `--fix` to automatically resolve issues that have a known fix:

```bash
gondwana doctor --fix
```

Currently auto-fixable:
- **Gondwana CLI** not installed → runs `dotnet tool install -g Gondwana.Cli`
- **Gondwana Templates** not installed → runs `dotnet new install Gondwana.Templates`
- **wasm-tools** not installed → runs `dotnet workload install wasm-tools`

After applying fixes, the checks are re-run and the updated results are displayed.

---

### `gondwana new winforms <name>`

Scaffolds a new WinForms Gondwana project.

```bash
gondwana new winforms MyGame
```

Equivalent to `dotnet new gondwana-winforms -n MyGame` but with cleaner output.

---

### `gondwana new avalonia <name>`

Scaffolds a new Avalonia Gondwana project (Windows, macOS, Linux).

```bash
gondwana new avalonia MyGame
```

Equivalent to `dotnet new gondwana-avalonia -n MyGame` but with cleaner output.

An optional `--output` / `-o` flag can be used to specify the output directory for either `new` command:

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

### `gondwana new wasm <name>`

Scaffolds a new Gondwana project that targets **both desktop and browser/WASM** using Avalonia.

```bash
gondwana new wasm MyGame
```

Equivalent to `dotnet new gondwana-wasm -n MyGame`.

An optional `--output` / `-o` flag can be used to specify the output directory:

```bash
gondwana new wasm MyGame -o ./projects/MyGame
```

The scaffolded project contains:

- `MyGame.csproj` — multi-targets `net8.0` (desktop) and `net8.0-browser` (WASM), with `Avalonia.Desktop` / `Avalonia.Browser` and `Gondwana.Audio.Browser` applied conditionally
- `Program.cs` / `Program.Browser.cs` — split entry points; the browser version imports the audio JS module and starts Avalonia in single-view mode
- `App.cs` — handles both `IClassicDesktopStyleApplicationLifetime` and `ISingleViewApplicationLifetime`
- `GameWindow.cs` — desktop `Window` (compiled only for `net8.0`)
- `GameView.cs` — browser `UserControl` (compiled for both targets, used only in WASM)
- `GameRenderSurface.cs` — thin subclass of `AvaloniaBitmapRenderSurfaceControl`
- `GameHost.cs` — `AvaloniaGameHost` subclass with `// TODO` stubs that show both desktop and browser audio patterns
- `wwwroot/gondwana-audio.js` — the Gondwana browser audio module
- `assets/README.txt` — instructions for adding sprites and other assets

After scaffolding, run on desktop:

```bash
cd MyGame
dotnet run
```

Build and publish for WASM:

```bash
cd MyGame
dotnet workload install wasm-tools   # one-time per machine
dotnet publish -f net8.0-browser -c Release
# Output: bin/Release/net8.0-browser/browser-wasm/AppBundle/
```

Or use the CLI shorthand:

```bash
cd MyGame
gondwana publish wasm
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

### `gondwana run wasm`

Publishes the project for WASM and serves it in the browser using `dotnet-serve`.

```bash
gondwana run wasm
gondwana run wasm --project ./src/MyGame
gondwana run wasm --skip-workload
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Debug` | Build configuration. |
| `--skip-workload` | | `false` | Skip `dotnet workload install wasm-tools`. |

Publishes the project for `net8.0-browser` and serves the resulting `AppBundle` via `dotnet-serve`, opening the game in the default browser. `dotnet-serve` is installed automatically as a global tool if it is not already present.

---

### `gondwana publish wasm`

Builds and publishes the project in the current directory (or `--project`) for `net8.0-browser`.

```bash
gondwana publish wasm
gondwana publish wasm --project ./src/MyGame
gondwana publish wasm --skip-workload
gondwana publish wasm --configuration Debug
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(cwd)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--skip-workload` | | `false` | Skip `dotnet workload install wasm-tools`. |

The output AppBundle is placed at `bin/<Configuration>/net8.0-browser/browser-wasm/AppBundle/`.
On success, the command also prints the AppBundle path as a plain line (machine-friendly).
If publish succeeds but AppBundle cannot be located, a warning is printed.

For further deployment see:
- `scripts/Deploy-Gondwana-Itch.ps1` — upload to itch.io via `butler`
- `scripts/Deploy-Gondwana-Website.ps1` — copy/rsync to a static web host

---

### `gondwana templates`

Manage Gondwana project templates.

```bash
gondwana templates install   # Install Gondwana.Templates from NuGet
gondwana templates update    # Update installed templates
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
