# External Asset Importing

Gondwana Studio can import supported Godot 3.x and Godot 4.x TileSets, Godot 4 tile scenes, Tiled TMX/TSX
content, and Aseprite sprites into native Gondwana authoring formats.
Import is a one-time conversion, not live foreign-format runtime support.
Games load ordinary GTS, GANI and GSCN files through existing serializers.
Godot, Tiled and Aseprite do not need to be installed.

## Workflow

Build the External Import plugin, start Studio, and use its **External Asset
Importer** panel (recover hidden panels through **View**). Choose Auto-detect or
a format, a source file, and an output directory. The directory defaults to the
current Studio working directory. **Analyze** lists proposed files, their logical member identities, and diagnostics
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
| Godot 3.x TileSet (`format=2`) | Text .tres resources, single tiles, atlas tiles, underlying autotile atlas geometry, multiple tile IDs/textures, region offsets, tile sizes and spacing → grouped GTS | No binary .res or .tscn scenes. Autotile bitmask/priority/fallback/icon-selection semantics are not preserved. Collision, navigation, occlusion, materials, normal maps, modulation, offsets, z-index and unfamiliar metadata are diagnosed and omitted. No GANI is generated; AnimatedTexture traversal is unsupported. |
| Godot 4 `.tres` TileSet | Text atlas sources, multiple images, margins/separation, single- and multi-cell atlas tiles, sparse atlas diagnostics, animation layout/speed/per-frame durations → grouped GTS/GANI | No `.res` or scene collections; use the `.tscn` provider for tile scenes. Multi-cell tiles become named regions containing their full atlas rectangles; animation frames have distinct regions when needed. Alternatives, TileData, terrain, polygons, navigation, proxies and custom metadata are diagnosed and omitted. Random animation starts warn. |
| Godot 4 `.tscn` tile scene | Legacy TileMap (`format = 2` cell data) and TileMapLayer (version 0 byte records), external `.tres` and embedded TileSets, multiple atlas sources, animations, negative coordinates, integer translations, visibility, layer order, H/V/transpose flags → GSCN plus one grouped GTS and GANI per atlas source | Orthogonal cells only. A plain Node root and Node2D translation/visibility/z-order ancestors are supported. Nontrivial transforms, Y sorting, proxies, alternatives, undeclared tiles, incompatible artwork dimensions, transposed rectangular tiles with mixed-parity dimensions (which require half-pixel anchoring), and unsupported tile ancestors block import. No general scene conversion or scene instancing. Scripts, cameras, physics, navigation, terrain rules and appearance overrides are diagnosed and omitted. |
| Tiled `.tsx` | Single image atlas, margin/spacing, exact RGB mask, looping tile animations with millisecond timing → GTS/GANI | No image collections, embedded image bytes, nonzero tile offsets or per-tile subrectangles. Wang/terrain, properties, collision objects and transformation metadata warn and are omitted. |
| Tiled `.tmx` | Finite orthogonal/isometric maps, external/embedded atlas tilesets grouped into one GTS document per map, multiple tilesets in one layer, sparse empty cells, visibility/order, integer pixel offsets, equal-axis parallax, animation references, all eight H/V/diagonal tile orientations → GSCN plus GTS/GANI | No infinite, staggered, hexagonal or oblique maps. H/V/diagonal flags become native TileTransform placements. The retained hex-only 120-degree bit is cleared with a warning on supported maps. Oversized atlas frames are mapped to GTS region overhang (top/right, with bottom-left tile-layer anchoring); tilesets using `tilerendersize="grid"` instead draw at map-cell size. Smaller-than-cell frames, drawing offsets requiring negative overhang and grid `preserve-aspect-fit` are unsupported. Representable drawing offsets are preserved as top/bottom/left/right OH. Object/image layers are omitted with warnings; opacity/tint/blend modes are omitted with warnings. Unequal-axis parallax warns and defaults to 1. |
| Aseprite `.ase` / `.aseprite` | RGBA, grayscale, indexed palettes/transparency, raw/zlib/linked cels, visibility, Normal blending, opacity, groups, cel z-order, tags and mixed timing → PNG/GTS/GANI | No tilemaps, reference layers, non-Normal blends, precise/scaled cel bounds, non-square pixel aspect ratios, external dependencies, ICC or custom gamma. Other metadata chunks are diagnosed and omitted. |

TMX supports XML tile elements, CSV, uncompressed base64, base64+zlib and
base64+gzip. Zstd is explicitly unsupported. Groups flatten recursively with
inherited visibility, offsets and parallax. Isometric placement uses Gondwana's
rhombic coordinates and compensates for Tiled's map origin.
Non-default orthogonal render order and nonzero map parallax origins are errors;
these cannot be reproduced by the current scene mapping.

