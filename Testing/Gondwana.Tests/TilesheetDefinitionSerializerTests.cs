using Gondwana.Drawing.Tilesheets.GTS;

namespace Gondwana.Tests;

/// <summary>
/// Contains unit tests for the <see cref="TilesheetDefinitionSerializer"/> class.
/// </summary>
public sealed class TilesheetDefinitionSerializerTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>
    /// Initializes the temporary test directory used by file-based serializer tests.
    /// </summary>
    public TilesheetDefinitionSerializerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"GondwanaTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Releases resources used by this test fixture.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // -----------------------------------------------------------------------
    // Load(Stream) — preserves Source from JSON (defaults to None when not set)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that loading from a stream defaults the source kind to None when it is not set.
    /// </summary>
    [Fact]
    public void LoadStream_SetsSourceToNone()
    {
        var definition = new TilesheetDefinition { Name = "Sheet" };
        using var stream = ToStream(definition);

        var loaded = TilesheetDefinitionSerializer.Load(stream);

        Assert.Equal(TilesheetDefinitionSourceKind.None, loaded.Source.Kind);
    }

    /// <summary>
    /// Verifies that loading from a stream preserves the serialized definition content.
    /// </summary>
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

    /// <summary>
    /// Verifies that loading from a file stamps the source as a loose definition file.
    /// </summary>
    [Fact]
    public void LoadFile_SetsSourceToLooseDefinitionFile()
    {
        var path = WriteGtsFile("test.gts", new TilesheetDefinition { Name = "Sheet" });

        var definition = TilesheetDefinitionSerializer.Load(path);

        Assert.Equal(TilesheetDefinitionSourceKind.LooseDefinitionFile, definition.Source.Kind);
        Assert.Equal(Path.GetFullPath(path), definition.Source.GtsFilePath);
    }

    /// <summary>
    /// Verifies that loading a file preserves a generated source already stored in the JSON.
    /// </summary>
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

    /// <summary>
    /// Verifies that loading a file stamps a None source as a loose definition file.
    /// </summary>
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

    /// <summary>
    /// Verifies that saving a definition with a None source writes a loose definition file source to disk.
    /// </summary>
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

    /// <summary>
    /// Verifies that saving a definition preserves an existing loose definition file source in the serialized JSON.
    /// </summary>
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

    /// <summary>
    /// Verifies that saving a definition preserves a generated source in the serialized JSON.
    /// </summary>
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

    /// <summary>
    /// Verifies that saving a definition preserves a packed definition file source in the serialized JSON.
    /// </summary>
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

    /// <summary>
    /// Verifies that saving a definition creates the target file on disk.
    /// </summary>
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

    /// <summary>
    /// Verifies that saving a definition with a None source does not mutate the original in-memory source.
    /// </summary>
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
