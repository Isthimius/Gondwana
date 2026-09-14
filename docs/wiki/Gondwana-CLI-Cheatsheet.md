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
| [`gondwana help`](#-gondwana-help) | Show a summary of all available commands. |
| [`gondwana doctor`](#-gondwana-doctor) | Validate the machine and Gondwana development toolchain. Pass `--fix` to auto-fix supported issues. |
| [`gondwana check`](#-gondwana-check) | Validate a Gondwana project's packages, resources, and configuration. |
| [`gondwana upgrade`](#-gondwana-upgrade) | Upgrade the Gondwana package family referenced by a project. |
| [`gondwana add <feature>`](#-gondwana-add) | Add a supported Gondwana feature package at the project's existing version. |
| [`gondwana info`](#-gondwana-info) | Show information about the Gondwana project in the current directory. |
| [`gondwana assets <subcommand>`](#-gondwana-assets) | Pack, inspect, validate, unpack, and generate keys for asset bundles. |
| [`gondwana tilesheet <subcommand>`](#-gondwana-tilesheet) | Inspect and validate Gondwana `.gts` tilesheet definitions. |
| [`gondwana pack <source> <output>`](#-gondwana-pack) | Pack a directory into an asset bundle (shorthand for `gondwana assets pack`). |
| [`gondwana new <winforms\|avalonia\|blazor> <name>`](#-gondwana-new) | Scaffold a WinForms, Avalonia, or Blazor WebAssembly/WebGL Gondwana project. |
| [`gondwana run [<subcommand>]`](#-gondwana-run) | Run the desktop project by default, or run the Blazor/WebGL target. |
| [`gondwana publish [<subcommand>]`](#-gondwana-publish) | Publish the desktop project by default, or publish Blazor/WebGL or an itch.io package. |
| [`gondwana deploy [<subcommand>]`](#-gondwana-deploy) | Deploy Blazor/WebGL output by default, or deploy to itch.io. |
| [`gondwana serve`](#-gondwana-serve) | Serve an existing published browser build locally with the correct WASM headers. |
| [`gondwana templates <subcommand>`](#-gondwana-templates) | Manage Gondwana `dotnet new` templates. |

---

## 📜 `gondwana help`

Prints a formatted table of all available commands with short descriptions, then reminds you to run `gondwana <command> --help` for detailed usage.

*No arguments or options.*

---

## 📜 `gondwana doctor`

Checks the machine and development toolchain. This is the broad environment check; use [`gondwana check`](#-gondwana-check) to inspect one project.

| Option | Description |
|---|---|
| `--fix` | Automatically fix supported issues, then rerun all checks and display the updated results. |

Checks performed:

- Git and its version
- .NET SDK and its version (.NET 8 or later is required)
- the repository-local `nbgv` tool
- the Gondwana CLI global tool
- Gondwana templates (`gondwana-winforms`, `gondwana-avalonia`, and `gondwana-blazor`)
- the `wasm-tools` workload
- `git-cliff`
- itch.io `butler`
- SkiaSharp native binaries
- SDL2 native binaries for `Gondwana.Input.SDL2`
- LibVLC for `Gondwana.Video`

A missing `wasm-tools` workload is a failure when the current directory contains a Blazor/WASM project; otherwise it is an optional warning. LibVLC may be reported as **Not checked** when the current project does not reference `Gondwana.Video`.

Currently auto-fixable:

- **Gondwana CLI** missing → runs `dotnet tool install -g Gondwana.Cli`
- **Gondwana Templates** missing → runs `dotnet new install Gondwana.Templates`; when already installed, `--fix` runs `dotnet new update` so a newer local package is not downgraded
- **wasm-tools** missing for a Blazor/WASM project → runs `dotnet workload install wasm-tools`
- **git-cliff** on Windows → runs `winget install/upgrade --id orhun.git-cliff`
- **butler** missing → downloads the latest binary from the [itch.io broth CDN](https://itch.io/docs/butler/installing.html), trying `broth.itch.ovh` first and `broth.itch.zone` as fallback, and installs it to `%LOCALAPPDATA%\itch\butler` (Windows) or `~/.itch/butler` (Linux/macOS). Run `butler login` afterward to authenticate.

**Examples**

```sh
gondwana doctor
gondwana doctor --fix
```

---

## 📜 `gondwana check`

Validates an existing Gondwana project. Without `--project`, the current directory must contain exactly one `.csproj`.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to a `.csproj` or a directory containing exactly one `.csproj`. |
| `--fix` | | `false` | Align older Gondwana packages to the project's explicitly declared stable `Gondwana` core version, then rerun the checks. |

The report includes:

- project name, locally declared target frameworks, and detected platform/host
- Gondwana package and project references
- package-version mismatches and mixed package/source references
- optional hosting-package status
- missing literal project, content, and resource references
- browser-specific package and configuration warnings
- asset bundles and loose `.gts` files found below the project directory

Asset discovery excludes `bin`, `obj`, `.git`, `.vs`, `node_modules`, and directory links. Protected bundles produce a warning and should be checked separately with `gondwana assets validate --password`.

`check --fix` is deliberately conservative. It never downgrades a reference, chooses a release track, adds optional hosting, or rewrites source code. If the project has no unambiguous stable core version to use, run `gondwana upgrade --version <version>` or edit the project manually.

Failures return exit code `1`; warnings alone return `0`.

**Examples**

```sh
gondwana check
gondwana check -p ./src/MyGame
gondwana check --fix
```

---

## 📜 `gondwana upgrade`

Upgrades all referenced Gondwana packages as a coordinated family. It changes package references only; it does not run restore or build.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to a `.csproj` or a directory containing exactly one `.csproj`. |
| `--version <version>` | | *(latest common stable)* | Use an exact version, including a prerelease when explicitly requested. |
| `--dry-run` | | `false` | Print every proposed package/file change without writing files. |

Without `--version`, the command queries NuGet.org and chooses the newest stable version available for every referenced Gondwana package. It refuses an automatic downgrade or a change away from a prerelease track. Passing `--version` works offline; the next normal NuGet restore determines whether that version is available and compatible.

The command supports ordinary version attributes, `<Version>` children, simple shared version properties, `VersionOverride`, and straightforward central package management in the nearest `Directory.Packages.props`. A central edit can affect every project that consumes that entry, so its file path is shown in the preview.

For safety, ambiguous or nonliteral MSBuild arrangements are refused rather than guessed. These include explicit imports, conditional or duplicate Gondwana references, unresolved/ranged versions, `Update`-only references, mixed source/package references, and shared properties also used by unrelated content.

**Examples**

```sh
gondwana upgrade --dry-run
gondwana upgrade --version 2.6.0 --dry-run
gondwana upgrade --version 2.6.0 -p ./src/MyGame/MyGame.csproj
```

---

## 📜 `gondwana add`

Adds a supported feature package using the project's existing aligned Gondwana version. It adds the package reference only; application setup remains in your code.

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<feature>` | | | **Required.** `widgets`, `audio`, `midi`, `gamepad`, `video`, or `hosting`. |
| `--project <path>` | `-p` | *(current directory)* | Path to a `.csproj` or a directory containing exactly one `.csproj`. |

| Feature | Package selected on current `master` |
|---|---|
| `widgets` | `Gondwana.Widgets` |
| `audio` | `Gondwana.Audio.Browser` for Blazor; desktop audio is already included in `Gondwana` core |
| `midi` | `Gondwana.Audio.Midi` (desktop) |
| `gamepad` | `Gondwana.Input.SDL2` (desktop; requires native SDL2) |
| `video` | `Gondwana.Video` (desktop; requires native LibVLC) |
| `hosting` | `Gondwana.WinForms.Hosting`, `Gondwana.Avalonia.Hosting`, or `Gondwana.Blazor.Hosting`, based on an unambiguous adapter |

The command makes no change when the feature is already referenced. It refuses to guess a version for a non-Gondwana project or a platform-specific package when the adapter is ambiguous.

**Examples**

```sh
gondwana add widgets
gondwana add hosting -p ./src/MyGame
gondwana add gamepad --project ./src/MyGame/MyGame.csproj
```

---

## 📜 `gondwana info`

Reads the `.csproj` in the current directory and prints its project name, target framework, detected host, Gondwana version, adapters, and discovered `.gaf`/`.assets` bundles.

When multiple `.csproj` files are present, the command lists them, warns, and uses the first one alphabetically.

*No arguments or options.*

---

## 📜 `gondwana assets`

| Subcommand | Description |
|---|---|
| `pack` | Pack a directory into an asset bundle. |
| `list` | List bundle entries with native asset type, stored name/path, and uncompressed byte count. |
| `inspect` | Show bundle path, entry count, bundle size, and the same entry table as `list`. |
| `validate` | Validate bundle integrity, keys, paths, and packed tilesheets. |
| `unpack` | Safely extract bundle entries to a directory. |
| `extract` | Alias for `assets unpack`. |
| `generate-keys` | Generate a C# constants class for bundle keys. |

---

### `gondwana assets pack <source> <output>`

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<source>` | | | **Required.** Source directory containing files to pack. |
| `<output>` | | | **Required.** Output bundle path, such as `game.assets` or `game.gaf`. |
| `--type <name>` | `-t` | `Misc` | Default type for files whose type cannot be inferred from the extension. |
| `--recurse` | `-r` | `true` | Recurse into subdirectories. |
| `--append` | `-a` | `false` | Append to an existing bundle. Without this option the output is replaced so stale entries do not survive a rerun. |
| `--type-map <file>` | `-m` | *(built-in defaults)* | Optional JSON type-map file. Resolution order: this option → CWD config → executable config → built-in defaults. |
| `--password <value>` | `-p` | *(none)* | Password-protect the bundle. Required with `--encrypt`. |
| `--encrypt` | `-e` | `false` | Encrypt the bundle with AES-256. Requires `--password`. |

**Examples**

```sh
gondwana assets pack ./Assets ./game.assets
gondwana assets pack ./Assets ./game.assets --append
gondwana assets pack ./Assets ./game.assets -m ./my-types.json
gondwana assets pack ./Assets ./game.assets --password secret
gondwana assets pack ./Assets ./game.assets --password secret --encrypt
```

---

### `gondwana assets list <file>`

Lists each entry's native asset type, stored name/path, and uncompressed byte count.

| Argument / Option | Short | Description |
|---|---|---|
| `<file>` | | **Required.** Asset bundle to list. |
| `--type <name>` | `-t` | Filter by `Image`, `Audio`, `Video`, `Font`, `Cursor`, `Svg`, or `Misc`. |
| `--password <value>` | `-p` | Password required for a protected or encrypted bundle. |

**Examples**

```sh
gondwana assets list ./game.assets
gondwana assets list ./game.assets -t Image
gondwana assets list ./game.assets --password secret
```

---

### `gondwana assets inspect <file>`

Shows the absolute bundle path, total entry count, bundle file size, and the same entry table as `assets list`.

| Argument / Option | Short | Description |
|---|---|---|
| `<file>` | | **Required.** Asset bundle to inspect. |
| `--type <name>` | `-t` | Filter the displayed entries by asset type. Summary counts still describe the complete bundle. |
| `--password <value>` | `-p` | Password required for a protected or encrypted bundle. |

**Examples**

```sh
gondwana assets inspect ./game.assets
gondwana assets inspect ./game.assets -t Audio
```

`list` and `inspect` check archive structure and keys but skip the full integrity-data pass. Use `assets validate` for complete bundle validation.

---

### `gondwana assets validate <file>`

Checks ZIP integrity/decryption, native entry keys, duplicate keys, portable safe paths, and packed GTS definitions. Packed tilesheets use their containing bundle when resolving image references. Other binary assets are integrity-checked but are not decoded as media.

| Argument / Option | Short | Description |
|---|---|---|
| `<file>` | | **Required.** Asset bundle to validate. |
| `--password <value>` | `-p` | Password required for a protected or encrypted bundle. |

`--type` is not supported because validation always checks the complete bundle. Invalid bundles return exit code `1`.

**Examples**

```sh
gondwana assets validate ./game.assets
gondwana assets validate ./game.assets --password secret
```

---

### `gondwana assets unpack <file> <output>`

Safely extracts a bundle. `gondwana assets extract` is an exact alias.

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<file>` | | | **Required.** Asset bundle to unpack. |
| `<output>` | | | **Required.** Destination directory; it is created when needed. |
| `--type <name>` | `-t` | *(all)* | Extract only the specified asset type. |
| `--overwrite` | | `false` | Replace existing destination files. |
| `--password <value>` | `-p` | *(none)* | Password required for a protected or encrypted bundle. |

Extraction preflights every path and refuses traversal, symbolic links/junctions, ambiguous destination names, and existing files unless `--overwrite` is supplied. A failed preflight writes no files; an I/O failure during extraction can leave entries already written.

**Examples**

```sh
gondwana assets unpack ./game.assets ./Extracted
gondwana assets unpack ./game.assets ./Extracted --overwrite -t Audio
gondwana assets extract ./game.assets ./Extracted --password secret
```

---

### `gondwana assets generate-keys <file>`

Generates a C# `public static class` containing one `public const string` per asset key, suitable for compile-time use.

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<file>` | | | **Required.** Asset bundle whose keys will be read. |
| `--output <file>` | `-o` | *(stdout)* | Destination `.cs` file. The directory is created automatically. |
| `--namespace <ns>` | `-n` | *(none)* | Namespace for the generated class. |
| `--class <name>` | `-c` | `AssetKeys` | Generated class name. |
| `--password <value>` | `-p` | *(none)* | Password required for a protected or encrypted bundle. |
| `--include-loader` | `-l` | `false` | Add a `Load(string? password = null)` method that creates an `AssetsFile`. |

**Examples**

```sh
gondwana assets generate-keys ./game.assets
gondwana assets generate-keys ./game.assets -o ./Generated/AssetKeys.cs -n MyGame -c AssetKeys
gondwana assets generate-keys ./game.assets --include-loader -o ./Generated/AssetKeys.cs -n MyGame
```

---

## 📜 `gondwana tilesheet`

| Subcommand | Description |
|---|---|
| `info` | Show image, region, frame, collision, overhang, mask, and alpha metadata from a `.gts` file. |
| `validate` | Validate a loose `.gts` definition and its referenced image/layout metadata. |

### `gondwana tilesheet info <file.gts>`

Prints tilesheet name and image source, image dimensions, region count, mask and premultiplied-alpha settings, and—for each region—its area, tile size, frame grid, explicit frame metadata count, collision type/insets, and overhang.

### `gondwana tilesheet validate <file.gts>`

Checks serialization, image references, layout and region bounds, tile dimensions, padding/margins, overhang, collision rectangles and types, duplicate regions/frame coordinates, and frame metadata outside the grid. Negative collision insets are valid expansion, and zero-sized collision geometry is permitted.

Both commands use the engine's GTS serializer and shared layout validator. Loose image or bundle paths must remain inside the `.gts` directory; absolute paths, parent traversal, and links are rejected. A loose definition containing only `Image.AssetEntryName` needs bundle context and should be validated through `gondwana assets validate` instead.

Failures return exit code `1`.

**Examples**

```sh
gondwana tilesheet info ./Assets/forest.gts
gondwana tilesheet validate ./Assets/forest.gts
```

---

## 📜 `gondwana pack`

Top-level shorthand for [`gondwana assets pack`](#gondwana-assets-pack-source-output). It accepts the same arguments and options.

**Examples**

```sh
gondwana pack ./Assets ./game.assets
gondwana pack ./Assets ./game.assets --append
gondwana pack ./Assets ./game.assets --password secret --encrypt
```

---

## 📜 `gondwana new`

| Subcommand | Description |
|---|---|
| `winforms` | Create a WinForms Gondwana project. |
| `avalonia` | Create an Avalonia Gondwana project for Windows, macOS, and Linux. |
| `blazor` | Create a Blazor WebAssembly project using Gondwana's GPU-backed WebGL renderer. |

### `gondwana new winforms <name>`

If a `.sln` exists in the output directory, the generated project is added to it; otherwise `<name>.sln` is created and the project is added.

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<name>` | | | **Required.** New project name. |
| `--output <dir>` | `-o` | `./<name>` | Output directory. |
| `--backbuffer <type>` | `-b` | `bitmap` | `bitmap` for the CPU/Skia path or `gpu` for OpenGL acceleration. |

**Examples**

```sh
gondwana new winforms MyGame
gondwana new winforms MyGame -o ./projects/MyGame
gondwana new winforms MyGame --backbuffer gpu
```

---

### `gondwana new avalonia <name>`

If a `.sln` exists in the output directory, the generated project is added to it; otherwise `<name>.sln` is created and the project is added.

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<name>` | | | **Required.** New project name. |
| `--output <dir>` | `-o` | `./<name>` | Output directory. |
| `--backbuffer <type>` | `-b` | `bitmap` | `bitmap` for the CPU/Skia path or `gpu` for OpenGL acceleration. |

**Examples**

```sh
gondwana new avalonia MyGame
gondwana new avalonia MyGame -o ./projects/MyGame
gondwana new avalonia MyGame --backbuffer gpu
```

---

### `gondwana new blazor <name>`

Scaffolds a `net8.0-browser` Blazor WebAssembly project using Gondwana's GPU-backed WebGL renderer. The CLI does not expose the retained CPU bitmap renderer as a normal project option.

| Argument / Option | Short | Default | Description |
|---|---|---|---|
| `<name>` | | | **Required.** New project name. |
| `--output <dir>` | `-o` | `./<name>` | Output directory. |

The project includes `Program.cs`, `App.razor`, `Pages/Index.razor`, `GameRenderSurface.razor` wrapping `BlazorGpuRenderSurfaceComponent`, a `BlazorGpuGameHost` subclass, `wwwroot/index.html`, and references to `Gondwana.Blazor`, `Gondwana.Blazor.Hosting`, and `Gondwana.Audio.Browser`.

**Examples**

```sh
gondwana new blazor MyGame
gondwana new blazor MyGame -o ./projects/MyGame
cd MyGame
gondwana run blazor
```

---

## 📜 `gondwana run`

### `gondwana run` (desktop default)

Runs the desktop target of a project.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to a `.csproj` or a directory containing exactly one `.csproj`. |
| `--configuration <name>` | `-c` | `Debug` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Desktop target framework; required when the project has multiple desktop targets. |

**Examples**

```sh
gondwana run
gondwana run -p ./src/MyGame
gondwana run -c Release
gondwana run -f net8.0
```

---

### `gondwana run blazor`

Builds and runs the Blazor WebAssembly/WebGL project, then opens its development-server URL in the browser.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to a `.csproj` or a directory containing exactly one `.csproj`. |
| `--configuration <name>` | `-c` | `Debug` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Browser target framework; auto-detected when only one browser target exists. |
| `--skip-workload` | | `false` | Skip checking/installing the `wasm-tools` workload. |

**Examples**

```sh
gondwana run blazor
gondwana run blazor -p ./src/MyGame
gondwana run blazor -f net8.0-browser --skip-workload
```

---

## 📜 `gondwana publish`

| Subcommand | Description |
|---|---|
| *(default)* | Publish the desktop target. |
| `blazor` | Publish a Blazor WebAssembly/WebGL project for browser deployment. |
| `itch` | Package published browser output as an itch.io-ready zip. |

### `gondwana publish` (desktop default)

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to a `.csproj` or a directory containing exactly one `.csproj`. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Desktop target framework; required when more than one non-browser target exists. |
| `--runtime <rid>` | `-r` | *(none)* | Runtime identifier such as `win-x64`, `linux-x64`, or `osx-arm64`. |
| `--output <path>` | `-o` | *(dotnet default)* | Publish output directory. |
| `--self-contained` | | `false` | Include the .NET runtime. |
| `--publish-single-file` | | `false` | Produce a single-file executable. |

**Examples**

```sh
gondwana publish
gondwana publish -p ./src/MyGame
gondwana publish -r win-x64 --self-contained
gondwana publish -r win-x64 --self-contained --publish-single-file
```

On success, the output directory is printed as a plain line when it can be located.

---

### `gondwana publish blazor`

Checks/installs `wasm-tools`, publishes the selected browser target, optionally rewrites the published `<base href>`, and prints the resulting `publish/wwwroot` path.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to a `.csproj` or a directory containing exactly one `.csproj`. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Browser target framework; auto-detected when only one browser target exists. |
| `--base-href <path>` | | *(unchanged)* | Override `<base href>` for subdirectory/static hosting, such as `/games/mygame/` or `./`. |
| `--skip-workload` | | `false` | Skip checking/installing `wasm-tools`. |

**Examples**

```sh
gondwana publish blazor
gondwana publish blazor -p ./src/MyGame
gondwana publish blazor --base-href /games/mygame/
gondwana publish blazor -f net8.0-browser --skip-workload
```

The normal output is `bin/<Configuration>/<framework>/publish/wwwroot/` (with an additional runtime directory when applicable).

---

### `gondwana publish itch`

Publishes the browser target unless `--skip-build` is used, then creates a zip whose root contains `index.html`.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to a `.csproj` or a directory containing exactly one `.csproj`. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Browser target framework. |
| `--output <path>` | `-o` | `<publish-parent>/<ProjectName>-itch.zip` | Output zip path. |
| `--base-href <path>` | | *(unchanged)* | Override the packaged `<base href>`, commonly `./` for portable static hosting. |
| `--skip-build` | | `false` | Package existing published output without running `dotnet publish`. |
| `--skip-workload` | | `false` | Skip checking/installing `wasm-tools` during publish. |

**Examples**

```sh
gondwana publish itch
gondwana publish itch -p ./src/MyGame
gondwana publish itch --base-href ./
gondwana publish itch --skip-build -o ./artifacts/MyGame-itch.zip
```

---

## 📜 `gondwana deploy`

| Subcommand | Description |
|---|---|
| *(default)* | Deploy a Blazor WebAssembly/WebGL build to a local or remote static web root. |
| `blazor` | Explicit form of the default browser deployment command. |
| `itch` | Publish/package and upload a browser build to itch.io with `butler`. |

### `gondwana deploy` / `gondwana deploy blazor` (default)

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to a `.csproj` or a directory containing exactly one `.csproj`. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Browser target framework. |
| `--base-href <path>` | | *(unchanged)* | Override the deployed `<base href>` for the public URL path. |
| `--web-root <path>` | | *(none)* | Local destination for the published `wwwroot` contents. |
| `--remote-host <user@host>` | | *(none)* | SSH target, used with `--remote-path`. |
| `--remote-path <path>` | | *(none)* | Remote destination, used with `--remote-host`. |
| `--skip-build` | | `false` | Deploy existing published output without publishing first. |
| `--skip-workload` | | `false` | Skip checking/installing `wasm-tools` during publish. |
| `--no-mirror` | | `false` | Copy/update without deleting stale destination files. |

Specify either `--web-root` or `--remote-host` plus `--remote-path`, not both. Local deployment mirrors the destination by default; remote deployment uses `rsync -avz --delete`. `--no-mirror` suppresses stale-file deletion.

**Examples**

```sh
gondwana deploy --web-root ./dist/MyGame
gondwana deploy -p ./src/MyGame --web-root ./dist/MyGame --base-href /games/mygame/
gondwana deploy blazor --remote-host deploy@example.com --remote-path /var/www/html/mygame
gondwana deploy --skip-build --web-root ./dist/MyGame --no-mirror
```

The default single-threaded Gondwana browser/WebGL path does **not** require COOP/COEP isolation headers. Those headers are needed only if the application explicitly enables .NET WASM multithreading. Production hosting should use HTTPS.

---

### `gondwana deploy itch`

Publishes/packages the selected browser target unless `--skip-build` is used, then uploads it through `butler`.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Path to a `.csproj` or a directory containing exactly one `.csproj`. |
| `--itch-game <user/game>` | | *(required)* | Existing itch.io game slug. |
| `--itch-channel <name>` | | `html5` | itch.io channel name. |
| `--configuration <name>` | `-c` | `Release` | Build configuration. |
| `--framework <tfm>` | `-f` | *(auto)* | Browser target framework. |
| `--base-href <path>` | | *(unchanged)* | Override the packaged `<base href>`; no itch-specific value is assumed. |
| `--skip-build` | | `false` | Upload existing published output without publishing first. |
| `--skip-workload` | | `false` | Skip checking/installing `wasm-tools` during publish. |

Prerequisites: `butler` on `PATH`, `butler login` completed, and the itch.io game already created.

**Examples**

```sh
gondwana deploy itch --itch-game user/mygame
gondwana deploy itch -p ./src/MyGame --itch-game user/mygame
gondwana deploy itch --itch-game user/mygame --itch-channel html5-beta --base-href ./
gondwana deploy itch --skip-build --itch-game user/mygame
```

On success, the game URL is printed as a plain line.

---

## 📜 `gondwana serve`

Serves an existing published browser build locally. It never publishes automatically; use `gondwana publish blazor` first.

| Option | Short | Default | Description |
|---|---|---|---|
| `--project <path>` | `-p` | *(current directory)* | Project used to locate its published `wwwroot`. |
| `--port <number>` | | `5000` | Localhost port. |
| `--no-open` | | `false` | Do not open the browser automatically. |
| `--configuration <name>` | `-c` | `Release` | Published build configuration to locate. |
| `--framework <tfm>` | `-f` | *(auto)* | Browser framework; useful for a multi-target project. |
| `--root <path>` | | *(auto)* | Serve a custom published `wwwroot` containing `index.html`. |

The built-in .NET HTTP server listens on localhost, supports `GET` and `HEAD`, serves WASM with the correct MIME type, and sends these isolation headers:

```text
Cross-Origin-Opener-Policy:   same-origin
Cross-Origin-Embedder-Policy: require-corp
```

It supports local absolute base-href subpaths and requires no Node or Python installation. It serves uncompressed files and does not provide production compression negotiation, SPA fallback, HTTPS, or remote hosting. Files are indexed at startup; restart `serve` after publishing files with new names.

**Examples**

```sh
gondwana publish blazor
gondwana serve
gondwana serve --project ./src/MyGame --port 5001 --no-open
gondwana serve -c Release -f net8.0-browser
gondwana serve --root ./artifacts/publish/wwwroot --no-open
```

Use `gondwana run blazor` for the development server instead.

---

## 📜 `gondwana templates`

| Subcommand | Description |
|---|---|
| `install` | Install `Gondwana.Templates`, or check for updates when already installed. |
| `update` | Check installed Gondwana templates for updates without downgrading newer local versions. |
| `list` | List installed Gondwana templates. |

### `gondwana templates install`

Installs `Gondwana.Templates` from NuGet when missing. When already installed, checks for updates instead so a newer local package is retained. *No arguments or options.*

### `gondwana templates update`

Runs `dotnet new update`. *No arguments or options.*

### `gondwana templates list`

Runs `dotnet new list gondwana`. *No arguments or options.*

---

## Asset type-map JSON format

The `--type-map` option is optional. When omitted, `assets pack` and the top-level `pack` alias resolve their type configuration in this order:

1. The path passed to `--type-map <file>`
2. `gondwana-asset-types.json` in the current working directory
3. `gondwana-asset-types.json` beside the `gondwana` executable
4. Built-in defaults

Example `gondwana-asset-types.json`:

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

Valid type names match the `AssetTypes` enum: `Image`, `Audio`, `Video`, `Font`, `Cursor`, `Svg`, and `Misc`.

---

## Where to read next

- [Gondwana.CLI README](https://github.com/Isthimius/Gondwana/tree/master/Tooling/Gondwana.Cli)
