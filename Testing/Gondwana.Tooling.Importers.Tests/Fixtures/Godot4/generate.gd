extends SceneTree

# Run with Godot 4.4-stable --headless --path . --script generate.gd.
# ResourceSaver, not hand-packed arrays, owns the fixture serialization.
func _initialize():
	var image = Image.create(64, 32, false, Image.FORMAT_RGBA8)
	image.fill(Color.DARK_BLUE)
	image.fill_rect(Rect2i(0, 0, 8, 16), Color.RED)
	image.fill_rect(Rect2i(16, 0, 16, 8), Color.YELLOW)
	image.save_png("res://atlas.png")
	# Imported texture paths remain external references in saved resources.
	var texture = ImageTexture.create_from_image(image)
	texture.take_over_path("res://atlas.png")
	var tiles = make_tiles(texture)
	ResourceSaver.save(tiles, "res://terrain.tres")
	tiles.take_over_path("res://terrain.tres")
	var root = Node2D.new()
	root.name = "World"
	root.position = Vector2(7, -9)
	var legacy = TileMap.new()
	legacy.name = "Legacy"
	root.add_child(legacy)
	legacy.owner = root
	legacy.tile_set = tiles
	legacy.position = Vector2(16, 32)
	legacy.set_cell(0, Vector2i(-2, -1), 3, Vector2i(0, 0))
	legacy.set_cell(0, Vector2i(1, 2), 9, Vector2i(2, 1), TileSetAtlasSource.TRANSFORM_FLIP_H)
	legacy.add_layer(1)
	legacy.set_layer_name(1, "Hidden")
	legacy.set_layer_enabled(1, false)
	legacy.set_layer_z_index(1, -2)
	legacy.set_cell(1, Vector2i(-1, 0), 3, Vector2i(2, 1))
	save_scene(root, "res://legacy.tscn")
	root.free()
	root = Node2D.new()
	root.name = "World"
	root.position = Vector2(7, -9)
	var group = Node2D.new()
	group.name = "Group"
	group.position = Vector2(16, 32)
	root.add_child(group)
	group.owner = root
	var layer = TileMapLayer.new()
	layer.name = "Ground"
	group.add_child(layer)
	layer.owner = root
	layer.tile_set = tiles
	layer.set_cell(Vector2i(-2, -1), 3, Vector2i(0, 0))
	layer.set_cell(Vector2i(1, 2), 9, Vector2i(2, 1))
	for flags in range(8):
		layer.set_cell(Vector2i(flags - 2, 0), 3, Vector2i(2, 1), flags << 12)
	var embedded = TileMapLayer.new()
	embedded.name = "Embedded"
	root.add_child(embedded)
	embedded.owner = root
	embedded.tile_set = make_tiles(texture)
	embedded.visible = false
	embedded.z_index = -1
	embedded.set_cell(Vector2i(0, -3), 9, Vector2i(2, 1))
	save_scene(root, "res://layers.tscn")
	root.free()
	quit()

func make_tiles(texture: Texture2D) -> TileSet:
	var tiles = TileSet.new()
	tiles.tile_size = Vector2i(16, 16)
	for id in [3, 9]:
		var atlas = TileSetAtlasSource.new()
		atlas.texture = texture
		atlas.texture_region_size = Vector2i(16, 16)
		atlas.create_tile(Vector2i(0, 0))
		atlas.set_tile_animation_columns(Vector2i(0, 0), 2)
		atlas.set_tile_animation_frames_count(Vector2i(0, 0), 2)
		atlas.set_tile_animation_speed(Vector2i(0, 0), 4)
		atlas.set_tile_animation_frame_duration(Vector2i(0, 0), 1, 3)
		atlas.create_tile(Vector2i(2, 1))
		tiles.add_source(atlas, id)
	return tiles

func save_scene(root: Node, path: String):
	var scene = PackedScene.new()
	assert(scene.pack(root) == OK)
	assert(ResourceSaver.save(scene, path) == OK)
