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
| [`gondwana help`](#gondwana-help) | Show a summary of all available commands. |
| [`gondwana doctor`](#gondwana-doctor) | Validate your local Gondwana development environment. Pass `--fix` to auto-fix issues. |
| [`gondwana info`](#gondwana-info) | Show information about the Gondwana project in the current directory. |
| [`gondwana assets <subcommand>`](#gondwana-assets) | Pack, inspect, and extract Gondwana asset files. |
| [`gondwana pack <source> <output>`](#gondwana-pack) | Pack a directory of files into an asset bundle (shorthand for `gondwana assets pack`). |
| [`gondwana new <subcommand>`](#gondwana-new) | Scaffold a new Gondwana project. |
| [`gondwana templates <subcommand>`](#gondwana-templates) | Manage Gondwana `dotnet new` templates. |
| [`gondwana run`](#gondwana-run) | Run the desktop build of the project in the current directory. |
| [`gondwana run wasm`](#gondwana-run-wasm) | Build and run the project in the browser (net8.0-browser dev server). |
| [`gondwana publish <subcommand>`](#gondwana-publish) | Publish a Gondwana project for distribution. |
| [`gondwana deploy <subcommand>`](#gondwana-deploy) | Deploy a Gondwana project to a distribution target. |

---

## `gondwana help`

Prints a formatted table of all available commands with short descriptions, then reminds you to run `gondwana <command> --help` for detailed usage.

*No arguments or options.*

---

## `gondwana doctor`

Checks the local environment for all Gondwana prerequisites (.NET SDK, templates, release/deploy tooling, native libraries).

| Option | Description |
|---|---|
| `--fix` | Automatically fix issues that can be resolved without manual steps. After applying fixes, all checks are re-run and the updated results are displayed. |

Currently auto-fixable:
- **Gondwana CLI** not installed → runs `dotnet tool install -g Gondwana.Cli`
- **Gondwana Templates** missing → runs `dotnet new install Gondwana.Templates`; if already installed, `--fix` runs `dotnet new update` instead so newer local template packages are retained rather than downgraded
- **wasm-tools** not installed → runs `dotnet workload install wasm-tools`
- **git-cliff** on Windows → runs `winget install/upgrade --id orhun.git-cliff`
- **butler** not installed → downloads the latest binary from the [itch.io broth CDN](https://itch.io/docs/butler/installing.html) and installs it to `%LOCALAPPDATA%\itch\butler` (Windows) or `~/.itch/butler` (Linux/macOS). Run `butler login` after installation to authenticate with itch.io.

**Examples**
```
gondwana doctor
gondwana doctor --fix
```

---

## `gondwana info`

Reads the `.csproj` in the current directory and prints project metadata (name, target framework, Gondwana version, adapters, and discovered asset bundles). When multiple `.csproj` files are present, the first one alphabetically is used.

*No arguments or options.*

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
| `--type <name>` | `-t` | Filter output to assets of the specified type (e.g. `Image`, `Audio`, `Video`, `Font`, `Cursor`, `Svg`, `Misc`). |
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
| `--include-loader` | `-l` | `false` | Also emit a `Load(string? password = null)` static method that calls `AssetsFile.LoadOrCreate` using the bundle file name (resolved relative to the app's working directory). |

**Examples**
```
gondwana assets generate-keys ./game.assets
gondwana assets generate-keys ./game.assets -o ./Generated/AssetKeys.cs -n MyGame -c AssetKeys
gondwana assets generate-keys ./game.assets --password secret
gondwana assets generate-keys ./game.assets --include-loader -o ./Generated/AssetKeys.cs -n MyGame
```

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

If a `.sln` already exists in the output directory, adds the generated project to it; otherwise creates `<name>.sln` and adds the project.

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

If a `.sln` already exists in the output directory, adds the generated project to it; otherwise creates `<name>.sln` and adds the project.

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

If a `.sln` already exists in the output directory, adds the generated project to it; otherwise creates `<name>.sln` and adds the project.

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

## `gondwana run`

### `gondwana run` (desktop)

Runs the desktop build of the project in the current directory.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Debug` | Build configuration (`Debug`, `Release`). |
| `--framework <tfm>` | `-f` | *(auto)* | Target framework (e.g. `net8.0`). Required for multi-target projects. |

**Examples**
```
gondwana run
gondwana run -p ./src/MyGame
gondwana run -c Release
gondwana run -f net8.0
```

Equivalent to `dotnet run --project <path> -c <configuration>`.

---

### `gondwana run wasm`

Builds and runs the project in the browser using the `net8.0-browser` dev server.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Debug` | Build configuration (`Debug`, `Release`). |
| `--skip-workload` | | `false` | Skip `dotnet workload install wasm-tools`. |

**Examples**
```
gondwana run wasm
gondwana run wasm -p ./src/MyGame
gondwana run wasm --skip-workload
gondwana run wasm --skip-workload -c Release
```

Equivalent to `dotnet run --project <path> -f net8.0-browser -c <configuration>`. The Avalonia browser host starts a local dev server and opens the game in the default browser.

---

## `gondwana publish`

| Subcommand | Description |
|---|---|
| *(default)* | Publish the desktop build of the current project. |
| `wasm` | Build and publish the current project for browser/WASM. |
| `itch` | Package a browser/WASM AppBundle as an itch.io-ready zip. |

### `gondwana publish` (desktop)

Publishes the desktop build of the project in the current directory.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Release` | Build configuration (`Release`, `Debug`). |
| `--framework <tfm>` | `-f` | *(auto)* | Desktop target framework to publish. Required only when multiple non-browser target frameworks exist. |
| `--runtime <rid>` | `-r` | *(none)* | Runtime identifier such as `win-x64`, `linux-x64`, or `osx-arm64`. |
| `--output <path>` | `-o` | *(dotnet default)* | Publish output directory. |
| `--self-contained` | | `false` | Publish as self-contained. |
| `--publish-single-file` | | `false` | Publish as a single-file executable. |

Equivalent to `dotnet publish <path> -c <configuration> -f <framework>` with optional `-r <rid>`, `-o <path>`, `--self-contained`, and `/p:PublishSingleFile=true`.

**Examples**
```
gondwana publish
gondwana publish -p ./src/MyGame
gondwana publish -r win-x64
gondwana publish -f net8.0 --self-contained
gondwana publish -r win-x64 --self-contained --publish-single-file
```

On success, the command prints the publish output directory as a plain line when it can be located.

---

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

On success, the command prints the AppBundle path as a plain line. If publish succeeds but the AppBundle cannot be located, a warning is printed.

For packaging or deployment, see also `gondwana publish itch`, `gondwana deploy`, and `gondwana deploy itch`.

---

### `gondwana publish itch`

Publishes the project for `net8.0-browser` (unless `--skip-build`) and packages the AppBundle contents into an itch.io-ready zip with `index.html` at the zip root.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Release` | Build configuration (`Release`, `Debug`). |
| `--output <path>` | `-o` | `bin/<Configuration>/net8.0-browser/browser-wasm/<ProjectName>-itch.zip` | Output zip path. |
| `--skip-build` | | `false` | Skip the dotnet publish step and package an existing AppBundle. |
| `--skip-workload` | | `false` | Skip `dotnet workload install wasm-tools`. |

**Examples**
```
gondwana publish itch
gondwana publish itch -p ./src/MyGame
gondwana publish itch --skip-build
gondwana publish itch -o ./artifacts/MyGame-itch.zip
```

On success, the command prints the zip path as a plain line.

---

## `gondwana deploy`

| Subcommand | Description |
|---|---|
| *(default)* | Deploy a browser/WASM AppBundle to a static web host. |
| `wasm` | Deploy a browser/WASM AppBundle to a static web host. |
| `itch` | Deploy a browser/WASM build to itch.io via `butler`. |

### `gondwana deploy` / `gondwana deploy wasm`

Deploys the project for browser/WASM to a static web host.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to the `.csproj` or its parent directory. |
| `--configuration <name>` | `-c` | `Release` | Build configuration (`Release`, `Debug`). |
| `--web-root <path>` | | *(none)* | Local destination directory for the AppBundle contents. |
| `--remote-host <user@host>` | | *(none)* | SSH remote, used with `--remote-path`. |
| `--remote-path <path>` | | *(none)* | Remote destination path, used with `--remote-host`. |
| `--skip-build` | | `false` | Skip the dotnet publish step and deploy an existing AppBundle. |
| `--skip-workload` | | `false` | Skip `dotnet workload install wasm-tools`. |

Specify either `--web-root` or `--remote-host` + `--remote-path`, not both.

**Examples**
```
gondwana deploy --web-root ./dist/MyGame
gondwana deploy -p ./src/MyGame --web-root ./dist/MyGame
gondwana deploy wasm --remote-host deploy@example.com --remote-path /var/www/html/mygame
gondwana deploy --skip-build --web-root ./dist/MyGame
```

Web servers must send:

```text
Cross-Origin-Opener-Policy:   same-origin
Cross-Origin-Embedder-Policy: require-corp
```

The site must also be served over HTTPS.

On success, the command prints the deploy destination as a plain line: the absolute local path when using `--web-root`, or `user@host:/remote/path/` when using `--remote-host`/`--remote-path`.

---

### `gondwana deploy itch`

Publishes the project for `net8.0-browser` (unless `--skip-build`), packages the AppBundle, and uploads it to itch.io using `butler`.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to the `.csproj` or its parent directory. |
| `--itch-game <user/game>` | | *(required)* | The itch.io game slug. |
| `--itch-channel <name>` | | `html5` | The itch.io release channel name. |
| `--configuration <name>` | `-c` | `Release` | Build configuration (`Release`, `Debug`). |
| `--skip-build` | | `false` | Skip the dotnet publish step and deploy an existing AppBundle. |
| `--skip-workload` | | `false` | Skip `dotnet workload install wasm-tools`. |

**Examples**
```
gondwana deploy itch --itch-game user/mygame
gondwana deploy itch -p ./src/MyGame --itch-game user/mygame
gondwana deploy itch --itch-game user/mygame --itch-channel html5-beta
gondwana deploy itch --skip-build --itch-game user/mygame
```

Prerequisites:
- `butler` on `PATH`
- `butler login` already completed
- the itch.io game already exists

On success, the command prints the game URL as a plain line (e.g. `https://user.itch.io/game`).

---

## `gondwana templates`

| Subcommand | Description |
|---|---|
| `install` | Install `Gondwana.Templates`, or check for updates if already installed. |
| `update` | Check installed Gondwana templates for updates without downgrading newer local versions. |
| `list` | List installed Gondwana templates. |

### `gondwana templates install`

Installs `Gondwana.Templates` from NuGet when it is missing. If it is already installed, this command checks for template updates instead so a newer local package is retained. *No arguments or options.*

### `gondwana templates update`

Runs `dotnet new update`. This checks installed template packages for updates without downgrading a newer already-installed local `Gondwana.Templates` package. *No arguments or options.*

### `gondwana templates list`

Runs `dotnet new list gondwana` and prints matching templates. *No arguments or options.*

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
  "Font":   ["ttf", "otf", "woff", "woff2"],
  "Svg":    ["svg"]
}
```

Valid type names match the `AssetTypes` enum: `Image`, `Audio`, `Video`, `Font`, `Cursor`, `Svg`, `Misc`.
