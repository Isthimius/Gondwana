using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using Gondwana.Tooling.Tilesheets.Editing;
using Gondwana.Tooling.Tilesheets.WinForms;
using SkiaSharp;

namespace Gondwana.Tests;

public sealed class TilesheetEditorDocumentTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "GtsEditorTests_" + Guid.NewGuid().ToString("N"));

    public TilesheetEditorDocumentTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    private string WriteDefinition(bool packed = false)
    {
        File.WriteAllBytes(Path.Combine(_directory, "different-image.png"), [1]);
        var definition = new TilesheetDefinition
        {
            Name = "Complete model",
            Source = TilesheetDefinitionSource.PackedDefinitionFile("provenance.gaf", "original.gts"),
            Image = packed ? new() { AssetsFilePath = "images.gaf", AssetEntryName = "unrelated-name" }
                : new() { FilePath = "different-image.png" },
            PremultiplyAlpha = true,
            Mask = new() { Red = 12, Green = 34, Blue = 56, Alpha = 78, Tolerance = 9 },
            Regions =
            [
                new()
                {
                    Name = "first", Area = new(3, 4, 90, 70), TileSize = new(16, 12),
                    RegionMargin = new(1, 2, 3, 4), TilePadding = new(2, 3, 4, 5), Overhang = new(1, 2, 3, 4),
                    CollisionAdjust = new(1, -2, 3, -4), CollisionType = TileCollisionType.Blocking,
                    Frames =
                    [
                        new() { XTile = 0, YTile = 0 },
                        new() { XTile = 1, YTile = 0, CollisionAdjust = new(1, -2, 3, -4), CollisionType = TileCollisionType.Blocking },
                        new() { XTile = 2, YTile = 0, CollisionAdjust = CollisionAdjust.None, CollisionType = TileCollisionType.None },
                        new() { XTile = 0, YTile = 1, CollisionType = TileCollisionType.Trigger }
                    ]
                },
                new() { Name = "second", Area = new(100, 80, 32, 32), TileSize = new(8, 8) }
            ]
        };
        string path = Path.Combine(_directory, "document.gts");
        TilesheetDefinitionSerializer.Save(path, definition);
        return path;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpenInspectSave_PreservesEntireModelAndInheritance(bool packed)
    {
        string path = WriteDefinition(packed);
        var document = TilesheetDocument.Open(path);
        string before = TilesheetDefinitionSerializer.ToJson(document.Definition);
        var region = document.Definition.Regions[0];
        Assert.Null(document.FindFrame(region, 2, 1));
        _ = PropertyFields.Frame(document, region, 2, 1, document.MarkChanged).GetProperties();
        _ = PropertyFields.Definition(document, document.MarkChanged).GetProperties();
        Assert.False(document.IsDirty);
        document.Save(path);
        Assert.Equal(before, TilesheetDefinitionSerializer.ToJson(TilesheetDefinitionSerializer.Load(path)));
        Assert.Null(region.Frames[0].CollisionAdjust);
        Assert.Equal(region.CollisionAdjust, region.Frames[1].CollisionAdjust);
        Assert.Equal(TileCollisionType.None, region.Frames[2].CollisionType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaveAs_RebasesImageReferenceAndPreservesProvenance(bool packed)
    {
        var document = TilesheetDocument.Open(WriteDefinition(packed));
        string destination = Path.Combine(_directory, "nested", "new.gts");
        document.Save(destination);
        var loaded = TilesheetDocument.Open(destination);
        if (packed)
            Assert.Equal("../images.gaf", loaded.Definition.Image.AssetsFilePath);
        else
            Assert.Equal(Path.Combine(_directory, "different-image.png"), loaded.ResolveImagePath());
        Assert.Equal("provenance.gaf", loaded.Definition.Source.AssetsFilePath);
        Assert.Equal("original.gts", loaded.Definition.Source.AssetEntryName);
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void GeometryEdit_RetainsMetadataUntilExplicitPrune()
    {
        var document = TilesheetDocument.Open(WriteDefinition());
        var region = document.Definition.Regions[0];
        var frame = region.Frames[1];
        var originalArea = region.Area;
        region.Area = new Rectangle(3, 4, 26, 26);
        document.MarkChanged();
        Assert.Contains(document.Validate(), e => e.Contains("outside the frame grid"));
        Assert.Same(frame, document.FindFrame(region, 1, 0));
        region.Area = originalArea;
        Assert.Empty(document.Validate());
        Assert.Same(frame, document.FindFrame(region, 1, 0));
        region.Area = new Rectangle(3, 4, 26, 26);
        Assert.Equal(3, document.RemoveOutOfGridMetadata(region));
        Assert.Single(region.Frames);
        Assert.Empty(document.Validate());
    }

    [Fact]
    public void PropertyAdapters_EditStructEdgesAndExplicitEqualOverrides()
    {
        var document = TilesheetDocument.Open(WriteDefinition());
        var region = document.Definition.Regions[0];
        var regionFields = new PropertyFields();
        regionFields.AddModel(region, document.MarkChanged, "Frames");
        regionFields.GetProperties()["TilePadding.Left"]!.SetValue(regionFields, 7);
        Assert.Equal(7, region.TilePadding.Left);
        Assert.Equal(3, region.TilePadding.Top);
        var fields = PropertyFields.Frame(document, region, 0, 0, document.MarkChanged);
        fields.GetProperties()["CollisionAdjust mode"]!.SetValue(fields, Inheritance.Override);
        fields.GetProperties()["CollisionType"]!.SetValue(fields, FrameCollisionChoice.Blocking);
        Assert.Equal(region.CollisionAdjust, region.Frames[0].CollisionAdjust);
        Assert.Equal(region.CollisionType, region.Frames[0].CollisionType);
        fields = PropertyFields.Frame(document, region, 0, 0, document.MarkChanged);
        fields.GetProperties()["Left"]!.SetValue(fields, -9);
        Assert.Equal(-9, region.Frames[0].CollisionAdjust!.Value.Left);
        fields.GetProperties()["CollisionAdjust mode"]!.SetValue(fields, Inheritance.InheritRegion);
        fields.GetProperties()["CollisionType"]!.SetValue(fields, FrameCollisionChoice.InheritRegion);
        Assert.Null(region.Frames[0].CollisionAdjust);
        Assert.Null(region.Frames[0].CollisionType);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void InvalidSaveAndFailedSave_KeepOriginalFileAndDirtySession()
    {
        string path = WriteDefinition();
        var document = TilesheetDocument.Open(path);
        string before = File.ReadAllText(path);
        document.Definition.Regions[0].TileSize = Size.Empty;
        document.MarkChanged();
        Assert.Throws<InvalidOperationException>(() => document.Save(path));
        Assert.Equal(before, File.ReadAllText(path));
        Assert.True(document.IsDirty);
        var failure = Record.Exception(() => document.Save(_directory, allowInvalid: true));
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.Equal(path, document.FilePath);
        Assert.True(document.IsDirty);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        document.Save(path, allowInvalid: true);
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void MaskDisabled_SerializesNullAndKeepsPremultiplyAlpha()
    {
        var document = TilesheetDocument.Open(WriteDefinition());
        var fields = PropertyFields.Definition(document, document.MarkChanged);
        fields.GetProperties()["Mask enabled"]!.SetValue(fields, false);
        document.Save(document.FilePath!);
        var loaded = TilesheetDefinitionSerializer.Load(document.FilePath!);
        Assert.Null(loaded.Mask);
        Assert.True(loaded.PremultiplyAlpha);
    }

    [Fact]
    public void FrameHitTest_SelectsOtherGtsRegionsWithoutChangingMetadata()
    {
        var document = TilesheetDocument.Open(WriteDefinition());
        var before = TilesheetDefinitionSerializer.ToJson(document.Definition);
        var first = document.Definition.Regions[0];
        var second = document.Definition.Regions[1];
        var hit = FrameGeometry.HitTest(document.Definition, first, new PointF(113, 93));
        Assert.NotNull(hit);
        Assert.Same(second, hit.Value.Region);
        Assert.Equal(new Point(1, 1), hit.Value.Frame);
        Assert.Equal(before, TilesheetDefinitionSerializer.ToJson(document.Definition));
        Assert.False(document.IsDirty);
        Assert.Null(FrameGeometry.HitTest(document.Definition, first, new PointF(4, 5))); // margin
        Assert.Null(FrameGeometry.HitTest(document.Definition, first, new PointF(5, 7))); // padding
        Assert.Null(FrameGeometry.HitTest(document.Definition, first, new PointF(-1, -1)));
    }

    [Fact]
    public void FrameHitTest_PrefersSelectedRegionWhenGridsOverlap()
    {
        var first = new TilesheetRegionDefinition { Area = new(0, 0, 64, 64), TileSize = new(16, 16) };
        var second = new TilesheetRegionDefinition { Area = first.Area, TileSize = new(32, 32) };
        var definition = new TilesheetDefinition { Regions = [first, second] };
        var hit = FrameGeometry.HitTest(definition, second, new PointF(40, 40));
        Assert.Same(second, hit!.Value.Region);
        Assert.Equal(new Point(1, 1), hit.Value.Frame);
        hit = FrameGeometry.HitTest(definition, null, new PointF(40, 40));
        Assert.Same(first, hit!.Value.Region);
        Assert.Equal(new Point(2, 2), hit.Value.Frame);
    }

    [Fact]
    public void DerivedGeometry_MatchesRuntimeSlicesAndCollisionAdjust()
    {
        var document = TilesheetDocument.Open(WriteDefinition());
        var region = document.Definition.Regions[0];
        using var bitmap = new SKBitmap(160, 128);
        using var runtime = TilesheetFactory.FromBitmap("Editor parity", bitmap);
        var runtimeRegion = runtime.AddRegion(region.Name, region.Area, region.TileSize, region.TilePadding,
            region.RegionMargin, region.Overhang, region.CollisionAdjust, region.CollisionType);
        var grid = TilesheetDefinitionValidator.GridSize(region);
        Assert.Equal(runtimeRegion.Columns, grid.Columns);
        Assert.Equal(runtimeRegion.Rows, grid.Rows);
        Assert.Equal(new Rectangle(28, 9, 16, 12), FrameGeometry.Bounds(region, 1, 0));
        var bounds = FrameGeometry.CollisionBounds(region, 1, 0);
        bounds.Offset(-28, -9);
        Assert.Equal(runtimeRegion.GetFrameCollisionArea(1, 0), bounds);
    }
}
