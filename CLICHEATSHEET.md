# Gondwana CLI Cheatsheet

Quick reference for all `gondwana` commands and their options.

---

## Installation

Install the CLI as a .NET global tool from NuGet:

```sh
dotnet tool install --global Gondwana.Cli
```

Update to the latest version:

```sh
dotnet tool update --global Gondwana.Cli
```

Uninstall:

```sh
dotnet tool uninstall --global Gondwana.Cli
```

After installation the `gondwana` command is available in any terminal.

---

## Top-level commands

| Command | Description |
|---|---|
| `gondwana help` | Show a summary of all available commands. |
| `gondwana doctor` | Validate your local Gondwana development environment. |
| `gondwana info` | Show information about the Gondwana project in the current directory. |
| `gondwana pack <source> <output>` | Pack a directory of files into an asset bundle (shorthand for `gondwana assets pack`). |
| `gondwana new <subcommand>` | Scaffold a new Gondwana project. |
| `gondwana templates <subcommand>` | Manage Gondwana `dotnet new` templates. |
| `gondwana publish <subcommand>` | Publish a Gondwana project for distribution. |
| `gondwana assets <subcommand>` | Pack, inspect, and extract Gondwana asset files. |

---

## `gondwana help`

Prints a formatted table of all available commands with short descriptions, then reminds you to run `gondwana <command> --help` for detailed usage.

*No arguments or options.*

---

## `gondwana doctor`

Checks the local environment for all Gondwana prerequisites (.NET SDK, native libraries, templates).

*No arguments or options.*

---

## `gondwana info`

Reads the `.csproj` in the current directory and prints project metadata (name, target framework, Gondwana version, adapters, and discovered asset bundles). When multiple `.csproj` files are present, the first one alphabetically is used.

*No arguments or options.*

---

## `gondwana pack`