For example, a 32×32 Tiled atlas tile on a 32×24 TMX map cell imports
with a GTS frame size of 32×32 and `Overhang.Top = 8`. An atlas tile
of 40×40 on a 32×24 map cell imports with `Overhang.Top = 16` and
`Overhang.Right = 8`. The atlas pixels are not cropped or resampled;
Gondwana uses the expanded draw rectangle when rendering. TMX imports
derive OH from the map-cell dimensions and Tiled tileset drawing offsets.
For example, the official Tiled 64×64-on-64×32 isometric example with a
16-pixel downward tile offset imports with `Overhang.Top = 16` and
`Overhang.Bottom = 16`. The source frame remains 64×64.
Importing a TSX alone cannot determine map-relative overhang, and nonzero
tile offsets still require TMX context.

For oversized frames with Tiled H/V/diagonal GID flags, conversion accepts
transformations only when Gondwana's oriented draw bounds preserve Tiled's
bottom-left tile-layer placement. For example, flipping a top-only overhang
horizontally is supported; flipping it vertically would move the overhang to
the bottom and is rejected with an explicit diagnostic. Full multi-axis
flipping remains supported for ordinary tiles without OH.


## Names and paths

Names are normalized, lowercased and sanitized by one shared helper. Windows
reserved filenames are protected on every platform. Duplicate outputs or logical
keys produce errors; there are no automatic `(2)` suffixes. Existing files are
protected by default. Source files/dependencies cannot be overwritten as outputs.
Analysis also checks logical keys in other native files directly in the output
directory. Files that cannot be inspected produce a warning; it does not search
unrelated directories or asset archives for global name collisions.

For Godot 4, `terrain.tres` produces one `terrain.gts` document regardless of
the number of atlas sources. A single source uses the logical name `terrain`;
multiple sources become members named `terrain.source.0`, `terrain.source.2`,
etc. Godot animation keys append `.tile.X.Y` to the source's logical name,
and every generated GANI references the shared `terrain.gts` document.
For Godot 3, one tile ID produces `terrain.gts` with logical name `terrain`.
Multiple tile IDs share that document with member names `terrain.tile.0`,
`terrain.tile.3`, etc., even when they use different image textures. The
serialized tile display name does not override these deterministic names.

Standalone TSX uses its sanitized tileset name as the logical name and filename
for its single-member GTS; animated tiles share `terrain-animations.gani`, with keys
such as `terrain.tile.17`. A TMX import instead groups **all** referenced external
and embedded tilesets into one `<map-basename>.gts` document, preserving each
tileset's logical name and independent image path. GSCN sources for every tileset
refer to the same grouped GTS path but retain distinct logical selectors. GANI
tilesheet sources also refer to that grouped path; generated animation files
remain grouped per tileset (`<tileset-basename>-animations.gani`). Embedded
tilesets retain map-basename-plus-firstgid identifiers for the generated
animation filenames. Importing a TSX by itself does not change this behavior.
When re-importing a TMX into a directory containing legacy per-tileset GTS files,
manually move or remove files whose logical keys are now in the map-level GTS.
Overwrite does not bypass logical-key checks against other native files, so the
import is blocked without changing outputs until those legacy files are cleaned up.

Definition dependencies use paths relative to the output directory whenever
possible, with `/` separators. Existing atlas images remain source dependencies;
keep or copy those images when moving imported content. No staging paths are
serialized. Godot `res://` paths resolve from the nearest ancestor containing
`project.godot` when available. Standalone TileSets without a project file
resolve `res://` relative to the source `.tres` directory instead; place referenced
textures at those relative paths. Missing textures remain errors, but creating a
dummy Godot project file is not required.

## Generated documents and grouping

A physical native output file contains a `Definitions` document. Each member has its own logical identity (`Name` for GTS, `Key` for GANI/GSPR/GSND, and `ID` for GSCN). The Studio preview lists all identities beside the single output path. The shared pipeline collects typed members, validates complete documents using the core serializers, and serializes each document once before transactional staging.

Grouping is chosen by each provider's output filenames. Aseprite groups tags from a sprite into one GANI document. Tiled groups tile animations from a tileset into `<tileset-basename>-animations.gani`; TMX scene references retain each animation key while sharing that path. A TMX map's GTS definitions share one physical document while standalone TSX imports remain single-member. Godot 4 groups animations per atlas source into `<source-basename>-animations.gani`. Godot 3 and 4 group all GTS members from the same source TileSet into one physical GTS document, and TMX produces one GSCN per map. The pipeline also accepts grouped GSPR, GSND, and scene or tilesheet collections from providers; the current external converters do not invent sprite, audio, or scene-collection features.

