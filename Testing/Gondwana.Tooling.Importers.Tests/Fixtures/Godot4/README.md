# Godot-generated tile scene fixtures

`legacy.tscn`, `layers.tscn`, `terrain.tres`, and `atlas.png` were saved by
the official **Godot 4.4-stable** Windows binary (`4c311cbee`).
`layers-43.tscn` was saved separately by **Godot 4.3-stable** (`77dcf97d8`)
using the same generator. The artwork is generated locally and has no external
licensing dependencies. No packed cell data was handwritten.

To regenerate, in this directory run:

```console
Godot_v4.4-stable_win64_console.exe --headless --path . --script generate.gd
```

For `layers-43.tscn`, run that script in a temporary copy of this project with
Godot 4.3 and copy the resulting `layers.tscn` to `layers-43.tscn` here.
Resource IDs may change between saves. Tests use logical cells, not those IDs.
Normal .NET tests need neither Godot nor network access.

The legacy scene contains two TileMap layers using an external TileSet, negative
coordinates, source IDs 3 and 9, nonzero atlas coordinates, animation, a flipped
tile, disabled-layer visibility, negative z order and parent/local translation.
The newer scenes contain external and embedded TileSets and all eight placement
orientations, in addition to visibility, translation and negative coordinates.

Serialization references:

- [Godot 4.2 TileMap source](https://github.com/godotengine/godot/blob/4.2/scene/2d/tile_map.cpp):
  `TileMapLayer::set_tile_data` / `get_tile_data`. Serialized `format = 2` is
  the zero-based `FORMAT_3` enumerator. Three little-endian int32 words encode
  six 16-bit values: signed cell X/Y, source ID, atlas X/Y, alternative/flags.
- [Godot 4.4 TileMapLayer source](https://github.com/godotengine/godot/blob/4.4/scene/2d/tile_map_layer.cpp):
  `set_tile_map_data_from_array` / `get_tile_map_data_as_array`. A uint16 version
  (0) precedes the same 12-byte records. Empty layers have an empty byte array.
  ResourceSaver emits text format 4 with a base64 PackedByteArray in both tested
  4.3 and 4.4 versions. Numeric PackedByteArray values are also accepted; tests
  derive those bytes from a generated fixture instead of inventing a layout.
- [Godot transform constants](https://docs.godotengine.org/en/4.4/classes/class_tilesetatlassource.html#constants):
  H = 4096, V = 8192, transpose = 16384. Transpose precedes H/V; H+transpose is
  clockwise 90 degrees. Other alternative bits identify unsupported alternatives.
