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

.NET SDK             OK  10.0.201
Templates            OK  gondwana-winforms, gondwana-avalonia found
SkiaSharp            OK
SDL2                 Missing native library
LibVLC               Not checked

1 issue found.
```

Checks performed:
- .NET SDK installed and version
- Gondwana templates (`gondwana-winforms`, `gondwana-avalonia`) installed
- SkiaSharp native binaries
- SDL2 native binaries (for `Gondwana.Input.SDL2`)
- LibVLC (for `Gondwana.Video`)

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

# List all assets in a bundle
gondwana assets list ./game.assets

# Extract all assets from a bundle
gondwana assets extract ./game.assets ./Extracted

# Generate a C# constants class for asset keys
gondwana assets generate-keys ./game.assets
gondwana assets generate-keys ./game.assets -o AssetKeys.cs -n MyGame.Assets
```

The `generate-keys` command produces a file like:

```csharp
public static class AssetKeys
{
    public const string PlayerSprite = "sprites/player.png";
    public const string ThemeMusic = "audio/theme.ogg";
}
```

#### Asset type mapping

`gondwana assets pack` maps file extensions to `AssetTypes` values using a JSON config file.
The tool looks for the config in this order:

1. The path given to `--type-map <file>` (if supplied)
2. `gondwana-asset-types.json` in the **current working directory**
3. `gondwana-asset-types.json` next to the `gondwana` executable (the shipped default)

The JSON format is an object whose keys are `AssetTypes` names and whose values are arrays of
file extensions (without a leading dot):

```json
{
  "Image":  ["png", "jpg", "jpeg", "bmp", "gif", "webp", "tiff", "ico"],
  "Audio":  ["wav", "mp3", "ogg", "flac", "aac", "wma", "mid", "midi"],
  "Video":  ["mp4", "avi", "mkv", "mov", "wmv", "webm", "m4v"],
  "Cursor": ["cur", "ani"],
  "Font":   ["ttf", "otf", "woff", "woff2"]
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
