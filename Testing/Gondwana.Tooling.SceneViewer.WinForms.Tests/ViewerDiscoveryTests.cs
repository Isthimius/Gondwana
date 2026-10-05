using System.Text;
using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.SceneViewer.WinForms.Tests;

public sealed partial class ViewerTests
{
    private void Pack(string file, AssetTypes type, string entry, string json)
    {
        using var archive = AssetsFile.LoadOrCreate(Path.Combine(_directory, file));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        archive.Add(type, entry, stream);
        archive.Save();
    }

    private void PackSheet(bool removeLoose = true, string archive = "sheets.gaf")
    {
        var path = Path.Combine(_directory, "sheet.gts");
        Pack(archive, AssetTypes.TilesheetDefinition, "arbitrary-sheet-entry", File.ReadAllText(path));
        if (removeLoose) File.Delete(path);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void GaniDependenciesUseSceneCatalog(bool packedAnimation, bool packedSheet)
    {
        WriteContent();
        var sceneDefinition = SceneDefinitionSerializer.Load(ScenePath);
        sceneDefinition.TilesheetSources.Clear();
        sceneDefinition.AnimationSources[0].GaniPath = "missing.gani";
        SceneDefinitionSerializer.Save(ScenePath, sceneDefinition);
        var animationPath = Path.Combine(_directory, "animation.gani");
        var animation = AnimationDefinitionSerializer.Load(animationPath);
        animation.TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("sheet", "missing.gts")];
        AnimationDefinitionSerializer.Save(animationPath, animation);
        if (packedAnimation)
        {
            Pack("animations.gaf", AssetTypes.AnimationDefinition, "unrelated-entry", File.ReadAllText(animationPath));
            File.Delete(animationPath);
        }
        if (packedSheet) PackSheet();
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.True(scene[0]![0, 0]!.TileAnimator.IsCycling);
        Assert.Equal(32, TilesheetRegistry.Instance.GetOrNull("sheet")!.SkBitmap.Width);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SceneSheetUsesLogicalIdentityInsteadOfFilename(bool packed)
    {
        WriteContent();
        var definition = SceneDefinitionSerializer.Load(ScenePath);
        definition.TilesheetSources[0].GtsPath = "missing.gts";
        SceneDefinitionSerializer.Save(ScenePath, definition);
        if (packed) PackSheet();
        else File.Move(Path.Combine(_directory, "sheet.gts"), Path.Combine(_directory, "unrelated.gts"));
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.NotNull(TilesheetRegistry.Instance.GetOrNull("sheet"));
    }

    [Fact]
    public void LooseDefinitionsOverridePackedAndUnifyDependencyPaths()
    {
        WriteContent();
        var sheet = TilesheetDefinitionSerializer.Load(Path.Combine(_directory, "sheet.gts"));
        sheet.Image.FilePath = "missing-packed-image.png";
        Pack("a.gaf", AssetTypes.TilesheetDefinition, "sheet", TilesheetDefinitionSerializer.ToJson(sheet));
        Pack("b.gaf", AssetTypes.TilesheetDefinition, "sheet", TilesheetDefinitionSerializer.ToJson(sheet));
        var animation = AnimationDefinitionSerializer.Load(Path.Combine(_directory, "animation.gani"));
        animation.Frames[0].XTile = 99;
        Pack("a.gaf", AssetTypes.AnimationDefinition, "walk", AnimationDefinitionSerializer.ToJson(animation));
        Pack("b.gaf", AssetTypes.AnimationDefinition, "walk", AnimationDefinitionSerializer.ToJson(animation));
        var definition = SceneDefinitionSerializer.Load(ScenePath);
        definition.TilesheetSources = [SceneTilesheetSourceDefinition.Packed("sheet", "a.gaf", "sheet")];
        definition.AnimationSources = [SceneAnimationSourceDefinition.Packed("walk", "a.gaf", "walk")];
        SceneDefinitionSerializer.Save(ScenePath, definition);
        WriteSprites("actor.gspr", false, Sprite("Player"));
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.Single(SpriteManager.Instance.AllSprites);
        Assert.True(scene[0]![0, 0]!.TileAnimator.IsCycling);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DuplicateDefinitionIdentitiesReportEverySource(bool animation, bool packed)
    {
        WriteContent();
        var extension = animation ? ".gani" : ".gts";
        var path = Path.Combine(_directory, animation ? "animation.gani" : "sheet.gts");
        if (packed)
        {
            var type = animation ? AssetTypes.AnimationDefinition : AssetTypes.TilesheetDefinition;
            Pack("z.gaf", type, "z-entry", File.ReadAllText(path));
            Pack("a.gaf", type, "a-entry", File.ReadAllText(path));
            File.Delete(path);
        }
        else File.Copy(path, Path.Combine(_directory, "duplicate" + extension));
        var error = Assert.Throws<InvalidDataException>(() => new ViewerSceneLoader().Load(ScenePath));
        Assert.Contains($"Multiple {(packed ? "packed" : "loose")} {(animation ? "GANI" : "GTS")}", error.Message);
        Assert.Contains(packed ? "a.gaf :: a-entry" : "duplicate" + extension, error.Message);
        Assert.Contains(packed ? "z.gaf :: z-entry" : Path.GetFileName(path), error.Message);
    }

    [Fact]
    public void ExplicitFallbackRemainsRelativeToContainingGani()
    {
        WriteContent();
        var external = Path.Combine(_directory, "external");
        Directory.CreateDirectory(external);
        foreach (var file in new[] { "sheet.gts", "animation.gani", "image.png" })
            File.Move(Path.Combine(_directory, file), Path.Combine(external, file));
        var definition = SceneDefinitionSerializer.Load(ScenePath);
        definition.TilesheetSources.Clear();
        definition.AnimationSources[0].GaniPath = "external/animation.gani";
        SceneDefinitionSerializer.Save(ScenePath, definition);
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.True(scene[0]![0, 0]!.TileAnimator.IsCycling);
    }

    private static SpriteInstanceDefinition Sprite(string? nickname, string scene = "viewer-test") => new()
    {
        Id = Guid.NewGuid(),
        Nickname = nickname,
        SceneId = scene,
        SceneLayerId = "front",
        Position = new(7, 11),
        Frame = new() { Tilesheet = "sheet" }
    };

    private void WriteSprites(string file, bool packed, params SpriteInstanceDefinition[] sprites)
    {
        var definition = new SpriteDefinition
        {
            Sprites = sprites.ToList(),
            TilesheetSources = [SpriteTilesheetSourceDefinition.Loose("sheet", "missing.gts")]
        };
        if (packed) Pack(file, AssetTypes.SpriteDefinition, "sprites", SpriteDefinitionSerializer.ToJson(definition));
        else SpriteDefinitionSerializer.Save(Path.Combine(_directory, file), definition);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SpritesResolveFramesAndOnlyMaterializeViewedScene(bool packedSprites, bool packedSheet)
    {
        WriteContent();
        var definition = SceneDefinitionSerializer.Load(ScenePath);
        definition.TilesheetSources.Clear();
        definition.AnimationSources.Clear();
        foreach (var layer in definition.Layers) layer.Tiles.Clear();
        SceneDefinitionSerializer.Save(ScenePath, definition);
        if (packedSheet) PackSheet();
        var current = Sprite("Player");
        var other = Sprite("Other", "other-scene");
        other.Frame!.Tilesheet = "unavailable-other-scene-sheet";
        WriteSprites(packedSprites ? "sprites.gaf" : "sprites.gspr", packedSprites, current, other);
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        var sprite = Assert.Single(SpriteManager.Instance.AllSprites);
        Assert.Equal(current.Id, sprite.Id);
        Assert.Equal(current.Position, sprite.SceneLayerCoordinates);
        Assert.Same(scene[0], sprite.SceneLayer);
        Assert.Equal("sheet", sprite.CurrentFrame.Tilesheet.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LooseSpriteOverridesPackedByIdOrNickname(bool byNickname)
    {
        WriteContent();
        var loose = Sprite("Player");
        var packed = Sprite(byNickname ? "Player" : "PackedName");
        if (!byNickname) packed.Id = loose.Id;
        packed.Position = new(99, 99);
        WriteSprites("z.gspr", false, loose);
        WriteSprites("a.gaf", true, packed);
        WriteSprites("b.gaf", true, packed);
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.Equal(loose.Position, Assert.Single(SpriteManager.Instance.AllSprites).SceneLayerCoordinates);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DuplicateSpritesReportSources(bool packed, bool byNickname)
    {
        WriteContent();
        var first = Sprite("Player");
        var second = Sprite(byNickname ? "Player" : "Different");
        if (!byNickname) second.Id = first.Id;
        var extension = packed ? ".gaf" : ".gspr";
        WriteSprites("z" + extension, packed, first);
        WriteSprites("a" + extension, packed, second);
        var error = Assert.Throws<InvalidDataException>(() => new ViewerSceneLoader().Load(ScenePath));
        Assert.Contains($"multiple {(packed ? "packed" : "loose")} GSPR", error.Message);
        Assert.Contains("a" + extension, error.Message);
        Assert.Contains("z" + extension, error.Message);
        Assert.Empty(SpriteManager.Instance.AllSprites);
    }

    [Fact]
    public void DistinctAndAnonymousSpritesSurviveSelectionWithoutMutatingDocuments()
    {
        WriteContent();
        var anonymous = Sprite(null);
        anonymous.Id = Guid.Empty;
        WriteSprites("a.gspr", false, Sprite("First"), anonymous);
        WriteSprites("b.gaf", true, Sprite("Second"), anonymous);
        var original = File.ReadAllText(Path.Combine(_directory, "a.gspr"));
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.Equal(4, SpriteManager.Instance.AllSprites.Count);
        Assert.Equal(original, File.ReadAllText(Path.Combine(_directory, "a.gspr")));
    }

    [Fact]
    public void InvalidCurrentSceneLayerFailsAndRollsBackSprites()
    {
        WriteContent();
        var invalid = Sprite("Invalid");
        invalid.SceneLayerId = "missing-layer";
        WriteSprites("sprites.gspr", false, Sprite("Valid"), invalid);
        var error = Assert.Throws<InvalidDataException>(() => new ViewerSceneLoader().Load(ScenePath));
        Assert.Contains("missing-layer", error.Message);
        Assert.Empty(SpriteManager.Instance.AllSprites);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CatalogSelectionAndDiagnosticsIgnoreCreationOrder(bool reversed)
    {
        WriteContent();
        var json = File.ReadAllText(Path.Combine(_directory, "sheet.gts"));
        foreach (var name in reversed ? new[] { "z", "a" } : new[] { "a", "z" })
            Pack(name + ".gaf", AssetTypes.TilesheetDefinition, name + "-entry", json);
        var loosePath = Path.Combine(_directory, "sheet.gts");
        var filesBefore = Directory.GetFiles(_directory).Order(StringComparer.Ordinal).ToArray();
        var catalog = new ViewerContentCatalog(_directory, path => AssetsFile.LoadOrCreate(path));
        Assert.Equal(loosePath, catalog.Tilesheet("sheet")!.Path);
        Assert.Equal(filesBefore, Directory.GetFiles(_directory).Order(StringComparer.Ordinal).ToArray());
        AssetsFile.ClearAll();
        File.Delete(loosePath);
        catalog = new ViewerContentCatalog(_directory, path => AssetsFile.LoadOrCreate(path));
        var error = Assert.Throws<InvalidDataException>(() => catalog.Tilesheet("sheet"));
        Assert.Equal($"Multiple packed GTS definitions claim 'sheet':\n{Path.Combine(_directory, "a.gaf")} :: a-entry\n{Path.Combine(_directory, "z.gaf")} :: z-entry", error.Message);
    }

    [Fact]
    public void ChildDirectoryDefinitionsAreNotDiscovered()
    {
        WriteContent();
        var child = Path.Combine(_directory, "child");
        Directory.CreateDirectory(child);
        File.Copy(Path.Combine(_directory, "sheet.gts"), Path.Combine(child, "duplicate.gts"));
        File.WriteAllText(Path.Combine(child, "broken.gspr"), "invalid json");
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.NotNull(TilesheetRegistry.Instance.GetOrNull("sheet"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SceneExplicitSheetFallbackWorksOutsideContentRoot(bool packed)
    {
        WriteContent();
        var child = Path.Combine(_directory, "external");
        Directory.CreateDirectory(child);
        if (packed)
        {
            PackSheet();
            File.Move(Path.Combine(_directory, "sheets.gaf"), Path.Combine(child, "sheets.gaf"));
        }
        else File.Move(Path.Combine(_directory, "sheet.gts"), Path.Combine(child, "sheet.gts"));
        File.Move(Path.Combine(_directory, "image.png"), Path.Combine(child, "image.png"));
        var definition = SceneDefinitionSerializer.Load(ScenePath);
        definition.TilesheetSources = [packed
            ? SceneTilesheetSourceDefinition.Packed("sheet", "external/sheets.gaf", "arbitrary-sheet-entry")
            : SceneTilesheetSourceDefinition.Loose("sheet", "external/sheet.gts")];
        definition.AnimationSources.Clear();
        foreach (var layer in definition.Layers) layer.Tiles.Clear();
        SceneDefinitionSerializer.Save(ScenePath, definition);
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.NotNull(TilesheetRegistry.Instance.GetOrNull("sheet"));
    }

    [Fact]
    public void SpriteExplicitFallbackIsRelativeAndOtherSceneSourcesAreNotOpened()
    {
        WriteContent();
        var child = Path.Combine(_directory, "external");
        Directory.CreateDirectory(child);
        File.Move(Path.Combine(_directory, "sheet.gts"), Path.Combine(child, "sheet.gts"));
        File.Move(Path.Combine(_directory, "image.png"), Path.Combine(child, "image.png"));
        var definition = SceneDefinitionSerializer.Load(ScenePath);
        definition.TilesheetSources.Clear();
        definition.AnimationSources.Clear();
        foreach (var layer in definition.Layers) layer.Tiles.Clear();
        SceneDefinitionSerializer.Save(ScenePath, definition);
        var sprites = new SpriteDefinition
        {
            Sprites = [Sprite("Player"), Sprite("Other", "unloaded")],
            TilesheetSources = [SpriteTilesheetSourceDefinition.Loose("sheet", "external/sheet.gts")],
            SceneSources = [SpriteSceneSourceDefinition.Loose("unloaded", "missing.gscn")]
        };
        SpriteDefinitionSerializer.Save(Path.Combine(_directory, "sprites.gspr"), sprites);
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.Equal("Player", Assert.Single(SpriteManager.Instance.AllSprites).Nickname);
    }

    [Fact]
    public void MissingFallbackArchiveIsNotCreated()
    {
        WriteContent();
        File.Delete(Path.Combine(_directory, "sheet.gts"));
        var definition = SceneDefinitionSerializer.Load(ScenePath);
        definition.TilesheetSources = [SceneTilesheetSourceDefinition.Packed("sheet", "missing.gaf", "sheet")];
        SceneDefinitionSerializer.Save(ScenePath, definition);
        var error = Assert.Throws<InvalidDataException>(() => new ViewerSceneLoader().Load(ScenePath));
        Assert.Contains("missing.gaf", error.Message);
        Assert.False(File.Exists(Path.Combine(_directory, "missing.gaf")));
    }

    [Fact]
    public void UnreadableAdjacentArchiveIdentifiesItsPath()
    {
        WriteContent();
        var path = Path.Combine(_directory, "broken.gaf");
        File.WriteAllText(path, "not an archive");
        var error = Assert.Throws<InvalidDataException>(() => new ViewerSceneLoader().Load(ScenePath));
        Assert.Contains(path, error.Message);
    }

    [Fact]
    public void GaniTilesheetAmbiguityUsesSharedDiagnostics()
    {
        WriteContent();
        var definition = SceneDefinitionSerializer.Load(ScenePath);
        definition.TilesheetSources.Clear();
        SceneDefinitionSerializer.Save(ScenePath, definition);
        PackSheet(false, "a.gaf");
        PackSheet(true, "z.gaf");
        var error = Assert.Throws<InvalidDataException>(() => new ViewerSceneLoader().Load(ScenePath));
        Assert.Contains("Animation source 'walk'", error.Message);
        Assert.Contains("Multiple packed GTS", error.Message);
        Assert.Contains("a.gaf :: arbitrary-sheet-entry", error.Message);
        Assert.Contains("z.gaf :: arbitrary-sheet-entry", error.Message);
    }
}
