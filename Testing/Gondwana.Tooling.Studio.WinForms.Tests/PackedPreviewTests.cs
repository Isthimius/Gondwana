using System.Collections;
using System.Reflection;
using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Animations.Editing;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Sprites.Editing;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.Tilesheets.Editing;
using Gondwana.Tooling.Tilesheets.Sources;
using Gondwana.Tooling.Tilesheets.WinForms;
using SkiaSharp;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void AuthoringResolverDecodesLooseAndPackedImages(bool packedDefinition, bool packedImage) => RunSta(root =>
    {
        var archive = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        var sheet = PreviewSheet("terrain", packedImage ? "assets.gaf" : null);
        var loose = Path.Combine(root, "world.gts");
        TilesheetDefinitionSerializer.Save(loose, sheet);
        using var packages = new AssetPackageCatalog();
        var resolver = new AuthoringResourceResolver(packages);
        var resource = packedDefinition
            ? new AuthoringResource(archive, AssetTypes.TilesheetDefinition, "definitions/world.gts")
            : new AuthoringResource(loose, AssetTypes.TilesheetDefinition);
        var loaded = resolver.LoadTilesheet(resource, "terrain");
        using var pixels = resolver.DecodeImage(loaded, resource.BaseDirectory, resource.ArchivePath);
        Assert.Equal(new SKColor(50, 205, 50), pixels.GetPixel(8, 8));
        Assert.Equal("terrain", loaded.Name);
        // Neither the cached package nor decoded bitmap retains a file handle.
        using var exclusive = File.Open(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    });

    [Fact]
    public void ExplicitPackedIdentitySelectsArchiveEntryAndLogicalMemberWithoutFallback() => RunSta(root =>
    {
        var first = CreatePreviewArchive(root, "first.gaf", Color.LimeGreen);
        var second = CreatePreviewArchive(root, "second.gaf", Color.Blue);
        WritePreviewImage(Path.Combine(root, "atlas.png"), Color.Red);
        TilesheetDefinitionSerializer.Save(Path.Combine(root, "world.gts"), PreviewSheet("terrain"));
        using var packages = new AssetPackageCatalog();
        var resolver = new AuthoringResourceResolver(packages);
        foreach (var (archive, color) in new[] { (first, SKColors.LimeGreen), (second, SKColors.Blue) })
        {
            var resource = new AuthoringResource(archive, AssetTypes.TilesheetDefinition, "definitions/world.gts");
            var sheet = resolver.LoadTilesheet(resource, "terrain");
            sheet.Image!.FilePath = "atlas.png";
            using var pixels = resolver.DecodeImage(sheet, root, archive);
            Assert.Equal(color, pixels.GetPixel(8, 8));
            Assert.Equal("atlas.png", sheet.Image.FilePath);
            Assert.Equal("images/atlas.png", sheet.Image.AssetEntryName);
            Assert.Throws<InvalidDataException>(() => resolver.LoadTilesheet(resource, "missing"));
            Assert.Throws<InvalidDataException>(() => resolver.LoadTilesheet(resource with { EntryName = "world.gts" }, "terrain"));
            Assert.Throws<InvalidDataException>(() => resolver.LoadTilesheet(resource with { AssetType = AssetTypes.Image }, "terrain"));
            Assert.Throws<InvalidDataException>(() => packages.OpenAsset(archive, AssetTypes.Image, "definitions/world.gts"));
        }
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void NestedAnimationDependenciesUseThePhysicalDocumentDirectory(bool packedAnimation, bool absoluteReference) => RunSta(root =>
    {
        var content = Path.Combine(root, "content");
        Directory.CreateDirectory(content);
        var archive = CreatePreviewArchive(content, "assets.gaf", Color.LimeGreen);
        var path = Path.Combine(root, "loose.gani");
        AnimationDefinitionSerializer.Save(path, PreviewAnimation(absoluteReference ? archive : Path.Combine("content", "assets.gaf")));
        using var packages = new AssetPackageCatalog();
        var resolver = new AuthoringResourceResolver(packages);
        var resource = packedAnimation ? new AuthoringResource(archive, AssetTypes.AnimationDefinition, "definitions/water.gani")
            : new AuthoringResource(path, AssetTypes.AnimationDefinition);
        var animation = resolver.LoadAnimation(resource, "water.flow");
        var source = AuthoringResourceResolver.TilesheetSource(Assert.Single(animation.TilesheetSources), resource.BaseDirectory);
        Assert.Equal(archive, source.ArchivePath);
        Assert.Equal("definitions/world.gts", source.EntryName);
        Assert.Equal(AssetTypes.TilesheetDefinition, source.AssetType);
        var sheet = resolver.LoadTilesheet(source, "terrain");
        using var pixels = resolver.DecodeImage(sheet, source.BaseDirectory, source.ArchivePath);
        Assert.Equal(SKColors.LimeGreen, pixels.GetPixel(8, 8));
    });

    [Fact]
    public void CatalogReloadsChangedImagesAndDoesNotRepeatFailedPasswordRequests() => RunSta(root =>
    {
        var archive = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        using var packages = new AssetPackageCatalog();
        var resolver = new AuthoringResourceResolver(packages);
        var sheet = PreviewSheet("terrain", "assets.gaf");
        using (var image = resolver.DecodeImage(sheet, root))
            Assert.Equal(SKColors.LimeGreen, image.GetPixel(8, 8));
        ReplacePreviewImage(root, archive, Color.Blue);
        File.SetLastWriteTimeUtc(archive, DateTime.UtcNow.AddMinutes(1));
        using (var image = resolver.DecodeImage(sheet, root))
            Assert.Equal(SKColors.Blue, image.GetPixel(8, 8));

        var locked = Path.Combine(root, "locked.gaf");
        using (var package = AssetsFile.LoadOrCreate(locked, "secret", encrypt: true, register: false))
        {
            package.Add(AssetTypes.Image, Path.Combine(root, "atlas.png"), "images/atlas.png");
            package.Save();
        }
        var prompts = 0;
        packages.PasswordProvider = _ => { prompts++; return null; };
        for (var i = 0; i < 2; i++)
            Assert.Throws<InvalidDataException>(() => packages.OpenAsset(locked, AssetTypes.Image, "images/atlas.png"));
        Assert.Equal(1, prompts);
        packages.Invalidate(locked);
        packages.PasswordProvider = _ => "secret";
        using var accessible = packages.OpenAsset(locked, AssetTypes.Image, "images/atlas.png");
        using var decoded = SKBitmap.Decode(accessible);
        Assert.Equal(SKColors.Blue, decoded.GetPixel(8, 8));
    });

    [Fact]
    public void MissingCorruptAndUnavailableDependenciesReportTheirSource() => RunSta(root =>
    {
        using var packages = new AssetPackageCatalog();
        var resolver = new AuthoringResourceResolver(packages);
        var archive = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        var missing = PreviewSheet("terrain", "absent.gaf");
        Assert.Contains("absent.gaf", Assert.Throws<FileNotFoundException>(() => resolver.DecodeImage(missing, root)).Message);
        using (var exclusive = File.Open(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Contains(archive, Assert.Throws<InvalidDataException>(() => packages.OpenAsset(archive, AssetTypes.Image, "images/atlas.png")).Message);
        packages.Invalidate(archive);
        File.WriteAllText(Path.Combine(root, "bad.gaf"), "not an archive");
        Assert.Contains("bad.gaf", Assert.Throws<InvalidDataException>(() => packages.OpenAsset(Path.Combine(root, "bad.gaf"), AssetTypes.Image, "image")).Message);
        var invalid = Path.Combine(root, "invalid.gts");
        File.WriteAllText(invalid, "invalid definition");
        using (var package = AssetsFile.LoadOrCreate(archive, null, false, register: false))
        {
            package.Add(AssetTypes.TilesheetDefinition, invalid, "invalid.gts");
            package.Add(AssetTypes.Image, invalid, "invalid.png");
            package.Save();
        }
        packages.Invalidate(archive);
        Assert.Throws<InvalidDataException>(() => resolver.LoadTilesheet(new(archive, AssetTypes.TilesheetDefinition, "invalid.gts"), "terrain"));
        var badImage = PreviewSheet("terrain", "assets.gaf");
        badImage.Image!.AssetEntryName = "invalid.png";
        Assert.Contains("invalid.png", Assert.Throws<InvalidDataException>(() => resolver.DecodeImage(badImage, root)).Message);
    });

    [Fact]
    public void AllEditorsPreviewPackedDependenciesAndReloadWithoutEditingDocuments() => RunSta(root =>
    {
        var archive = CreatePreviewArchive(root, "assets.gaf", Color.LimeGreen);
        var gts = Path.Combine(root, "loose.gts");
        TilesheetDefinitionSerializer.Save(gts, PreviewSheet("terrain", "assets.gaf"));
        var gani = Path.Combine(root, "loose.gani");
        AnimationDefinitionSerializer.Save(gani, PreviewAnimation("assets.gaf"));
        var scene = PreviewScene("assets.gaf");
        var gscn = Path.Combine(root, "loose.gscn");
        SceneDefinitionSerializer.Save(gscn, scene);
        var gspr = Path.Combine(root, "loose.gspr");
        SpriteDefinitionSerializer.Save(gspr, new SpriteDefinition()
        {
            Key = "actors",
            TilesheetSources = [SpriteTilesheetSourceDefinition.Packed("terrain", "assets.gaf", "definitions/world.gts")],
            SceneSources = [SpriteSceneSourceDefinition.Packed("island", "assets.gaf", "definitions/island.gscn")],
            AnimationSources = [SpriteAnimationSourceDefinition.Packed("water.flow", "assets.gaf", "definitions/water.gani")],
            Sprites = [new() { Nickname = "hero", SceneId = "island", SceneLayerId = "ground",
                Frame = new() { Tilesheet = "terrain", RegionName = "default" },
                AnimationKey = "water.flow", StartAnimation = true }]
        });
        // Packed dependencies have no loose copies. A same-name image is deliberately wrong.
        File.Delete(Path.Combine(root, "world.gts"));
        File.Delete(Path.Combine(root, "water.gani"));
        File.Delete(Path.Combine(root, "island.gscn"));
        WritePreviewImage(Path.Combine(root, "atlas.png"), Color.Red);
        var snapshots = new[] { gts, gani, gscn, gspr, archive }.ToDictionary(path => path, File.ReadAllBytes);
        var runtimePackages = AssetsFile.AllAssetsFiles.ToArray();
        var runtimeTilesheets = Gondwana.Drawing.Tilesheets.TilesheetRegistry.Instance.Names.ToArray();
        using var packages = new AssetPackageCatalog();
        using var tilesheets = new TilesheetEditorControl(TilesheetDocument.Open(gts), packages);
        using var animations = new AnimationEditorControl(AnimationDocument.Open(gani), packages);
        using var scenes = new SceneEditorControl(SceneDocument.Open(gscn), packages);
        using var sprites = new SpriteEditorControl(SpriteDocument.Open(gspr), packages);
        AssertPreviewColor(Property<Bitmap>(Private(tilesheets, "_viewport"), "Image"), Color.LimeGreen);
        AssertSourceImages(Private(animations, "_sources"), Color.LimeGreen);
        var loadedSource = Assert.Single(((IEnumerable)Private(animations, "_sources")).Cast<object>());
        Assert.Equal(new AuthoringResource(archive, AssetTypes.TilesheetDefinition, "definitions/world.gts"),
            Property<AuthoringResource>(loadedSource, "Resource"));
        AssertSourceImages(Private(scenes, "_tilesheetSources"), Color.LimeGreen);
        var animationSource = Assert.Single(((IEnumerable)Private(scenes, "_animationSources")).Cast<object>());
        AssertSourceImages(Property<object>(animationSource, "Tilesheets"), Color.LimeGreen);
        AssertSourceImages(Private(sprites, "_sources"), Color.LimeGreen);
        var canvas = (Control)Private(Private(sprites, "_preview"), "_canvas");
        AssertPreviewColor(Property<Bitmap>(Private(canvas, "_source"), "Image"), Color.LimeGreen);
        Assert.NotNull(Private(canvas, "_projection"));
        ((TreeView)Private(animations, "_sourceTree")).ExpandAll();
        ((TreeView)Private(scenes, "_sourceTree")).ExpandAll();
        Assert.NotEmpty(((ImageList)Private(animations, "_sourceFrameImages")).Images);
        Assert.NotEmpty(((ImageList)Private(scenes, "_sourceFrameImages")).Images);
        AssertRenderedColor((Control)Private(scenes, "_preview"), Color.LimeGreen);
        AssertRenderedColor(canvas, Color.LimeGreen);
        Assert.False(tilesheets.Document.IsDirty);
        Assert.False(animations.Document.IsDirty);
        Assert.False(scenes.Document.IsDirty);
        Assert.False(sprites.Document.IsDirty);
        Assert.Equal(runtimePackages, AssetsFile.AllAssetsFiles);
        Assert.Equal(runtimeTilesheets, Gondwana.Drawing.Tilesheets.TilesheetRegistry.Instance.Names);
        foreach (var (path, contents) in snapshots) Assert.Equal(contents, File.ReadAllBytes(path));

        ReplacePreviewImage(root, archive, Color.Blue);
        animations.ReloadSources();
        scenes.ReloadSources();
        sprites.ReloadSources();
        AssertSourceImages(Private(animations, "_sources"), Color.Blue);
        AssertSourceImages(Private(scenes, "_tilesheetSources"), Color.Blue);
        AssertSourceImages(Private(sprites, "_sources"), Color.Blue);
        AssertRenderedColor((Control)Private(scenes, "_preview"), Color.Blue);
        AssertRenderedColor(canvas, Color.Blue);
        Assert.False(animations.Document.IsDirty);
        Assert.False(scenes.Document.IsDirty);
        Assert.False(sprites.Document.IsDirty);
        foreach (var path in new[] { gts, gani, gscn, gspr }) Assert.Equal(snapshots[path], File.ReadAllBytes(path));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnimationPreviewKeepsItsExplicitSourceWhenSceneHasSameLogicalTilesheet(bool missing) => RunSta(root =>
    {
        var archive = CreatePreviewArchive(root, "animation.gaf", Color.LimeGreen);
        CreatePreviewArchive(root, "scene.gaf", Color.Blue);
        if (missing)
        {
            using var package = AssetsFile.LoadOrCreate(archive, null, false, register: false);
            var animation = PreviewAnimation("missing.gaf");
            var path = Path.Combine(root, "water.gani");
            AnimationDefinitionSerializer.Save(path, animation);
            package.Add(AssetTypes.AnimationDefinition, path, "definitions/water.gani");
            package.Save();
        }
        var scene = PreviewScene("scene.gaf");
        scene.AnimationSources = [SceneAnimationSourceDefinition.Packed("water.flow", "animation.gaf", "definitions/water.gani")];
        var scenePath = Path.Combine(root, "scene.gscn");
        SceneDefinitionSerializer.Save(scenePath, scene);
        using var editor = new SceneEditorControl(SceneDocument.Open(scenePath));
        var preview = Private(editor, "_preview");
        var resolve = preview.GetType().GetMethod("ResolveTilesheetSource", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = resolve.Invoke(preview, [new SceneFrameDefinition { Tilesheet = "terrain", RegionName = "default" }, "water.flow"]);
        if (missing)
        {
            Assert.Null(result);
            var animation = Assert.Single(((IEnumerable)Private(editor, "_animationSources")).Cast<object>());
            Assert.Contains(((IEnumerable)Property<object>(animation, "Diagnostics")).Cast<string>(), message => message.Contains("missing.gaf"));
        }
        else
        {
            AssertPreviewColor(Property<Bitmap>(result!, "Image"), Color.LimeGreen);
            AssertRenderedColor((Control)preview, Color.LimeGreen);
        }
        AssertRenderedColor((Control)preview, Color.Blue);
        Assert.False(editor.Document.IsDirty);
    });

    private static object Private(object instance, string name) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static T Property<T>(object instance, string name) =>
        (T)instance.GetType().GetProperty(name)!.GetValue(instance)!;

    private static void AssertSourceImages(object sources, Color color)
    {
        var entries = ((IEnumerable)sources).Cast<object>().ToArray();
        Assert.NotEmpty(entries);
        foreach (var entry in entries) AssertPreviewColor(Property<Bitmap>(entry, "Image"), color);
    }

    private static void AssertPreviewColor(Bitmap image, Color expected) =>
        Assert.Equal(expected.ToArgb(), image.GetPixel(8, 8).ToArgb());

    private static void AssertRenderedColor(Control control, Color expected)
    {
        var parent = control.Parent!;
        var dock = control.Dock;
        using var host = new Form { ClientSize = new Size(300, 240), ShowInTaskbar = false };
        try
        {
            host.Controls.Add(control);
            control.Dock = DockStyle.Fill;
            host.Show();
            Application.DoEvents();
            using var rendered = new Bitmap(control.Width, control.Height);
            control.DrawToBitmap(rendered, control.ClientRectangle);
            Assert.True(Enumerable.Range(0, rendered.Width).Any(x =>
                Enumerable.Range(0, rendered.Height).Any(y => rendered.GetPixel(x, y).ToArgb() == expected.ToArgb())),
                $"Preview did not render {expected} pixels.");
        }
        finally
        {
            parent.Controls.Add(control);
            control.Dock = dock;
        }
    }

    private static TilesheetDefinition PreviewSheet(string name, string? archive = null) => new()
    {
        Name = name,
        Image = archive is null ? new() { FilePath = "atlas.png" }
            : new() { AssetsFilePath = archive, AssetEntryName = "images/atlas.png" },
        Regions = [new() { Name = "default", Area = new Rectangle(0, 0, 16, 16), TileSize = new Size(16, 16) }]
    };

    private static AnimationDefinition PreviewAnimation(string archive) => new()
    {
        Key = "water.flow",
        ThrottleTime = .1,
        TilesheetSources = [AnimationTilesheetSourceDefinition.Packed("terrain", archive, "definitions/world.gts")],
        Frames = [new() { Tilesheet = "terrain", RegionName = "default" }]
    };

    private static SceneDefinition PreviewScene(string archive) => new()
    {
        ID = "island",
        TilesheetSources = [SceneTilesheetSourceDefinition.Packed("terrain", archive, "definitions/world.gts")],
        AnimationSources = [SceneAnimationSourceDefinition.Packed("water.flow", archive, "definitions/water.gani")],
        Layers = [new() { ID = "ground", Columns = 2, Rows = 1, TileWidth = 16, TileHeight = 16,
            Tiles = [new() { Frame = new() { Tilesheet = "terrain", RegionName = "default" } },
                new() { X = 1, AnimationKey = "water.flow", StartAnimation = true }] }]
    };

    private static string CreatePreviewArchive(string root, string name, Color color)
    {
        WritePreviewImage(Path.Combine(root, "atlas.png"), color);
        var sheet = PreviewSheet("terrain");
        // A packed GTS may use its own archive for images without serializing that path.
        sheet.Image = new() { AssetEntryName = "images/atlas.png" };
        var gts = Path.Combine(root, "world.gts");
        TilesheetDefinitionSerializer.SaveAll(gts, [PreviewSheet("unrelated"), sheet]);
        var gani = Path.Combine(root, "water.gani");
        AnimationDefinitionSerializer.SaveAll(gani, [new() { Key = "unrelated" }, PreviewAnimation(name)]);
        var gscn = Path.Combine(root, "island.gscn");
        SceneDefinitionSerializer.SaveAll(gscn, [new() { ID = "unrelated" }, PreviewScene(name)]);
        var archive = Path.Combine(root, name);
        using var package = AssetsFile.LoadOrCreate(archive, null, false, register: false);
        package.Add(AssetTypes.Image, Path.Combine(root, "atlas.png"), "images/atlas.png");
        package.Add(AssetTypes.TilesheetDefinition, gts, "definitions/world.gts");
        package.Add(AssetTypes.AnimationDefinition, gani, "definitions/water.gani");
        package.Add(AssetTypes.SceneDefinition, gscn, "definitions/island.gscn");
        package.Save();
        return archive;
    }

    private static void WritePreviewImage(string path, Color color)
    {
        using var image = new Bitmap(16, 16);
        using (var graphics = Graphics.FromImage(image)) graphics.Clear(color);
        image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void ReplacePreviewImage(string root, string archive, Color color)
    {
        WritePreviewImage(Path.Combine(root, "atlas.png"), color);
        using var package = AssetsFile.LoadOrCreate(archive, null, false, register: false);
        package.Add(AssetTypes.Image, Path.Combine(root, "atlas.png"), "images/atlas.png");
        package.Save();
    }
}
