using System.Drawing;
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

    [Theory]
    [InlineData(CycleType.Simple, 0.6)]
    [InlineData(CycleType.Repeating, 0.6)]
    [InlineData(CycleType.PingPong, 0.8)]
    public void MixedDurationsRoundTripAndFollowDisplayedFrame(CycleType type, double total)
    {
        var sheet = CreateTilesheet();
        var sequence = new FrameSequence([sheet.GetFrame(0, 0), sheet.GetFrame(1, 0), sheet.GetFrame(0, 0)])
        { SequenceCycleType = type };
        sequence.SetDurationSeconds(0, 0.1);
        sequence.SetDurationSeconds(2, 0.3);
        var original = new Cycle(sequence, 0.2, "mixed");
        var definition = AnimationDefinitionSerializer.FromJson(AnimationDefinitionSerializer.ToJson(original));
        original.Dispose();
        var cycle = AnimationDefinitionSerializer.ToCycle(definition);
        Assert.Null(definition.Frames[1].DurationSeconds);
        Assert.Equal(total, cycle.TotalCycleTime, 8);
        Assert.Equal(0.1, cycle.CurrentFrameDurationSeconds);
        cycle.Sequence.AdvanceFrame();
        Assert.Equal(0.2, cycle.CurrentFrameDurationSeconds);
        cycle.Sequence.AdvanceFrame();
        Assert.Equal(0.3, cycle.CurrentFrameDurationSeconds);
        cycle.Sequence.AdvanceFrame();
        Assert.Equal(type == CycleType.PingPong ? 0.2 : type == CycleType.Repeating ? 0.1 : 0.3,
            cycle.CurrentFrameDurationSeconds);
        var clone = (Cycle)cycle.Clone();
        Assert.Equal(0.2, clone.ThrottleTime);
        clone.Sequence.SetDurationSeconds(0, 0.9);
        Assert.Equal(0.1, cycle.Sequence.GetDurationSeconds(0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidFrameDurationsAreRejected(double duration)
    {
        var definition = new AnimationDefinition { Key = "invalid", Frames = [new() { Tilesheet = "test", DurationSeconds = duration }] };
        Assert.Contains(AnimationDefinitionValidator.Validate(definition), e => e.Contains("DurationSeconds"));
    }

    [Theory]
    [InlineData(CycleType.Simple)]
    [InlineData(CycleType.Repeating)]
    [InlineData(CycleType.PingPong)]
    public void AnimatorConsumesCurrentFrameDelayIncludingCatchUp(CycleType type)
    {
        Gondwana.Timers.EngineSimulationClock.BeginTimerDriven(0);
        try
        {
            var sheet = CreateTilesheet();
            var sequence = new FrameSequence([sheet.GetFrame(0, 0), sheet.GetFrame(1, 0), sheet.GetFrame(0, 0)]) { SequenceCycleType = type };
            sequence.SetDurationSeconds(0, 0.1); sequence.SetDurationSeconds(2, 0.3);
            using var cycle = new Cycle(sequence, 0.2, "animator.mixed");
            using var scene = new Gondwana.Scenes.Scene();
            var tile = scene.AddLayer(1, 1, 16, 16)[0, 0]!;
            tile.CurrentFrame = sheet.GetFrame(0, 0); tile.EnableAnimator = true;
            var animator = tile.TileAnimator; animator.StartAnimation("animator.mixed");
            long Tick(double seconds) => (long)(seconds * Gondwana.Timers.HighResTimer.TicksPerSecond);
            animator.CycleAnimation(Tick(0.09)); Assert.Equal(0, animator.CurrentCycle.Sequence.CurrentFrameIdx);
            animator.CycleAnimation(Tick(0.11)); Assert.Equal(1, animator.CurrentCycle.Sequence.CurrentFrameIdx);
            animator.CycleAnimation(Tick(0.29)); Assert.Equal(1, animator.CurrentCycle.Sequence.CurrentFrameIdx);
            animator.CycleAnimation(Tick(0.59)); Assert.Equal(2, animator.CurrentCycle.Sequence.CurrentFrameIdx);
            animator.CycleAnimation(Tick(0.61));
            Assert.Equal(type == CycleType.Repeating ? 0 : type == CycleType.PingPong ? 1 : 2, animator.CurrentCycle.Sequence.CurrentFrameIdx);
            Assert.Equal(type != CycleType.Simple, animator.IsCycling);
            animator.StopAnimation();
        }
        finally { Gondwana.Timers.EngineSimulationClock.UseWallClock(); }
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
    public void TilesheetSources_RoundTripAsAuthoringMetadataWithoutChangingRuntimeResolution()
    {
        var sheet = CreateTilesheet();

        var definition = new AnimationDefinition
        {
            Key = "actor.walk",
            ThrottleTime = 0.1,
            CycleType = CycleType.Repeating,
            TilesheetSources =
            [
                AnimationTilesheetSourceDefinition.Loose(
                    sheet.Name,
                    "tilesheets/actor.gts"),
                AnimationTilesheetSourceDefinition.Packed(
                    "unused-packed-sheet",
                    "content.gaf",
                    "tilesheets/unused.gts")
            ],
            Frames =
            [
                new AnimationFrameDefinition
                {
                    Tilesheet = sheet.Name,
                    RegionName = TilesheetRegion.DefaultRegionName,
                    XTile = 0,
                    YTile = 0
                }
            ]
        };

        string json = AnimationDefinitionSerializer.ToJson(definition);
        var restored = AnimationDefinitionSerializer.FromJson(json);

        Assert.Equal(2, restored.TilesheetSources.Count);

        var loose = restored.TilesheetSources[0];
        Assert.Equal(sheet.Name, loose.Tilesheet);
        Assert.Equal(
            AnimationTilesheetSourceKind.LooseDefinitionFile,
            loose.Kind);
        Assert.Equal("tilesheets/actor.gts", loose.GtsPath);

        var packed = restored.TilesheetSources[1];
        Assert.Equal(
            AnimationTilesheetSourceKind.PackedDefinitionFile,
            packed.Kind);
        Assert.Equal("content.gaf", packed.AssetsFilePath);
        Assert.Equal("tilesheets/unused.gts", packed.AssetEntryName);

        using var cycle = AnimationDefinitionSerializer.ToCycle(restored);
        Assert.Single(cycle.Sequence.FrameList);
        Assert.Same(sheet, cycle.Sequence[0].Tilesheet);
    }

    [Fact]
    public void Validator_ReportsMalformedTilesheetSourceMetadata()
    {
        var definition = new AnimationDefinition
        {
            Key = "actor.walk",
            TilesheetSources =
            [
                new AnimationTilesheetSourceDefinition
                {
                    Tilesheet = "actors",
                    Kind = AnimationTilesheetSourceKind.LooseDefinitionFile
                },
                new AnimationTilesheetSourceDefinition
                {
                    Tilesheet = "actors",
                    Kind = AnimationTilesheetSourceKind.PackedDefinitionFile,
                    AssetsFilePath = "content.gaf"
                }
            ],
            Frames =
            [
                new AnimationFrameDefinition
                {
                    Tilesheet = "actors",
                    RegionName = "default"
                }
            ]
        };

        var errors = AnimationDefinitionValidator.Validate(definition);

        Assert.Contains(
            errors,
            error => error.Contains(
                "loose GTS source path is empty",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            errors,
            error => error.Contains(
                "duplicate source",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            errors,
            error => error.Contains(
                "packed GTS entry name is empty",
                StringComparison.OrdinalIgnoreCase));
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