The plan assigns generated-source provenance to all native members and retains each serializer's existing persistence rules (GSPR and GSND load provenance is not serialized). Cross-definition references retain logical selectors and output-relative paths. Duplicate identities within or across generated documents of the same type are errors, as are conflicts with any member of other native documents directly in the output directory.

External conversion creates complete output documents. Overwrite replaces a destination file in full; it never appends to an existing `Definitions` collection. Use an editor's **Import definition…** command to combine existing native definitions interactively. External conversion does not relocate existing referenced resources or change GAF packaging.

## Godot tile scenes

Choose **Godot 4 Tile Scene (.tscn)** or Auto-detect in Studio. Godot text
formats 3 and 4 are recognized; TileMapLayer data can use base64 or numeric
`PackedByteArray` values. The test fixtures were saved with official Godot 4.3
and 4.4 builds. Older Godot 3 TileMap layouts and unknown byte-data versions
are rejected rather than guessed.

Each imported tile layer uses its occupied bounding rectangle. Cell coordinates
are rebased to nonnegative GSCN indices, and the layer's `OriginPx` compensates
for the original coordinates and accumulated integer Node2D translations.
This preserves world positions, including tiles left of or above the Godot
origin. Empty layers become empty 1×1 grids. A layer exceeding one million
cells in its bounding rectangle must be split before import. Visibility includes
ancestor visibility and the tile layer's enabled state. Layers are ordered by
effective Godot z-index, then source order, with unique native ZOrder values.
Native layer IDs use `<scene-name>.layer.<source-order-ordinal>` so legacy
TileMap layers cannot collide with child node paths. Diagnostics retain Godot
node/layer paths for locating the source of a problem.

For `level.tscn`, all referenced TileSets share `level.gts`. Logical identities
are scene-scoped: `level.tileset.0.source.3`, for example. TileSet ordinals follow
first reference order; repeated references to the same TileSet are reused.
A TileSet with one atlas omits the `.source.ID` suffix. Embedded and external
TileSets use the same converter as standalone `.tres` import. Each atlas's
animations share `<atlas-name>-animations.gani` (multi-source filenames use
`-source-ID`); GSCN and GANI retain logical selectors into `level.gts`.
Textures resolve relative to their owning `.tres` or `.tscn`, with `res://`
resolved from the nearest `project.godot` ancestor, or the owning file's directory
when no project file exists. Textures remain external image dependencies.

The scene mapping requires selected artwork dimensions to match logical cell
dimensions. Named multi-cell regions are usable only when their resulting frame
size matches the logical cell. Texture-origin offsets, per-tile rendering
overrides, custom alternatives, proxy remapping and nonorthogonal TileSets block
scene import. Geometry-affecting hierarchy properties such as rotation, scaling,
skew, top-level transforms and Y sorting must be removed or baked before import.
Unsupported leaf nodes are omitted with warnings; a tile node beneath an
unsupported ancestor blocks import. Plain Node is allowed only at the root;
intermediate non-CanvasItem nodes break Godot inheritance and are not flattened.
Effective z-index outside Godot's canvas range also blocks import. Scripts are not executed. Metadata such as
physics, navigation and terrain definitions may warn without blocking when it
does not affect the explicit tile placement being converted.

Runtime loading uses the existing native registries: load all GTS members, then
GANI cycles, then materialize GSCN. The scene's dependency sources remain
authoring references; they do not introduce automatic foreign-resource loading.

## Godot multi-cell regions

Godot 4 `size_in_atlas` values larger than one cell are represented as named
GTS regions (for example, `tile-0-0`). Their image rectangle includes the
texture-region cells and any inter-cell separation pixels. Animated multi-cell
tiles use per-frame regions such as `tile-0-0-frame-1`; GANI frames reference
those regions at local frame coordinate (0, 0). The default atlas grid remains
available for normal-size tiles. This preserves visual slicing geometry without
claiming to preserve Godot terrain rules or collision metadata. Unsupported or
out-of-bounds spans are diagnosed with atlas source, tile coordinate and frame.

## Aseprite layout and playback

Every rendered frame retains the full canvas size. The atlas uses deterministic
rows bounded to approximately 4096 pixels; each populated row gets a GTS region,
so an incomplete final row introduces no phantom frames. Total decoded canvas
pixels are limited to 64 million and atlas dimensions to 16384 pixels.

Tags produce sanitized animation keys in one `<sprite-basename>.gani` document. Forward and reverse loops use
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
