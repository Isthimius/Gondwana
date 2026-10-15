using System.Text;
using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gondwana.Tests;

/// <summary>Cross-format contracts for physical documents and logical member identity.</summary>
[Collection("Global engine state")]
public sealed class DefinitionDocumentTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DefinitionDocuments-" + Guid.NewGuid());

    /// <summary>Creates an isolated content directory.</summary>
    public DefinitionDocumentTests() => Directory.CreateDirectory(_root);

    /// <inheritdoc/>
    public void Dispose() { AssetsFile.ClearAll(); Directory.Delete(_root, true); }

    private sealed record Format(string Extension, string Identity, AssetTypes Type,
        Func<string, object[]> Parse, Func<string, object> Singleton,
        Func<string, string?, object> Loose, Func<Stream, string?, object> Stream,
        Func<AssetsFile, string, string?, object> Packed,
        Func<AssetsFile, string, object[]> PackedAll,
        Action<string, object[]> Save, Func<object, DefinitionLoadStamp?> Stamp);

    private static Format Get(string format) => format switch
    {
        "gts" => new(format, "Name", AssetTypes.TilesheetDefinition,
            json => TilesheetDefinitionSerializer.FromDocumentJson(json).Definitions.Cast<object>().ToArray(),
            json => TilesheetDefinitionSerializer.FromJson(json),
            (path, key) => TilesheetDefinitionSerializer.Load(path, key),
            (stream, key) => TilesheetDefinitionSerializer.Load(stream, key),
            (archive, entry, key) => TilesheetDefinitionSerializer.Load(archive, entry, key),
            (archive, entry) => TilesheetDefinitionSerializer.LoadAll(archive, entry).Cast<object>().ToArray(),
            (path, members) => TilesheetDefinitionSerializer.SaveAll(path, members.Cast<TilesheetDefinition>()),
            member => ((TilesheetDefinition)member).LoadStamp),
        "gani" => new(format, "Key", AssetTypes.AnimationDefinition,
            json => AnimationDefinitionSerializer.FromDocumentJson(json).Definitions.Cast<object>().ToArray(),
            json => AnimationDefinitionSerializer.FromJson(json),
            (path, key) => AnimationDefinitionSerializer.Load(path, key),
            (stream, key) => AnimationDefinitionSerializer.Load(stream, key),
            (archive, entry, key) => AnimationDefinitionSerializer.Load(archive, entry, key),
            (archive, entry) => AnimationDefinitionSerializer.LoadAll(archive, entry).Cast<object>().ToArray(),
            (path, members) => AnimationDefinitionSerializer.SaveAll(path, members.Cast<AnimationDefinition>()),
            member => ((AnimationDefinition)member).LoadStamp),
        "gscn" => new(format, "ID", AssetTypes.SceneDefinition,
            json => SceneDefinitionSerializer.FromDocumentJson(json).Definitions.Cast<object>().ToArray(),
            json => SceneDefinitionSerializer.FromJson(json),
            (path, key) => SceneDefinitionSerializer.Load(path, key),
            (stream, key) => SceneDefinitionSerializer.Load(stream, key),
            (archive, entry, key) => SceneDefinitionSerializer.Load(archive, entry, key),
            (archive, entry) => SceneDefinitionSerializer.LoadAll(archive, entry).Cast<object>().ToArray(),
            (path, members) => SceneDefinitionSerializer.SaveAll(path, members.Cast<SceneDefinition>()),
            member => ((SceneDefinition)member).LoadStamp),
        "gsnd" => new(format, "Key", AssetTypes.AudioDefinition,
            json => AudioDefinitionSerializer.FromDocumentJson(json).Definitions.Cast<object>().ToArray(),
            json => AudioDefinitionSerializer.FromJson(json),
            (path, key) => AudioDefinitionSerializer.Load(path, key),
            (stream, key) => AudioDefinitionSerializer.Load(stream, key),
            (archive, entry, key) => AudioDefinitionSerializer.Load(archive, entry, key),
            (archive, entry) => AudioDefinitionSerializer.LoadAll(archive, entry).Cast<object>().ToArray(),
            (path, members) => AudioDefinitionSerializer.SaveAll(path, members.Cast<AudioDefinition>()),
            member => ((AudioDefinition)member).LoadStamp),
        "gspr" => new(format, "Key", AssetTypes.SpriteDefinition,
            json => SpriteDefinitionSerializer.FromDocumentJson(json).Definitions.Cast<object>().ToArray(),
            json => SpriteDefinitionSerializer.FromJson(json),
            (path, key) => SpriteDefinitionSerializer.Load(path, key),
            (stream, key) => SpriteDefinitionSerializer.Load(stream, key),
            (archive, entry, key) => SpriteDefinitionSerializer.Load(archive, entry, key),
            (archive, entry) => SpriteDefinitionSerializer.LoadAll(archive, entry).Cast<object>().ToArray(),
            (path, members) => SpriteDefinitionSerializer.SaveAll(path, members.Cast<SpriteDefinition>()),
            member => ((SpriteDefinition)member).LoadStamp),
        _ => throw new ArgumentException(format)
    };

    private static JObject Member(Format format, string key) => new() { [format.Identity] = key };
    private static string Envelope(params JObject[] members) => new JObject { ["Definitions"] = new JArray(members) }.ToString();

    /// <summary>Verifies legacy, canonical, selector, singleton, ordinal identity, and malformed-document contracts.</summary>
    /// <param name="extension">The native format.</param>
    [Theory]
    [InlineData("gts")]
    [InlineData("gani")]
    [InlineData("gscn")]
    [InlineData("gsnd")]
    [InlineData("gspr")]
    public void ParsingContracts(string extension)
    {
        var format = Get(extension);
        var first = Member(format, "first");
        Assert.Single(format.Parse(first.ToString()));
        Assert.Single(format.Parse(Envelope(first)));
        Assert.NotNull(format.Singleton(first.ToString()));
        Assert.NotNull(format.Singleton(Envelope(first)));
        var json = Envelope(first, Member(format, "second"));
        Assert.Equal(2, format.Parse(json).Length);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        Assert.Equal("second", (string?)JObject.FromObject(format.Stream(stream, "second"))[format.Identity]);
        Assert.True(stream.CanRead);
        stream.Position = 0;
        Assert.Contains("missing", Assert.Throws<InvalidDataException>(() => format.Stream(stream, "missing")).Message);
        Assert.Contains("LoadAll", Assert.Throws<InvalidDataException>(() => format.Singleton(json)).Message);
        Assert.Throws<InvalidDataException>(() => format.Singleton("{\"Definitions\":[]}"));
        Assert.Equal(2, format.Parse(Envelope(first, Member(format, "FIRST"))).Length);
        foreach (var malformed in new[] { "null", "[]", "{\"Definitions\":null}", "{\"Definitions\":[null]}", "{\"Definitions\":{}}", "{\"Definitions\":[{}]}", "{\"Definitions\":[],\"Key\":\"mixed\"}", Envelope(first, first) })
            Assert.Throws<InvalidDataException>(() => format.Parse(malformed));
    }

    /// <summary>Verifies round-trip, non-mutating saves, and separate physical/member provenance for all formats.</summary>
    /// <param name="extension">The native format.</param>
    [Theory]
    [InlineData("gts")]
    [InlineData("gani")]
    [InlineData("gscn")]
    [InlineData("gsnd")]
    [InlineData("gspr")]
    public void SaveAndProvenanceContracts(string extension)
    {
        var format = Get(extension);
        var members = format.Parse(Envelope(Member(format, "first"), Member(format, "second")));
        var before = JsonConvert.SerializeObject(members);
        var path = Path.Combine(_root, "shared." + extension);
        format.Save(path, members);
        Assert.Equal(before, JsonConvert.SerializeObject(members));
        Assert.Equal(2, ((JArray)JObject.Parse(File.ReadAllText(path))["Definitions"]!).Count);
        Assert.Throws<InvalidDataException>(() => format.Loose(path, null));
        var selected = format.Loose(path, "second");
        var provenance = format.Stamp(selected)!.Materialized(selected, () => selected)!;
        Assert.Equal(path, provenance.FilePath);
        Assert.Equal("second", provenance.DefinitionIdentity);
        Assert.Null(provenance.AssetsFilePath);
        Assert.True(provenance.Matches(selected));
        using var anonymous = new MemoryStream(File.ReadAllBytes(path));
        Assert.Null(format.Stamp(format.Stream(anonymous, "second")));
        var archivePath = Path.Combine(_root, "content.gaf");
        using var archive = AssetsFile.LoadOrCreate(archivePath);
        using var bytes = File.OpenRead(path);
        archive.Add(format.Type, "shared." + extension, bytes);
        archive.Save();
        bytes.Dispose();
        Assert.Throws<InvalidDataException>(() => format.Packed(archive, "shared." + extension, null));
        Assert.Throws<InvalidDataException>(() => format.Packed(archive, "shared." + extension, "missing"));
        foreach (var member in format.PackedAll(archive, "shared." + extension))
        {
            var stamp = format.Stamp(member)!.Materialized(member, () => member)!;
            Assert.Equal(archivePath, stamp.AssetsFilePath);
            Assert.Equal("shared." + extension, stamp.AssetEntryName);
            Assert.Equal((string?)JObject.FromObject(member)[format.Identity], stamp.DefinitionIdentity);
            Assert.Null(stamp.FilePath);
        }
        format.Save(path, [members[0]]);
        Assert.Single((JArray)JObject.Parse(File.ReadAllText(path))["Definitions"]!);
        Assert.Equal("first", (string?)JObject.FromObject(format.Loose(path, null))[format.Identity]);
    }

    /// <summary>Legacy audio and sprite roots infer stable keys only outside the canonical envelope.</summary>
    [Fact]
    public void LegacyGroupIdentityInference()
    {
        foreach (var extension in new[] { "gsnd", "gspr" })
        {
            var format = Get(extension);
            var path = Path.Combine(_root, "legacy." + extension);
            File.WriteAllText(path, "{}");
            Assert.Equal("legacy", (string?)JObject.FromObject(format.Loose(path, null))["Key"]);
            Assert.Throws<InvalidDataException>(() => format.Parse("{\"Definitions\":[{}]}"));
        }
    }

    /// <summary>Save As rebases every member while preserving unrelated data and in-memory references.</summary>
    [Fact]
    public void SaveAsRebasesEveryFormat()
    {
        var original = Path.Combine(_root, "old");
        var destination = Path.Combine(_root, "new");
        Directory.CreateDirectory(original);
        foreach (var extension in new[] { "gts", "gani", "gscn", "gsnd", "gspr" })
        {
            var format = Get(extension);
            var members = new[] { Member(format, "first"), Member(format, "second") };
            foreach (var member in members)
            {
                if (extension == "gts") member["Image"] = new JObject { ["FilePath"] = "image.png" };
                else if (extension == "gsnd") member["Resources"] = new JArray(new JObject { ["Key"] = "sound", ["FilePath"] = "sound.wav" });
                else member["TilesheetSources"] = new JArray(new JObject { ["Tilesheet"] = "terrain", ["GtsPath"] = "tiles.gts" });
            }
            var path = Path.Combine(original, "all." + extension);
            File.WriteAllText(path, Envelope(members));
            var loaded = new[] { format.Loose(path, "first"), format.Loose(path, "second") };
            var before = JsonConvert.SerializeObject(loaded);
            var target = Path.Combine(destination, "all." + extension);
            format.Save(target, loaded);
            Assert.Equal(before, JsonConvert.SerializeObject(loaded));
            foreach (var member in (JArray)JObject.Parse(File.ReadAllText(target))["Definitions"]!)
            {
                var reference = extension == "gts" ? member["Image"]!["FilePath"] :
                    extension == "gsnd" ? member["Resources"]![0]!["FilePath"] : member["TilesheetSources"]![0]!["GtsPath"];
                var leaf = extension == "gts" ? "image.png" : extension == "gsnd" ? "sound.wav" : "tiles.gts";
                Assert.Equal(Path.Combine(original, leaf), Path.GetFullPath((string)reference!, destination));
            }
        }
    }
}
