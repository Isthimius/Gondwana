using System.Drawing;
using Gondwana.Assets;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Tilesheets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>
/// Verifies EngineState integration for inline and external GANI animation definitions.
/// </summary>
[Collection("Global engine state")]
public sealed class EngineStateGaniTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        $"GondwanaEngineStateGani_{Guid.NewGuid():N}");

    public EngineStateGaniTests()
    {
        Directory.CreateDirectory(_tempDir);
        Cycle.ClearAllAnimationCycles();
        TilesheetRegistry.Instance.Clear();
        AssetsFile.ClearAll();
    }

    public void Dispose()
    {
        Cycle.ClearAllAnimationCycles();
        TilesheetRegistry.Instance.Clear();
        AssetsFile.ClearAll();

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void SaveToFile_Default_EmbedsInlineGaniDefinition()
    {
        var sheet = CreateTilesheet("inline-sheet");
        _ = CreateCycle(sheet, "world.water", 0.2);
        var path = Path.Combine(_tempDir, "inline.state");

        new EngineState().SaveToFile(
            path,
            parts: EngineStateParts.Tilesheets | EngineStateParts.Cycles);

        var root = JObject.Parse(File.ReadAllText(path));
        var entry = GetAnimationEntry(root, "world.water");

        Assert.NotNull(entry["Definition"]);
        AssertJsonNullOrMissing(entry, "GaniPath");

        var definition = Assert.IsType<JObject>(entry["Definition"]);
        Assert.Equal("world.water", definition.Value<string>("Key"));
        Assert.NotNull(definition["Frames"]);
        Assert.Null(definition["Sequence"]);
        Assert.DoesNotContain("\"$ref\"", definition.ToString());
    }

    [Fact]
    public void SaveToFile_SeparateGaniFiles_WritesReferenceAndCleanDefinition()
    {
        var sheet = CreateTilesheet("external-sheet");
        _ = CreateCycle(sheet, "actor.walk", 0.1);
        var path = Path.Combine(_tempDir, "external.state");

        new EngineState().SaveToFile(
            path,
            parts: EngineStateParts.Tilesheets | EngineStateParts.Cycles,
            separateGaniFiles: true);

        var root = JObject.Parse(File.ReadAllText(path));
        var entry = GetAnimationEntry(root, "actor.walk");

        AssertJsonNullOrMissing(entry, "Definition");
        var relativePath = entry.Value<string>("GaniPath");
        Assert.False(string.IsNullOrWhiteSpace(relativePath));

        var ganiPath = Path.GetFullPath(Path.Combine(_tempDir, relativePath!));
        Assert.True(File.Exists(ganiPath));
        Assert.Equal(
            Path.Combine(_tempDir, "external.animations"),
            Path.GetDirectoryName(ganiPath));

        var ganiJson = File.ReadAllText(ganiPath);
        Assert.DoesNotContain("\"$id\"", ganiJson);
        Assert.DoesNotContain("\"$ref\"", ganiJson);
        Assert.Contains("\"Frames\"", ganiJson);
        Assert.Contains("\"Tilesheet\": \"external-sheet\"", ganiJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadFromFile_RestoresGaniAfterItsTilesheets(bool separateGaniFiles)
    {
        var sheet = CreateTilesheet("restore-sheet");
        _ = CreateCycle(sheet, "effects.spark", 0.075, CycleType.PingPong);
        var path = Path.Combine(
            _tempDir,
            separateGaniFiles ? "restore-external.state" : "restore-inline.state");

        new EngineState().SaveToFile(
            path,
            parts: EngineStateParts.Tilesheets | EngineStateParts.Cycles,
            separateGaniFiles: separateGaniFiles);

        Cycle.ClearAllAnimationCycles();
        TilesheetRegistry.Instance.Clear();

        // Cycles depend on Tilesheets. Dependency normalization restores the
        // tilesheet portion of this state before GANI materialization.
        EngineState.LoadFromFile(
            path,
            parts: EngineStateParts.Cycles);

        var restoredSheet = TilesheetRegistry.Instance["restore-sheet"];
        var restored = Assert.Single(
            Cycle.GetAnimationCycles(),
            cycle => cycle.CycleKey == "effects.spark");

        Assert.Equal(0.075, restored.ThrottleTime);
        Assert.Equal(CycleType.PingPong, restored.Sequence.SequenceCycleType);
        Assert.Equal(2, restored.Sequence.FrameCount);
        Assert.Same(restoredSheet, restored.Sequence[0].Tilesheet);
        Assert.Same(restored, restored.NextCycle);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadFromFile_ResolvesCircularNextCycleReferences(bool separateGaniFiles)
    {
        var sheet = CreateTilesheet("chain-sheet");
        var idle = CreateCycle(sheet, "actor.idle", 0.4);
        var blink = CreateCycle(sheet, "actor.blink", 0.08, CycleType.Simple);
        idle.NextCycle = blink;
        blink.NextCycle = idle;

        var path = Path.Combine(
            _tempDir,
            separateGaniFiles ? "chain-external.state" : "chain-inline.state");

        new EngineState().SaveToFile(
            path,
            parts: EngineStateParts.Tilesheets | EngineStateParts.Cycles,
            separateGaniFiles: separateGaniFiles);

        Cycle.ClearAllAnimationCycles();
        TilesheetRegistry.Instance.Clear();

        EngineState.LoadFromFile(
            path,
            parts: EngineStateParts.Cycles);

        var cycles = Cycle.GetAnimationCycles();
        var restoredIdle = Assert.Single(cycles, cycle => cycle.CycleKey == "actor.idle");
        var restoredBlink = Assert.Single(cycles, cycle => cycle.CycleKey == "actor.blink");

        Assert.Same(restoredBlink, restoredIdle.NextCycle);
        Assert.Same(restoredIdle, restoredBlink.NextCycle);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MergeFromFile_GaniHonorsOverwriteExisting(bool separateGaniFiles)
    {
        var sheet = CreateTilesheet("merge-sheet");
        _ = CreateCycle(sheet, "shared.animation", 0.15);
        var path = Path.Combine(
            _tempDir,
            separateGaniFiles ? "merge-external.state" : "merge-inline.state");

        new EngineState().SaveToFile(
            path,
            parts: EngineStateParts.Tilesheets | EngineStateParts.Cycles,
            separateGaniFiles: separateGaniFiles);

        Cycle.ClearAllAnimationCycles();
        var existing = CreateCycle(sheet, "shared.animation", 0.9);

        EngineState.MergeFromFile(
            path,
            overwriteExisting: false,
            parts: EngineStateParts.Cycles);

        var preserved = Assert.Single(
            Cycle.GetAnimationCycles(),
            cycle => cycle.CycleKey == "shared.animation");
        Assert.Same(existing, preserved);
        Assert.Equal(0.9, preserved.ThrottleTime);

        EngineState.MergeFromFile(
            path,
            overwriteExisting: true,
            parts: EngineStateParts.Cycles);

        var replaced = Assert.Single(
            Cycle.GetAnimationCycles(),
            cycle => cycle.CycleKey == "shared.animation");
        Assert.NotSame(existing, replaced);
        Assert.Equal(0.15, replaced.ThrottleTime);
    }

    [Fact]
    public void LoadFromFile_AcceptsLegacyRawCycleEntries()
    {
        var sheet = CreateTilesheet("legacy-sheet");
        _ = CreateCycle(sheet, "legacy.walk", 0.33, CycleType.Repeating);

        var serializer = JsonSerializer.Create(EngineState.JsonSerializerSettings);
        var legacyCycles = JToken.FromObject(Cycle._cycles, serializer);

        // This fragment is serialized independently from the EngineState shell below,
        // so its reference IDs start over at "1". Real legacy files used one serializer
        // for the whole document. Prefix the fragment IDs to reproduce that uniqueness.
        foreach (var obj in legacyCycles.DescendantsAndSelf().OfType<JObject>())
        {
            foreach (var propertyName in new[] { "$id", "$ref" })
            {
                if (obj[propertyName] is JValue { Type: JTokenType.String } value)
                    value.Value = "legacy-" + value.Value<string>();
            }
        }

        var path = Path.Combine(_tempDir, "legacy.state");
        new EngineState().SaveToFile(
            path,
            parts: EngineStateParts.Tilesheets);

        var root = JObject.Parse(File.ReadAllText(path));
        root["Cycles"] = legacyCycles;
        File.WriteAllText(path, root.ToString());

        Cycle.ClearAllAnimationCycles();
        TilesheetRegistry.Instance.Clear();

        EngineState.LoadFromFile(
            path,
            parts: EngineStateParts.Cycles);

        var restored = Assert.Single(
            Cycle.GetAnimationCycles(),
            cycle => cycle.CycleKey == "legacy.walk");

        Assert.Equal(0.33, restored.ThrottleTime);
        Assert.Equal(CycleType.Repeating, restored.Sequence.SequenceCycleType);
        Assert.Equal("legacy-sheet", restored.Sequence[0].Tilesheet.Name);
        Assert.Same(restored, restored.NextCycle);
    }

    private Tilesheet CreateTilesheet(string name)
    {
        var imagePath = Path.Combine(_tempDir, $"{name}.png");
        using (var bitmap = new SKBitmap(32, 16))
        {
            bitmap.Erase(SKColors.White);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(imagePath, data.ToArray());
        }

        var sheet = TilesheetRegistry.Instance.LoadFromImageFile(name, imagePath);
        sheet.DefaultRegion.TileSize = new Size(16, 16);
        return sheet;
    }

    private static Cycle CreateCycle(
        Tilesheet sheet,
        string key,
        double throttle,
        CycleType cycleType = CycleType.Repeating)
    {
        var sequence = new FrameSequence(
            [
                sheet.GetFrame(0, 0),
                sheet.GetFrame(1, 0)
            ])
        {
            SequenceCycleType = cycleType
        };

        return new Cycle(sequence, throttle, key);
    }

    private static JObject GetAnimationEntry(JObject root, string key)
    {
        var cycles = Assert.IsType<JObject>(
            root["Cycles"]
            ?? throw new Xunit.Sdk.XunitException("EngineState JSON did not contain Cycles."));

        return Assert.IsType<JObject>(
            cycles[key]
            ?? throw new Xunit.Sdk.XunitException($"EngineState JSON did not contain animation '{key}'."));
    }

    private static void AssertJsonNullOrMissing(JObject obj, string propertyName)
    {
        var token = obj[propertyName];
        Assert.True(
            token is null || token.Type == JTokenType.Null,
            $"Expected '{propertyName}' to be absent or JSON null, but found {token?.Type}.");
    }
}
