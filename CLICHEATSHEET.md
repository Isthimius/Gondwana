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
| `gondwana doctor` | Validate your local Gondwana development environment. |
| `gondwana info` | Show information about the Gondwana project in the current directory. |
| `gondwana new <subcommand>` | Scaffold a new Gondwana project. |
| `gondwana templates <subcommand>` | Manage Gondwana `dotnet new` templates. |
| `gondwana assets <subcommand>` | Pack, inspect, and extract Gondwana asset files. |

---

## `gondwana doctor`

Checks the local environment for all Gondwana prerequisites (.NET SDK, native libraries, templates).

*No arguments or options.*

---

## `gondwana info`

Reads the `.csproj` in the current directory and prints project metadata (name, target framework, Gondwana version, adapters, and discovered asset bundles). When multiple `.csproj` files are present, the first one alphabetically is used.

*No arguments or options.*

---

## `gondwana new`

| Subcommand | Description |
|---|---|
| `winforms` | Create a new WinForms Gondwana project. |

### `gondwana new winforms <name>`

| Argument / Option | Short | Description |
|---|---|---|
| `<name>` | | **Required.** Name of the new project. |
| `--output <dir>` | `-o` | Directory to place the generated output in. Defaults to a new folder named `<name>` in the current directory. |

**Example**
```
gondwana new winforms MyGame
gondwana new winforms MyGame -o ./projects/MyGame
```

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
| `--type-map <file>` | `-m` | *(auto)* | Path to a JSON file that maps asset types to file extensions. Resolution order: this flag → `gondwana-asset-types.json` in CWD → `gondwana-asset-types.json` next to the executable. |

**Examples**
```
gondwana assets pack ./Assets ./game.assets
gondwana assets pack ./Assets ./game.assets --append
gondwana assets pack ./Assets ./game.assets -m ./my-types.json
```

---

### `gondwana assets list <file>`

| Argument / Option | Short | Description |
|---|---|---|
| `<file>` | | **Required.** Path to the asset bundle to inspect. |
| `--type <name>` | `-t` | Filter output to assets of the specified type (e.g. `Image`, `Audio`, `Video`, `Font`, `Cursor`, `Misc`). |

**Example**
```
gondwana assets list ./game.assets
gondwana assets list ./game.assets -t Image
```

---

### `gondwana assets extract <file> <output>`

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<file>` | | | **Required.** Path to the asset bundle to extract. |
| `<output>` | | | **Required.** Directory to extract assets into. Created automatically if it does not exist. |
| `--type <name>` | `-t` | *(all)* | Extract only assets of the specified type (e.g. `Image`, `Audio`). |
| `--overwrite` | | `false` | Overwrite existing files in the output directory. |

**Example**
```
gondwana assets extract ./game.assets ./Extracted
gondwana assets extract ./game.assets ./Extracted --overwrite -t Audio
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

**Examples**
```
gondwana assets generate-keys ./game.assets
gondwana assets generate-keys ./game.assets -o ./Generated/AssetKeys.cs -n MyGame -c AssetKeys
```

---

## Asset type-map JSON format

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
