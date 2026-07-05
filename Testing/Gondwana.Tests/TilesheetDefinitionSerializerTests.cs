using Gondwana.Drawing.Tilesheets.GTS;

namespace Gondwana.Tests;

public sealed class TilesheetDefinitionSerializerTests : IDisposable
{
    private readonly string _tempDir;

    public TilesheetDefinitionSerializerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"GondwanaTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // -----------------------------------------------------------------------
    // Load(Stream) — source is always None
    // -----------------------------------------------------------------------

    [Fact]
    public void LoadStream_SetsSourceToNone()
    {
        var definition = new TilesheetDefinition { Name = "Sheet" };
        using var stream = ToStream(definition);

        var loaded = TilesheetDefinitionSerializer.Load(stream);

        Assert.Equal(TilesheetDefinitionSourceKind.None, loaded.Source.Kind);
    }

    [Fact]
    public void LoadStream_PreservesDefinitionContent()
    {
        var definition = new TilesheetDefinition
        {
            Name = "HeroSheet",
            Image = new TilesheetImageDefinition { FilePath = "hero.png" },
            PremultiplyAlpha = true
        };
        using var stream = ToStream(definition);

        var loaded = TilesheetDefinitionSerializer.Load(stream);

        Assert.Equal("HeroSheet", loaded.Name);
        Assert.Equal("hero.png", loaded.Image.FilePath);
        Assert.True(loaded.PremultiplyAlpha);
        Assert.Equal(TilesheetDefinitionSourceKind.None, loaded.Source.Kind);
    }

    // -----------------------------------------------------------------------
    // Load(string) — source is stamped as LooseDefinitionFile
    // -----------------------------------------------------------------------

    [Fact]
    public void LoadFile_SetsSourceToLooseDefinitionFile()
    {
        var path = WriteGtsFile("test.gts", new TilesheetDefinition { Name = "Sheet" });

        var definition = TilesheetDefinitionSerializer.Load(path);

        Assert.Equal(TilesheetDefinitionSourceKind.LooseDefinitionFile, definition.Source.Kind);
        Assert.Equal(Path.GetFullPath(path), definition.Source.GtsFilePath);
    }

    [Fact]
    public void LoadFile_WhenJsonContainsSourceGenerated_PreservesGenerated()
    {
        // If the .gts JSON already bakes in a Generated source, Load should not overwrite it.
        var definition = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.Generated()
        };
        var path = WriteGtsFile("pregenerated.gts", definition);

        var loaded = TilesheetDefinitionSerializer.Load(path);

        Assert.Equal(TilesheetDefinitionSourceKind.Generated, loaded.Source.Kind);
    }

    [Fact]
    public void LoadFile_WhenJsonContainsSourceNone_StampsLooseDefinitionFile()
    {
        // A .gts with Source=None should be stamped with the file path on load.
        var definition = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.None()
        };
        var path = WriteGtsFile("none-source.gts", definition);

        var loaded = TilesheetDefinitionSerializer.Load(path);

        Assert.Equal(TilesheetDefinitionSourceKind.LooseDefinitionFile, loaded.Source.Kind);
        Assert.Equal(Path.GetFullPath(path), loaded.Source.GtsFilePath);
    }

    // -----------------------------------------------------------------------
    // Save(string, TilesheetDefinition) — source stamping and preservation
    // -----------------------------------------------------------------------

    [Fact]
    public void SaveDefinition_WithSourceNone_StampsLooseDefinitionFileInSavedJson()
    {
        var definition = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.None()
        };
        var path = GtsPath("saved-none.gts");

        TilesheetDefinitionSerializer.Save(path, definition);

        var loaded = TilesheetDefinitionSerializer.Load(path);
        Assert.Equal(TilesheetDefinitionSourceKind.LooseDefinitionFile, loaded.Source.Kind);
        Assert.Equal(Path.GetFullPath(path), loaded.Source.GtsFilePath);
    }

    [Fact]
    public void SaveDefinition_WithSourceLooseDefinitionFile_PreservesSourceInSavedJson()
    {
        var original = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.LooseDefinitionFile("/original/location.gts")
        };
        var path = GtsPath("saved-loose.gts");

        TilesheetDefinitionSerializer.Save(path, original);

        var loaded = TilesheetDefinitionSerializer.Load(path);
        // The serialized JSON preserves the original Source; ApplyDefaultSource won't overwrite.
        Assert.Equal(TilesheetDefinitionSourceKind.LooseDefinitionFile, loaded.Source.Kind);
        Assert.Equal("/original/location.gts", loaded.Source.GtsFilePath);
    }

    [Fact]
    public void SaveDefinition_WithSourceGenerated_PreservesGeneratedInSavedJson()
    {
        var original = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.Generated()
        };
        var path = GtsPath("saved-generated.gts");

        TilesheetDefinitionSerializer.Save(path, original);

        var loaded = TilesheetDefinitionSerializer.Load(path);
        Assert.Equal(TilesheetDefinitionSourceKind.Generated, loaded.Source.Kind);
    }

    [Fact]
    public void SaveDefinition_WithSourcePackedDefinitionFile_PreservesPackedInSavedJson()
    {
        var original = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.PackedDefinitionFile("/data/assets.gaf", "sheet.gts")
        };
        var path = GtsPath("saved-packed.gts");

        TilesheetDefinitionSerializer.Save(path, original);

        var loaded = TilesheetDefinitionSerializer.Load(path);
        Assert.Equal(TilesheetDefinitionSourceKind.PackedDefinitionFile, loaded.Source.Kind);
        Assert.Equal("/data/assets.gaf", loaded.Source.AssetsFilePath);
        Assert.Equal("sheet.gts", loaded.Source.AssetEntryName);
    }

    [Fact]
    public void SaveDefinition_CreatesFileOnDisk()
    {
        var definition = new TilesheetDefinition { Name = "Sheet" };
        var path = GtsPath("created.gts");

        TilesheetDefinitionSerializer.Save(path, definition);

        Assert.True(File.Exists(path));
    }

    // -----------------------------------------------------------------------
    // Save(string, TilesheetDefinition) — does not mutate the passed-in Source
    // -----------------------------------------------------------------------

    [Fact]
    public void SaveDefinition_WithSourceNone_DoesNotMutateOriginalDefinitionSource()
    {
        var definition = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.None()
        };
        var path = GtsPath("no-mutate.gts");

        TilesheetDefinitionSerializer.Save(path, definition);

        // The in-memory object should still report None; Save works on a clone.
        Assert.Equal(TilesheetDefinitionSourceKind.None, definition.Source.Kind);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private string GtsPath(string fileName) => Path.Combine(_tempDir, fileName);

    private static MemoryStream ToStream(TilesheetDefinition definition)
    {
        var json = TilesheetDefinitionSerializer.ToJson(definition);
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
    }

    private string WriteGtsFile(string fileName, TilesheetDefinition definition)
    {
        var path = GtsPath(fileName);
        var json = TilesheetDefinitionSerializer.ToJson(definition);
        File.WriteAllText(path, json);
        return path;
    }
}
