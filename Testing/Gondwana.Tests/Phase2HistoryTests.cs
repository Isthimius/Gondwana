using System.ComponentModel;
using System.Drawing;
using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Tooling.Animations.Editing;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Audio.Editing;
using Gondwana.Tooling.Audio.WinForms;
using Gondwana.Tooling.Editing;
using Gondwana.Tooling.Sprites.Editing;
using Gondwana.Tooling.Sprites.WinForms;

namespace Gondwana.Tests;

public sealed class Phase2HistoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Phase2History_" + Guid.NewGuid().ToString("N"));
    public Phase2HistoryTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private object Create(string format) => format switch
    {
        "gani" => AnimationDocument.Create(_root),
        "gspr" => SpriteDocument.Create(_root),
        "gsnd" => AudioDocument.Create(_root),
        _ => throw new ArgumentException(format)
    };

    [Theory]
    [InlineData("gani")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void MemberOperationsRetainIdentityOrderingAndSelection(string format)
    {
        dynamic d = Create(format);
        using var owned = (IDisposable)d;
        EditHistory history = d.History;
        object first = d.Definition;
        object added = d.AddDefinition();
        object copy = d.DuplicateDefinition();
        d.RenameDefinition("renamed");
        history.Undo(); Assert.NotEqual("renamed", (string)d.Definition.Key);
        history.Redo(); Assert.Equal("renamed", (string)d.Definition.Key);
        Assert.True((bool)d.RemoveDefinition((dynamic)added));
        history.Undo(); Assert.Same(added, (object)d.Definitions[1]); Assert.Same(copy, (object)d.Definition);
        history.Redo(); Assert.Equal(2, (int)d.Definitions.Count);
        history.Undo(); history.Undo(); history.Undo();
        Assert.Same(added, (object)d.Definition);
        history.Undo(); Assert.Same(first, (object)d.Definition); Assert.Equal(1, (int)d.Definitions.Count);
        history.Redo(); Assert.Same(added, (object)d.Definition);
        d.SelectDefinition((dynamic)first);
        Assert.Equal("Add " + (format == "gani" ? "animation" : format == "gspr" ? "sprite" : "audio") + " definition", history.UndoDescription);
    }

    [Theory]
    [InlineData("gani")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void CheckpointsBranchingFailedSaveAndSaveAsPreserveHistory(string format)
    {
        dynamic d = Create(format);
        using var owned = (IDisposable)d;
        EditHistory history = d.History;
        d.Save(Path.Combine(_root, "original." + format), true);
        var original = (string)d.Definition.Key;
        d.RenameDefinition("A"); Assert.True((bool)d.IsDirty);
        history.Undo(); Assert.False((bool)d.IsDirty);
        history.Redo(); Assert.True((bool)d.IsDirty);
        d.Save(Path.Combine(_root, "saved." + format), true);
        d.RenameDefinition("B"); history.Undo(); Assert.False((bool)d.IsDirty);
        history.Undo(); Assert.True((bool)d.IsDirty); Assert.Equal(original, (string)d.Definition.Key);
        d.RenameDefinition("C"); Assert.False(history.CanRedo);
        Assert.ThrowsAny<Exception>(() => d.Save(_root, true)); Assert.True((bool)d.IsDirty);
        var destination = Path.Combine(_root, "new", "moved." + format);
        d.Save(destination, true); Assert.False((bool)d.IsDirty);
        history.Undo(); Assert.True((bool)d.IsDirty); Assert.Equal(destination, (string)d.FilePath);
        history.Redo(); Assert.False((bool)d.IsDirty);
        owned.Dispose(); Assert.False(history.CanUndo); Assert.False(history.CanRedo);
    }

    [Theory]
    [InlineData("gani")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void ImportIsOneStepAndPackedCheckpointRetainsMembers(string format)
    {
        dynamic source = Create(format);
        using var sourceOwner = (IDisposable)source;
        source.RenameDefinition("source"); source.AddDefinition();
        string path = Path.Combine(_root, "import." + format);
        source.Save(path, true);
        dynamic d = Create(format);
        using var owner = (IDisposable)d;
        object original = d.Definition;
        Assert.Equal(2, (int)d.ImportDefinitions(path));
        object imported = d.Definition;
        EditHistory history = d.History;
        history.Undo(); Assert.Same(original, (object)d.Definition); Assert.Equal(1, (int)d.Definitions.Count);
        history.Redo(); Assert.Same(imported, (object)d.Definition);
        string archivePath = Path.Combine(_root, "content.gaf");
        using (var archive = AssetsFile.LoadOrCreate(archivePath, null, false, register: false))
        {
            var type = format == "gani" ? AssetTypes.AnimationDefinition : format == "gspr" ? AssetTypes.SpriteDefinition : AssetTypes.AudioDefinition;
            var bytes = (byte[])d.SerializePacked();
            archive.Add(type, "typed/member", () => new MemoryStream(bytes));
            archive.Save();
        }
        using var packed = AssetsFile.LoadOrCreate(archivePath, null, false, register: false);
        dynamic opened = format switch
        {
            "gani" => (object)AnimationDocument.Open(packed, "typed/member"),
            "gspr" => SpriteDocument.Open(packed, "typed/member"),
            _ => AudioDocument.Open(packed, "typed/member")
        };
        using var packedOwner = (IDisposable)opened;
        EditHistory packedHistory = opened.History;
        Assert.False((bool)opened.IsDirty);
        opened.RenameDefinition("packed edit"); opened.AcceptPackedSave(); Assert.False((bool)opened.IsDirty);
        packedHistory.Undo(); Assert.True((bool)opened.IsDirty);
        packedHistory.Redo(); Assert.False((bool)opened.IsDirty);
    }

    [Fact]
    public void AnimationFramesAndPropertiesRetainInstancesAndSuppressNoOps()
    {
        using var d = AnimationDocument.Create(_root);
        var first = new AnimationFrameDefinition { Tilesheet = "tiles", RegionName = "body", XTile = 2, YTile = 3, DurationSeconds = .2 };
        var second = new AnimationFrameDefinition { Tilesheet = "tiles", RegionName = "other", XTile = 4, YTile = 5 };
        d.AddFrame(first); d.AddFrame(second);
        Assert.False(d.MoveFrame(0, -1));
        d.MoveFrame(0, 1); Assert.Same(first, d.Definition.Frames[1]);
        d.History.Undo(); Assert.Same(first, d.Definition.Frames[0]);
        d.History.Redo(); d.RemoveFrame(first); d.History.Undo(); Assert.Same(first, d.Definition.Frames[1]);
        Assert.Equal(.2, first.DurationSeconds); Assert.Equal(2, first.XTile);
        var properties = new AnimationPropertyAdapter(d) { SelectedFrameIndex = 1 };
        properties.DurationSeconds = null; Assert.Null(first.DurationSeconds);
        d.History.Undo(); Assert.Equal(.2, first.DurationSeconds);
        Assert.Throws<ArgumentOutOfRangeException>(() => properties.DurationSeconds = 0);
        properties.ThrottleTime = .4; properties.CycleType = CycleType.PingPong;
        properties.HideTileOnCycleEnd = true; properties.NextCycleKey = " next ";
        Assert.Equal("next", d.Definition.NextCycleKey);
        d.History.Undo(); Assert.Null(d.Definition.NextCycleKey);
        d.History.Undo(); Assert.False(d.Definition.HideTileOnCycleEnd);
        d.History.Undo(); Assert.Equal(CycleType.Repeating, d.Definition.CycleType);
        d.History.Undo(); Assert.Equal(.1, d.Definition.ThrottleTime);
        properties.ThrottleTime = .1; Assert.True(d.History.CanRedo);
    }

    [Fact]
    public void SpriteInstancePropertiesAndRelatedAssignmentsRestorePersistentIdentity()
    {
        using var d = SpriteDocument.Create(_root);
        var first = d.AddSprite(); var second = d.DuplicateSprite(first);
        var id = first.Id;
        d.RemoveSprite(first); d.History.Undo(); Assert.Same(first, d.Definition.Sprites[0]); Assert.Equal(id, first.Id);
        Assert.NotEqual(id, second.Id);
        var properties = new SpriteProperties(first, d);
        properties.Position = new PointF(2, 3); properties.CollisionTop = 8; properties.Visible = false;
        d.History.Undo(); Assert.True(first.Visible);
        d.History.Undo(); Assert.Equal(0, first.AdjustCollisionArea.Top);
        d.History.Undo(); Assert.Equal(PointF.Empty, first.Position);
        var frame = new SpriteFrameDefinition { Tilesheet = "tiles", RegionName = "body", XTile = 3, YTile = 5 };
        Assert.True(d.AssignFrame(first, frame));
        Assert.False(d.AssignFrame(first, new() { Tilesheet = "tiles", RegionName = "body", XTile = 3, YTile = 5 }));
        d.History.Undo(); Assert.Null(first.Frame); d.History.Redo(); Assert.Same(frame, first.Frame);
        d.Edit(first, "Assign scene layer", () => { first.SceneId = "scene"; first.SceneLayerId = "layer"; });
        d.History.Undo(); Assert.Equal("", first.SceneId); Assert.Equal("", first.SceneLayerId);
        d.History.Redo(); Assert.Equal("scene", first.SceneId); Assert.Equal("layer", first.SceneLayerId);
        properties.AnimationKey = "walk"; properties.StartAnimation = true;
        d.Edit(first, "Clear animation", () => { first.AnimationKey = null; first.StartAnimation = false; });
        d.History.Undo(); Assert.Equal("walk", first.AnimationKey); Assert.True(first.StartAnimation);
    }

    [Fact]
    public void AudioResourcesAndDescriptorsRetainSourceKindsOrderingAndProperties()
    {
        using var d = AudioDocument.Create(_root);
        var loose = d.AddLooseFile(Path.Combine(_root, "sound.wav"));
        var packed = d.AddPackedAudio(Path.Combine(_root, "sound.gaf"), "folder/exact", ".ogg");
        var uri = d.AddUri("stream", "https://example.test/a.ogg");
        d.Remove(packed); d.History.Undo(); Assert.Same(packed, d.Definition.Resources[1]);
        Assert.Equal(AudioResourceSourceKind.PackedAsset, packed.SourceKind); Assert.Equal("folder/exact", packed.AssetEntryName);
        var adapter = new AudioResourceProperties(d, loose);
        TypeDescriptor.GetProperties(adapter)["Volume"]!.SetValue(adapter, .5f);
        Assert.Equal(.5f, loose.Volume); d.History.Undo(); Assert.Equal(1, loose.Volume);
        d.History.Redo(); Assert.Equal(.5f, loose.Volume);
        d.Remove(uri); d.History.Undo(); Assert.Same(uri, d.Definition.Resources[2]);
        d.Save(Path.Combine(_root, "moved", "audio.gsnd"), true);
        Assert.Equal("https://example.test/a.ogg", uri.SourceUri);
        Assert.Equal(AudioResourceSourceKind.Uri, uri.SourceKind);
        Assert.False(File.Exists(Path.Combine(_root, "sound.wav")));
    }

    [Fact]
    public void RemovedSourceOriginsSurviveSaveAsForAnimationAndSprite()
    {
        using var animation = AnimationDocument.Create(_root);
        var archive = Path.Combine(_root, "references.gaf");
        animation.AddPackedTilesheetSource("tiles", archive, "nested/typed");
        var reference = animation.Definition.TilesheetSources[0];
        animation.RemoveTilesheetSource("tiles");
        animation.Save(Path.Combine(_root, "new", "animation.gani"), true);
        animation.History.Undo(); Assert.Same(reference, animation.Definition.TilesheetSources[0]);
        Assert.Equal(archive, animation.ResolveReferencePath(reference.AssetsFilePath!)); Assert.Equal("nested/typed", reference.AssetEntryName);
        using var sprite = SpriteDocument.Create(_root);
        sprite.AddPackedTilesheetSource("tiles", archive, "tiles/exact");
        var original = sprite.Definition.TilesheetSources[0];
        sprite.SetLooseTilesheetSource("tiles", Path.Combine(_root, "replacement.gts"));
        sprite.AddPackedAnimationSource("walk", archive, "animations/exact");
        sprite.AddPackedSceneSource("scene", archive, "scenes/exact");
        sprite.Save(Path.Combine(_root, "other", "sprites.gspr"), true);
        sprite.History.Undo(); sprite.History.Undo(); sprite.History.Undo();
        Assert.Same(original, sprite.Definition.TilesheetSources[0]);
        Assert.Equal(archive, sprite.ResolveReferencePath(original.AssetsFilePath!)); Assert.Equal("tiles/exact", original.AssetEntryName);
    }

    [Fact]
    public void AudioPropertyReferenceReplayUsesCurrentSaveLocationAndUriSemantics()
    {
        using var d = AudioDocument.Create(_root);
        var original = Path.Combine(_root, "original.wav");
        var replacement = Path.Combine(_root, "replacement.wav");
        var resource = d.AddLooseFile(original);
        d.EditResource(resource, "Replace audio", () => resource.FilePath = replacement);
        d.Save(Path.Combine(_root, "new", "audio.gsnd"), true);
        d.History.Undo(); Assert.Equal(original, Path.GetFullPath(resource.FilePath!, d.BaseDirectory));
        d.History.Redo(); Assert.Equal(replacement, Path.GetFullPath(resource.FilePath!, d.BaseDirectory));
        d.Remove(resource); d.Save(Path.Combine(_root, "third", "audio.gsnd"), true);
        d.History.Undo(); Assert.Same(resource, Assert.Single(d.Definition.Resources));
        Assert.Equal(replacement, Path.GetFullPath(resource.FilePath!, d.BaseDirectory));
    }

    [Fact]
    public void EveryEditableSpriteDescriptorCapturesOldValuesAndSuppressesUnchangedAssignments()
    {
        using var document = SpriteDocument.Create(_root);
        var sprite = document.AddSprite();
        var adapter = new SpriteProperties(sprite, document);
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(adapter))
        {
            if (property.IsReadOnly) continue;
            object? before = property.GetValue(adapter);
            object next = property.PropertyType == typeof(string) ? "edited-" + property.Name :
                property.PropertyType == typeof(bool) ? !(bool)before! :
                property.PropertyType == typeof(int) ? (int)before! + 1 :
                property.PropertyType == typeof(float) ? (float)before! + 1f :
                property.PropertyType == typeof(PointF) ? new PointF(4, 5) :
                property.PropertyType == typeof(Size) ? new Size(10, 11) :
                property.PropertyType.IsEnum ? Enum.GetValues(property.PropertyType).Cast<object>().First(value => !Equals(value, before)) :
                throw new InvalidOperationException("Add regression coverage for " + property.Name);
            document.History.Clear();
            property.SetValue(adapter, next); Assert.True(document.History.CanUndo, property.Name);
            document.History.Undo(); Assert.Equal(before, property.GetValue(adapter));
            document.History.Redo(); Assert.Equal(next, property.GetValue(adapter));
            document.History.Clear(); property.SetValue(adapter, next);
            Assert.False(document.History.CanUndo, property.Name);
        }
    }

    [Fact]
    public void SpriteSourceRemovalAndReplacementPreserveExactMemberMetadata()
    {
        using var document = SpriteDocument.Create(_root);
        var archive = Path.Combine(_root, "content.gaf");
        document.AddPackedAnimationSource("Walk", archive, "same/base");
        var animation = document.Definition.AnimationSources[0];
        document.AddPackedSceneSource("Scene", archive, "same/base");
        var scene = document.Definition.SceneSources[0];
        document.SetLooseAnimationSource("walk", Path.Combine(_root, "animation.gani"));
        document.History.Undo(); Assert.Same(animation, document.Definition.AnimationSources[0]);
        Assert.Equal("Walk", animation.AnimationKey); Assert.Equal("same/base", animation.AssetEntryName);
        document.SetLooseSceneSource("Scene", Path.Combine(_root, "scene.gscn"));
        document.History.Undo(); Assert.Same(scene, document.Definition.SceneSources[0]);
        document.RemoveAnimationSource("WALK"); document.History.Undo(); Assert.Same(animation, document.Definition.AnimationSources[0]);
        document.RemoveSceneSource("Scene"); document.History.Undo(); Assert.Same(scene, document.Definition.SceneSources[0]);
        document.SetLooseSceneSource("Scene", Path.Combine(_root, "scene.gscn"));
        document.History.Clear();
        document.SetLooseSceneSource("Scene", Path.Combine(_root, "scene.gscn")); Assert.False(document.History.CanUndo);
    }

    [Theory]
    [InlineData("gani")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void RemovedImplicitPackedReferencesRetainTheirOriginalArchiveAfterSaveAs(string format)
    {
        string archivePath = Path.Combine(_root, "implicit.gaf");
        dynamic seed = Create(format);
        using var seedOwner = (IDisposable)seed;
        if (format == "gsnd") seed.AddPackedAudio(archivePath, "typed/dependency", ".ogg");
        else seed.AddPackedTilesheetSource("tiles", archivePath, "typed/dependency");
        if (format == "gsnd") seed.Definition.Resources[0].AssetsFilePath = null;
        else seed.Definition.TilesheetSources[0].AssetsFilePath = null;
        using var archive = AssetsFile.LoadOrCreate(archivePath, null, false, register: false);
        var bytes = (byte[])seed.SerializePacked();
        archive.Add(format == "gani" ? AssetTypes.AnimationDefinition : format == "gspr" ? AssetTypes.SpriteDefinition : AssetTypes.AudioDefinition,
            "typed/document", () => new MemoryStream(bytes));
        archive.Save();
        dynamic document = format switch
        {
            "gani" => (object)AnimationDocument.Open(archive, "typed/document"),
            "gspr" => SpriteDocument.Open(archive, "typed/document"),
            _ => AudioDocument.Open(archive, "typed/document")
        };
        using var owner = (IDisposable)document;
        dynamic reference = format == "gsnd" ? document.Definition.Resources[0] : document.Definition.TilesheetSources[0];
        if (format == "gsnd") document.Remove(reference);
        else document.RemoveTilesheetSource("tiles");
        string destination = Path.Combine(_root, "elsewhere", "loose." + format);
        document.Save(destination, true);
        EditHistory history = document.History;
        history.Undo();
        Assert.Equal(archivePath, Path.GetFullPath((string)reference.AssetsFilePath, (string)document.BaseDirectory));
        Assert.Equal("typed/dependency", (string)reference.AssetEntryName);
        Assert.Equal(destination, (string)document.FilePath); Assert.Null((object?)document.PackedLocation);
        Assert.Same((object)reference, format == "gsnd" ? (object)document.Definition.Resources[0] : (object)document.Definition.TilesheetSources[0]);
    }
}