Top-level shorthand for [`gondwana assets pack`](#gondwana-assets-pack-source-output). Accepts exactly the same arguments and options.

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<source>` | | | **Required.** Source directory containing files to pack. |
| `<output>` | | | **Required.** Output bundle file path (e.g. `game.assets` or `game.gaf`). |
| `--type <name>` | `-t` | `Misc` | Default asset type for files whose type cannot be inferred from the extension. |
| `--recurse` | `-r` | `true` | Recurse into subdirectories. |
| `--append` | `-a` | `false` | Append to an existing bundle instead of overwriting it. |
| `--type-map <file>` | `-m` | *(built-in defaults)* | Path to a JSON file that maps asset types to file extensions. Optional — uses built-in defaults when omitted and no `gondwana-asset-types.json` is found. |
| `--password <pass>` | `-p` | *(none)* | Password-protect the bundle. Required when `--encrypt` is used. |
| `--encrypt` | `-e` | `false` | Encrypt the bundle using AES-256. Requires `--password`. |

**Examples**
```
gondwana pack ./Assets ./game.assets
gondwana pack ./Assets ./game.assets --append
gondwana pack ./Assets ./game.assets -m ./my-types.json
gondwana pack ./Assets ./game.assets --password secret
gondwana pack ./Assets ./game.assets --password secret --encrypt
```

---

## `gondwana new`

| Subcommand | Description |
|---|---|
| `winforms` | Create a new WinForms Gondwana project. |
| `avalonia` | Create a new Avalonia Gondwana project (Windows, macOS, Linux). |
| `wasm` | Create a new Avalonia Gondwana project targeting desktop and browser/WASM. |

### `gondwana new winforms <name>`

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<name>` | | | **Required.** Name of the new project. |
| `--output <dir>` | `-o` | | Directory to place the generated output in. Defaults to a new folder named `<name>` in the current directory. |
| `--backbuffer <type>` | `-b` | `bitmap` | Backbuffer type: `bitmap` (CPU-based, default) or `gpu` (OpenGL-accelerated). |

**Example**
```
gondwana new winforms MyGame
gondwana new winforms MyGame -o ./projects/MyGame
gondwana new winforms MyGame --backbuffer gpu
```

---

### `gondwana new avalonia <name>`

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<name>` | | | **Required.** Name of the new project. |
| `--output <dir>` | `-o` | | Directory to place the generated output in. Defaults to a new folder named `<name>` in the current directory. |
| `--backbuffer <type>` | `-b` | `bitmap` | Backbuffer type: `bitmap` (CPU-based, default) or `gpu` (OpenGL-accelerated). |

**Example**
```
gondwana new avalonia MyGame
gondwana new avalonia MyGame -o ./projects/MyGame
gondwana new avalonia MyGame --backbuffer gpu
```

---

### `gondwana new wasm <name>`

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<name>` | | | **Required.** Name of the new project. |
| `--output <dir>` | `-o` | | Directory to place the generated output in. Defaults to a new folder named `<name>` in the current directory. |

**Example**
```
gondwana new wasm MyGame
gondwana new wasm MyGame -o ./projects/MyGame
```

Scaffolds an Avalonia project that compiles for both `net8.0` (desktop) and `net8.0-browser` (WASM).
Includes `Program.Browser.cs`, `GameView.cs`, `wwwroot/gondwana-audio.js`, and the `Gondwana.Audio.Browser` package reference.

After scaffolding, publish for WASM with:
```
dotnet workload install wasm-tools    # one-time per machine
dotnet publish -f net8.0-browser -c Release
```

Output is placed in `bin/Release/net8.0-browser/browser-wasm/AppBundle/`.

---

## `gondwana publish`

| Subcommand | Description |
|---|---|
| `wasm` | Build and publish the current project for browser/WASM. |

### `gondwana publish wasm`

Installs the `wasm-tools` .NET workload (unless `--skip-workload`) then runs
`dotnet publish -f net8.0-browser -c Release` and reports the AppBundle path.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Release` | Build configuration (`Release`, `Debug`). |
| `--skip-workload` | | `false` | Skip `dotnet workload install wasm-tools`. |

**Examples**
```
gondwana publish wasm
gondwana publish wasm -p ./src/MyGame
gondwana publish wasm --skip-workload -c Debug
```

For itch.io and website deployment, see `scripts/Deploy-Gondwana-Itch.ps1` and
`scripts/Deploy-Gondwana-Website.ps1`.

---

## `gondwana templates`

| Subcommand | Description |
|---|---|
| `install` | Install `Gondwana.Templates` from NuGet. |
| `update` | Update installed Gondwana templates. |
| `list` | List installed Gondwana templates. |

### `gondwana templates install`

Runs `dotnet new install Gondwana.Templates`. *No arguments or options.*

### `gondwana templates update`

Runs `dotnet new update Gondwana.Templates`. *No arguments or options.*

### `gondwana templates list`

Runs `dotnet new list gondwana` and prints matching templates. *No arguments or options.*

---

## `gondwana assets`

| Subcommand | Description |
|---|---|
| `pack` | Pack a directory of files into an asset bundle. |
| `list` | List all assets in a bundle. |
| `extract` | Extract all assets from a bundle to a directory. |
| `generate-keys` | Generate a C# constants class for all asset keys in a bundle. |

---

### `gondwana assets pack <source> <output>`

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<source>` | | | **Required.** Source directory containing files to pack. |
| `<output>` | | | **Required.** Output bundle file path (e.g. `game.assets` or `game.gaf`). |
| `--type <name>` | `-t` | `Misc` | Default asset type for files whose type cannot be inferred from the extension. |
| `--recurse` | `-r` | `true` | Recurse into subdirectories. |
| `--append` | `-a` | `false` | Append to an existing bundle instead of overwriting it. By default the output file is deleted first so no stale entries survive a re-run. |
| `--type-map <file>` | `-m` | *(built-in defaults)* | Path to a JSON file that maps asset types to file extensions. Optional. Resolution order: this flag → `gondwana-asset-types.json` in CWD → `gondwana-asset-types.json` next to the executable → built-in defaults. |
| `--password <pass>` | `-p` | *(none)* | Password-protect the bundle. Required when `--encrypt` is used. |
| `--encrypt` | `-e` | `false` | Encrypt the bundle using AES-256. Requires `--password`. |

**Examples**
```
gondwana assets pack ./Assets ./game.assets
gondwana assets pack ./Assets ./game.assets --append
gondwana assets pack ./Assets ./game.assets -m ./my-types.json
gondwana assets pack ./Assets ./game.assets --password secret
gondwana assets pack ./Assets ./game.assets --password secret --encrypt
```

---

### `gondwana assets list <file>`

| Argument / Option | Short | Description |
|---|---|---|
| `<file>` | | **Required.** Path to the asset bundle to inspect. |
| `--type <name>` | `-t` | Filter output to assets of the specified type (e.g. `Image`, `Audio`, `Video`, `Font`, `Cursor`, `Misc`). |
| `--password <pass>` | `-p` | Password required to open a password-protected or encrypted bundle. |

**Example**
```
gondwana assets list ./game.assets
gondwana assets list ./game.assets -t Image
gondwana assets list ./game.assets --password secret
```

---

### `gondwana assets extract <file> <output>`

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<file>` | | | **Required.** Path to the asset bundle to extract. |
| `<output>` | | | **Required.** Directory to extract assets into. Created automatically if it does not exist. |
| `--type <name>` | `-t` | *(all)* | Extract only assets of the specified type (e.g. `Image`, `Audio`). |
| `--overwrite` | | `false` | Overwrite existing files in the output directory. |
| `--password <pass>` | `-p` | *(none)* | Password required to open a password-protected or encrypted bundle. |

**Example**
```
gondwana assets extract ./game.assets ./Extracted
gondwana assets extract ./game.assets ./Extracted --overwrite -t Audio
gondwana assets extract ./game.assets ./Extracted --password secret
```

---

### `gondwana assets generate-keys <file>`

Generates a C# `public static class` containing one `public const string` per asset key, suitable for use at compile time.

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<file>` | | | **Required.** Path to the asset bundle to read keys from. |
| `--output <file>` | `-o` | *(stdout)* | Output `.cs` file path. Prints to stdout if omitted. The destination directory is created automatically if it does not exist. |
| `--namespace <ns>` | `-n` | *(none)* | C# namespace for the generated class. |
| `--class <name>` | `-c` | `AssetKeys` | C# class name. |
| `--password <pass>` | `-p` | *(none)* | Password required to open a password-protected or encrypted bundle. |
| `--include-loader` | `-l` | `false` | Also emit a `Load(string? password = null)` static method that instantiates `AssetsFile` for the bundle. |

**Examples**
```
gondwana assets generate-keys ./game.assets
gondwana assets generate-keys ./game.assets -o ./Generated/AssetKeys.cs -n MyGame -c AssetKeys
gondwana assets generate-keys ./game.assets --password secret
gondwana assets generate-keys ./game.assets --include-loader -o ./Generated/AssetKeys.cs -n MyGame
```

---

## Asset type-map JSON format

The `--type-map` flag is **optional**. When omitted, `assets pack` (and `pack`) resolve the type config in this order, falling back to built-in defaults if nothing is found:

1. The path given to `--type-map <file>`
2. `gondwana-asset-types.json` in the current working directory
3. `gondwana-asset-types.json` next to the `gondwana` executable
4. **Built-in defaults** — no config file required

Drop a `gondwana-asset-types.json` in the project directory (or pass `--type-map`) to customise extension → type mappings for `assets pack`.

```json
{
  "Image":  ["png", "jpg", "jpeg", "bmp", "gif", "webp", "tiff", "ico"],
  "Audio":  ["wav", "mp3", "ogg", "flac", "aac", "wma", "mid", "midi"],
  "Video":  ["mp4", "avi", "mkv", "mov", "wmv", "webm", "m4v"],
  "Cursor": ["cur", "ani"],
  "Font":   ["ttf", "otf", "woff", "woff2"]
}
```

Valid type names match the `AssetTypes` enum: `Image`, `Audio`, `Video`, `Font`, `Cursor`, `Misc`.
