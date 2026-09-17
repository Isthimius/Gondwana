# Gondwana Asset Files (WinForms)

Standalone .NET 8 Windows editor for Gondwana asset files (`.gaf`). It provides a
simple desktop workflow for creating, inspecting, importing, replacing, exporting,
renaming, and deleting assets stored through `AssetsFile`.

Open this project in Visual Studio, or run it from the repository root:

```console
dotnet run --project Tooling/Gondwana.Tooling.Assets.WinForms -c Release
```

This is development tooling, not a runtime dependency for a game.

## Workflow

- **New** creates a new `.gaf` (or `.zip`) asset file. New files can be created
  either unencrypted or password-protected.
- **Open** loads an existing asset file. If the initial open fails, the editor
  prompts for a password and retries it as an encrypted asset file.
- The main grid lists each asset's **Type**, **Name**, and stored **Size**.
- Use the **Type** selector to show all entries or only one `AssetTypes` value.
  The **Search** box filters the current list by asset name as you type.
- **Add / Import** imports one or more files. Choose the Gondwana asset type once
  for the selected files, then confirm or change the asset name for each file.
- **Replace** keeps the selected asset's type and name while replacing its stored
  contents from another file.
- **Rename** copies the selected asset to the new name and removes the old entry.
- **Export** writes the selected asset back to a normal file. Double-clicking a
  grid row performs the same export operation.
- **Delete** removes the selected asset after confirmation.
- **Refresh** rebuilds the grid from the currently loaded asset file and reapplies
  the current type and search filters.
- **Save** persists the current asset file. **Save As** writes a complete copy to
  another `.gaf` or `.zip`, with an independent choice of password protection.

The status bar reports the current file and filtered asset count, along with the
result of operations such as import, rename, export, and save.

## Asset types

Entries are stored using Gondwana's `AssetTypes` classification rather than being
inferred solely from file extensions. The editor exposes those types directly when
importing and when filtering the grid.

This allows an asset file to contain heterogeneous content while retaining the
runtime type information expected by Gondwana. For example, SVG content is stored
as an `Svg` asset entry rather than merely as an arbitrary file with an `.svg`
extension.

## Asset-file behavior

The editor works through Gondwana's `AssetsFile` API. Asset contents are read and
written as streams, so the tooling does not need format-specific editors for the
payload itself.

A few behaviors are worth noting:

- `.gaf` and `.zip` files are both offered by the file dialogs.
- Password protection is chosen when creating a file or when using **Save As**.
- Opening an encrypted file requires the correct password before entries can be
  enumerated.
- **Save As** copies every entry from the currently open asset file into the new
  destination; it does not merely copy the container file on disk.
- Importing, replacing, renaming, and deleting modify the in-memory `AssetsFile`.
  Use **Save** to persist those changes to the current file.

## Current scope

This tool is intentionally a compact asset-container editor. It manages the assets
inside a Gondwana asset file but does not try to edit the contents of those assets.
For example, image, audio, SVG, tilesheet, and other payloads remain ordinary stored
streams and should be edited with their appropriate authoring tools before import.

The editor currently provides:

- asset-file creation and opening
- optional password-protected containers
- multi-file import
- Gondwana asset-type assignment
- type filtering and name searching
- asset replacement and renaming
- individual asset export
- asset deletion
- Save and Save As

## Development and maintenance

The UI is implemented directly with WinForms and uses the engine's existing
`Gondwana.Assets` model rather than maintaining a second asset-file implementation.
Changes to this tool should therefore preserve compatibility with `AssetsFile` and
`AssetTypes` instead of introducing editor-only file semantics.

Run the project directly for Windows UI verification and exercise at least these
paths when changing asset-file behavior:

1. Create both plain and password-protected asset files.
2. Import multiple assets and assign a type.
3. Filter by type and search by name.
4. Replace, rename, export, and delete an entry.
5. Save, reopen, and verify the resulting entries.
6. Use **Save As** both with and without password protection and reopen the copy.

For broader regression coverage, also run the repository's normal Release build and
test suite.

## Documentation

- [Gondwana source repository](https://github.com/Isthimius/Gondwana)
- [Gondwana Wiki](https://github.com/Isthimius/Gondwana/wiki)
- [API Reference](https://isthimius.github.io/Gondwana/api/)

## License

MIT
