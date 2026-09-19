using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>
/// Verifies GANI definition conversion, validation, and runtime materialization.
/// </summary>
[Collection("Global engine state")]
public sealed class AnimationGaniTests : IDisposable
{
    private readonly List<Tilesheet> _tilesheets = [];

    public AnimationGaniTests()
    {
        Cycle.ClearAllAnimationCycles();
    }

    public void Dispose()
    {
        Cycle.ClearAllAnimationCycles();

        foreach (var tilesheet in _tilesheets)
            tilesheet.Dispose();
    }

    [Fact]
    public void RuntimeCycle_RoundTripsPersistentDefinition()
    {
        var sheet = CreateTilesheet();
        var sequence = new FrameSequence(
            [
                sheet.GetFrame(0, 0),
                sheet.GetFrame(1, 0)
            ])
        {
            SequenceCycleType = CycleType.PingPong
        };

        var original = new Cycle(
            sequence,
            throttleTime: 0.125,
            cycleKey: "actor.walk",
            hideTileOnCycleEnd: true);

        var definition = AnimationDefinitionSerializer.FromCycle(original);
        var json = AnimationDefinitionSerializer.ToJson(definition);

        Assert.Equal(AnimationDefinitionSourceKind.Generated, definition.Source.Kind);
        Assert.DoesNotContain("\"$id\"", json);
        Assert.DoesNotContain("\"$ref\"", json);
        Assert.Equal("actor.walk", definition.Key);
        Assert.Equal("actor.walk", definition.NextCycleKey);
        Assert.Equal(2, definition.Frames.Count);

        original.Dispose();

        var restored = AnimationDefinitionSerializer.ToCycle(
            AnimationDefinitionSerializer.FromJson(json));

        Assert.Equal("actor.walk", restored.CycleKey);
        Assert.Equal(0.125, restored.ThrottleTime);
        Assert.Equal(CycleType.PingPong, restored.Sequence.SequenceCycleType);
        Assert.True(restored.HideTileOnCycleEnd);
        Assert.Same(restored, restored.NextCycle);
        Assert.Equal(2, restored.Sequence.FrameCount);
        Assert.Equal(sheet.Name, restored.Sequence[0].Tilesheet.Name);
        Assert.Equal(1, restored.Sequence[1].XTile);
    }

    [Fact]
    public void Definition_ResolvesRegisteredTilesheetFrames()
    {
        var sheet = CreateTilesheet();

        var definition = new AnimationDefinition
        {
            Key = "world.water",
            ThrottleTime = 0.2,
            CycleType = CycleType.Repeating,
            Frames =
            [
                new AnimationFrameDefinition
                {
                    Tilesheet = sheet.Name,
                    RegionName = TilesheetRegion.DefaultRegionName,
                    XTile = 1,
                    YTile = 0
                }
            ]
        };

        var cycle = AnimationDefinitionSerializer.ToCycle(definition);

        Assert.Equal(CycleType.Repeating, cycle.Sequence.SequenceCycleType);
        Assert.Single(cycle.Sequence.FrameList);
        Assert.Same(sheet, cycle.Sequence[0].Tilesheet);
        Assert.Same(cycle, cycle.NextCycle);
    }

    [Fact]
    public void SaveAndLoad_StampLooseDefinitionSourceWithoutMutatingOriginal()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"GondwanaGani_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var path = Path.Combine(directory, "walk.gani");
            var definition = new AnimationDefinition
            {
                Key = "actor.walk",
                Frames =
                [
                    new AnimationFrameDefinition
                    {
                        Tilesheet = "actors",
                        RegionName = "walk"
                    }
                ]
            };

            AnimationDefinitionSerializer.Save(path, definition);
            var loaded = AnimationDefinitionSerializer.Load(path);

            Assert.Equal(AnimationDefinitionSourceKind.None, definition.Source.Kind);
            Assert.Equal(AnimationDefinitionSourceKind.LooseDefinitionFile, loaded.Source.Kind);
            Assert.Equal(Path.GetFullPath(path), loaded.Source.GaniFilePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Validator_ReportsMissingKeyFramesAndInvalidCoordinates()
    {
        var definition = new AnimationDefinition
        {
            ThrottleTime = -1,
            Frames =
            [
                new AnimationFrameDefinition
                {
                    Tilesheet = string.Empty,
                    RegionName = string.Empty,
                    XTile = -1
                }
            ]
        };

        var errors = AnimationDefinitionValidator.Validate(definition);

        Assert.Contains(errors, error => error.Contains("key is empty", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("ThrottleTime", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("tilesheet name is empty", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("region name is empty", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("cannot be negative", StringComparison.OrdinalIgnoreCase));
    }

    private Tilesheet CreateTilesheet()
    {
        var sheet = TilesheetRegistry.Instance.LoadFromBitmap(
            $"GANI_{Guid.NewGuid():N}",
            new SKBitmap(32, 16));

        sheet.DefaultRegion.TileSize = new Size(16, 16);
        _tilesheets.Add(sheet);
        return sheet;
    }
}
