# External Asset Importing

Gondwana Studio can import supported Godot 4 atlas TileSets, Tiled TMX/TSX
content, and Aseprite sprites into native Gondwana authoring formats.
Import is a one-time conversion, not live foreign-format runtime support.
Games load ordinary GTS, GANI and GSCN files through existing serializers.
Godot, Tiled and Aseprite do not need to be installed.

## Workflow

Build the External Import plugin, start Studio, and use its **External Asset
Importer** panel (recover hidden panels through **View**). Choose Auto-detect or
a format, a source file, and an output directory. The directory defaults to the
current Studio working directory. **Analyze** lists proposed files and diagnostics
without writing anything. **Import** is enabled only after successful analysis.
Changing inputs invalidates the analysis. Work runs in a cancellable background
task; completion is presented on Studio's UI thread.

Errors block the whole import. Warnings identify omitted or defaulted source
features. Informational diagnostics explain metadata and geometry differences.
Review warnings before importing. Parsing and native validation finish before
files are staged. Output files are replaced only with explicit overwrite enabled.
Cancellation or a commit failure rolls back files already committed when possible;
if recovery fails, the error identifies the staging directory containing backups.
This is not a transaction against external applications editing the same files.

Successful import refreshes the working-directory browser, logs generated paths,
and opens the primary GSCN or GTS document. Existing open documents retain Studio's
normal activation behavior; import does not discard unsaved editor changes.

## Support matrix

| Source | Supported conversion | Explicit limitations |
| --- | --- | --- |
| Godot 4 `.tres` TileSet | Text atlas sources, multiple images, margins/separation, 1×1 grid tiles, sparse atlas diagnostics, animation layout/speed/per-frame durations → GTS/GANI | No `.res`, scenes, scene collections or multi-cell tiles. Alternatives, TileData, terrain, polygons, navigation, proxies and custom metadata are diagnosed and omitted. Random animation starts warn. |
| Tiled `.tsx` | Single image atlas, margin/spacing, exact RGB mask, looping tile animations with millisecond timing → GTS/GANI | No image collections, embedded image bytes, nonzero tile offsets or per-tile subrectangles. Wang/terrain, properties, collision objects and transformation metadata warn and are omitted. |
| Tiled `.tmx` | Finite orthogonal/isometric maps, external/embedded atlas tilesets, multiple tilesets in one layer, sparse empty cells, visibility/order, integer pixel offsets, equal-axis parallax, animation references → GSCN plus GTS/GANI | No infinite, staggered, hexagonal or oblique maps. Flip/rotation flags are errors. Frame dimensions must match map cells. Object/image layers are omitted with warnings; opacity/tint/blend modes are omitted with warnings. Unequal-axis parallax warns and defaults to 1. |
| Aseprite `.ase` / `.aseprite` | RGBA, grayscale, indexed palettes/transparency, raw/zlib/linked cels, visibility, Normal blending, opacity, groups, cel z-order, tags and mixed timing → PNG/GTS/GANI | No tilemaps, reference layers, non-Normal blends, precise/scaled cel bounds, non-square pixel aspect ratios, external dependencies, ICC or custom gamma. Other metadata chunks are diagnosed and omitted. |

TMX supports XML tile elements, CSV, uncompressed base64, base64+zlib and
base64+gzip. Zstd is explicitly unsupported. Groups flatten recursively with
inherited visibility, offsets and parallax. Isometric placement uses Gondwana's
rhombic coordinates and compensates for Tiled's map origin.
Non-default orthogonal render order and nonzero map parallax origins are errors;
these cannot be reproduced by the current scene mapping.

## Names and paths

Names are normalized, lowercased and sanitized by one shared helper. Windows
reserved filenames are protected on every platform. Duplicate outputs or logical
keys produce errors; there are no automatic `(2)` suffixes. Existing files are
protected by default. Source files/dependencies cannot be overwritten as outputs.
Analysis also checks logical keys in other native files directly in the output
directory. Files that cannot be inspected produce a warning; it does not search
unrelated directories or asset archives for global name collisions.

Single-source `terrain.tres` produces `terrain.gts`; multiple sources produce
`terrain-source-0.gts`, `terrain-source-2.gts`, with logical names
`terrain.source.0`, `terrain.source.2`. Godot animation keys append `.tile.X.Y`.
TSX uses its sanitized tileset name as the logical name and source basename for
files; animated tile 17 produces `terrain-tile-17.gani` and `terrain.tile.17`.
Embedded TMX tilesets use map basename plus firstgid for filenames.

Definition dependencies use paths relative to the output directory whenever
possible, with `/` separators. Existing atlas images remain source dependencies;
keep or copy those images when moving imported content. No staging paths are
serialized. Godot `res://` paths resolve from the nearest ancestor containing
`project.godot`; an absent project root produces an actionable error.

## Aseprite layout and playback

Every rendered frame retains the full canvas size. The atlas uses deterministic
rows bounded to approximately 4096 pixels; each populated row gets a GTS region,
so an incomplete final row introduces no phantom frames. Total decoded canvas
pixels are limited to 64 million and atlas dimensions to 16384 pixels.

Tags produce sanitized GANI filenames/keys. Forward and reverse loops use
Repeating; ping-pong and reverse ping-pong use PingPong with the appropriate
sequence order. Positive repeat counts expand to a finite Simple sequence (up to
100000 frame occurrences); ping-pong repeats count directional passes. Untagged
sprites get one `all` animation. Timing uses per-frame `DurationSeconds`.

See [[GANI Files|GANI-Files]] for timing and [[GTS Files|GTS-Files]] for trailing
margin compensation. Animation graphs and game-specific animation state logic
are outside this importer.

Format references: [Tiled TMX](https://doc.mapeditor.org/en/stable/reference/tmx-map-format/),
[Godot TileSetAtlasSource](https://docs.godotengine.org/en/stable/classes/class_tilesetatlassource.html),
[Aseprite binary specification](https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md).
