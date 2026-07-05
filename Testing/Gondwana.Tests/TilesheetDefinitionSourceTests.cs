using Gondwana.Drawing.Tilesheets.GTS;

namespace Gondwana.Tests;

public sealed class TilesheetDefinitionSourceTests
{
    // -----------------------------------------------------------------------
    // Factory: None
    // -----------------------------------------------------------------------

    [Fact]
    public void None_HasNoneKindAndNullPaths()
    {
        var source = TilesheetDefinitionSource.None();

        Assert.Equal(TilesheetDefinitionSourceKind.None, source.Kind);
        Assert.Null(source.GtsFilePath);
        Assert.Null(source.AssetsFilePath);
        Assert.Null(source.AssetEntryName);
    }

    // -----------------------------------------------------------------------
    // Factory: Generated
    // -----------------------------------------------------------------------

    [Fact]
    public void Generated_HasGeneratedKindAndNullPaths()
    {
        var source = TilesheetDefinitionSource.Generated();

        Assert.Equal(TilesheetDefinitionSourceKind.Generated, source.Kind);
        Assert.Null(source.GtsFilePath);
        Assert.Null(source.AssetsFilePath);
        Assert.Null(source.AssetEntryName);
    }

    // -----------------------------------------------------------------------
    // Factory: LooseDefinitionFile
    // -----------------------------------------------------------------------

    [Fact]
    public void LooseDefinitionFile_SetsKindAndGtsFilePath()
    {
        var source = TilesheetDefinitionSource.LooseDefinitionFile("/data/sheet.gts");

        Assert.Equal(TilesheetDefinitionSourceKind.LooseDefinitionFile, source.Kind);
        Assert.Equal("/data/sheet.gts", source.GtsFilePath);
        Assert.Null(source.AssetsFilePath);
        Assert.Null(source.AssetEntryName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LooseDefinitionFile_NullOrWhitespacePath_ThrowsArgumentException(string? path)
    {
        Assert.Throws<ArgumentException>(() => TilesheetDefinitionSource.LooseDefinitionFile(path!));
    }

    // -----------------------------------------------------------------------
    // Factory: PackedDefinitionFile
    // -----------------------------------------------------------------------

    [Fact]
    public void PackedDefinitionFile_SetsKindAndBothPaths()
    {
        var source = TilesheetDefinitionSource.PackedDefinitionFile("/data/assets.gaf", "sheet.gts");

        Assert.Equal(TilesheetDefinitionSourceKind.PackedDefinitionFile, source.Kind);
        Assert.Equal("/data/assets.gaf", source.AssetsFilePath);
        Assert.Equal("sheet.gts", source.AssetEntryName);
        Assert.Null(source.GtsFilePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PackedDefinitionFile_NullOrWhitespaceAssetsPath_ThrowsArgumentException(string? assetsPath)
    {
        Assert.Throws<ArgumentException>(() => TilesheetDefinitionSource.PackedDefinitionFile(assetsPath!, "entry.gts"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PackedDefinitionFile_NullOrWhitespaceEntryName_ThrowsArgumentException(string? entryName)
    {
        Assert.Throws<ArgumentException>(() => TilesheetDefinitionSource.PackedDefinitionFile("/data/assets.gaf", entryName!));
    }

    // -----------------------------------------------------------------------
    // ToJson content — all 4 source kinds
    // -----------------------------------------------------------------------

    [Fact]
    public void ToJson_SourceNone_SerializesKindAsZero()
    {
        var definition = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.None()
        };

        var json = TilesheetDefinitionSerializer.ToJson(definition);

        Assert.Contains("\"Kind\": 0", json);
    }

    [Fact]
    public void ToJson_SourceLooseDefinitionFile_SerializesKindAndPath()
    {
        var definition = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.LooseDefinitionFile("/project/sheet.gts")
        };

        var json = TilesheetDefinitionSerializer.ToJson(definition);

        Assert.Contains("\"Kind\": 1", json);
        Assert.Contains("/project/sheet.gts", json);
    }

    [Fact]
    public void ToJson_SourcePackedDefinitionFile_SerializesKindAndBothPaths()
    {
        var definition = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.PackedDefinitionFile("/project/assets.gaf", "sheet.gts")
        };

        var json = TilesheetDefinitionSerializer.ToJson(definition);

        Assert.Contains("\"Kind\": 2", json);
        Assert.Contains("/project/assets.gaf", json);
        Assert.Contains("sheet.gts", json);
    }

    [Fact]
    public void ToJson_SourceGenerated_SerializesKindAsThree()
    {
        var definition = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.Generated()
        };

        var json = TilesheetDefinitionSerializer.ToJson(definition);

        Assert.Contains("\"Kind\": 3", json);
    }

    // -----------------------------------------------------------------------
    // ToJson/FromJson round-trip — all 4 source kinds
    // -----------------------------------------------------------------------

    [Fact]
    public void RoundTrip_SourceNone_PreservesKind()
    {
        var original = new TilesheetDefinition { Name = "Sheet", Source = TilesheetDefinitionSource.None() };

        var json = TilesheetDefinitionSerializer.ToJson(original);
        var restored = TilesheetDefinitionSerializer.FromJson(json);

        Assert.Equal(TilesheetDefinitionSourceKind.None, restored.Source.Kind);
    }

    [Fact]
    public void RoundTrip_SourceLooseDefinitionFile_PreservesKindAndPath()
    {
        var original = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.LooseDefinitionFile("/project/sheet.gts")
        };

        var json = TilesheetDefinitionSerializer.ToJson(original);
        var restored = TilesheetDefinitionSerializer.FromJson(json);

        Assert.Equal(TilesheetDefinitionSourceKind.LooseDefinitionFile, restored.Source.Kind);
        Assert.Equal("/project/sheet.gts", restored.Source.GtsFilePath);
        Assert.Null(restored.Source.AssetsFilePath);
        Assert.Null(restored.Source.AssetEntryName);
    }

    [Fact]
    public void RoundTrip_SourcePackedDefinitionFile_PreservesKindAndBothPaths()
    {
        var original = new TilesheetDefinition
        {
            Name = "Sheet",
            Source = TilesheetDefinitionSource.PackedDefinitionFile("/project/assets.gaf", "sheet.gts")
        };

        var json = TilesheetDefinitionSerializer.ToJson(original);
        var restored = TilesheetDefinitionSerializer.FromJson(json);

        Assert.Equal(TilesheetDefinitionSourceKind.PackedDefinitionFile, restored.Source.Kind);
        Assert.Equal("/project/assets.gaf", restored.Source.AssetsFilePath);
        Assert.Equal("sheet.gts", restored.Source.AssetEntryName);
        Assert.Null(restored.Source.GtsFilePath);
    }

    [Fact]
    public void RoundTrip_SourceGenerated_PreservesKind()
    {
        var original = new TilesheetDefinition { Name = "Sheet", Source = TilesheetDefinitionSource.Generated() };

        var json = TilesheetDefinitionSerializer.ToJson(original);
        var restored = TilesheetDefinitionSerializer.FromJson(json);

        Assert.Equal(TilesheetDefinitionSourceKind.Generated, restored.Source.Kind);
        Assert.Null(restored.Source.GtsFilePath);
        Assert.Null(restored.Source.AssetsFilePath);
        Assert.Null(restored.Source.AssetEntryName);
    }
}
