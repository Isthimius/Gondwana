using Gondwana.Audio.GSND;
using Gondwana.Tooling.Audio.Editing;

namespace Gondwana.Tooling.Audio.WinForms.Tests;

/// <summary>Tests additive import, source isolation, selection, and resource paths.</summary>
public sealed class DefinitionImportTests
{
    /// <summary>Imports legacy, selected, or all members and preserves resource targets through Save As.</summary>
    /// <param name="mode">Zero imports a legacy singleton; one selects a member; two imports all.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ImportPreservesExistingMembersAndPaths(int mode)
    {
        var root = Path.Combine(Path.GetTempPath(), "DefinitionImport-" + Guid.NewGuid());
        var sourceDirectory = Path.Combine(root, "source");
        var destinationDirectory = Path.Combine(root, "destination");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(destinationDirectory);
        try
        {
            var sourcePath = Path.Combine(sourceDirectory, "source.gsnd");
            var first = new AudioDefinition { Key = "shared", Resources = [new() { Key = "sound", SourceUri = "audio/relative.ogg", FilePath = "images/atlas.png", AssetsFilePath = "packs/media.gaf", AssetEntryName = "audio" }] };
            var second = AudioDefinitionSerializer.FromJson(AudioDefinitionSerializer.ToJson(first));
            second.Key = "second";
            File.WriteAllText(sourcePath, mode == 0 ? AudioDefinitionSerializer.ToJson(first) :
                AudioDefinitionSerializer.ToDocumentJson(new() { Definitions = [second, first] }));
            var originalBytes = File.ReadAllBytes(sourcePath);
            var document = AudioDocument.Create(destinationDirectory);
            document.RenameDefinition("shared");
            var original = document.Definition;
            var originalJson = AudioDefinitionSerializer.ToJson(original);
            IReadOnlyList<AudioDefinition>? loaded = null;
            int count = document.ImportDefinitions(sourcePath, members =>
            {
                loaded = members;
                return mode == 1 ? [1] : Enumerable.Range(0, members.Count).ToArray();
            });
            Assert.Equal(mode == 2 ? 2 : 1, count);
            Assert.True(document.IsDirty);
            Assert.Same(original, document.Definitions[0]);
            Assert.Equal(originalJson, AudioDefinitionSerializer.ToJson(original));
            Assert.Same(document.Definitions[1], document.SelectedDefinition);
            Assert.Contains(document.Definitions, member => member.Key == "shared-2");
            Assert.DoesNotContain(document.Definitions, member => loaded!.Any(source => ReferenceEquals(member, source)));
            var member = document.Definitions.Last();
            Assert.Equal(Path.Combine(sourceDirectory, "images", "atlas.png"), Path.GetFullPath(member.Resources[0].FilePath!, document.BaseDirectory));
            Assert.Equal(Path.Combine(sourceDirectory, "packs", "media.gaf"), Path.GetFullPath(member.Resources[0].AssetsFilePath!, document.BaseDirectory));
            loaded![mode == 1 ? 1 : 0].Key = "source-mutated";
            Assert.DoesNotContain(document.Definitions, item => item.Key == "source-mutated");
            var saved = Path.Combine(destinationDirectory, "combined.gsnd");
            document.Save(saved, allowInvalid: true);
            var moved = Path.Combine(root, "save-as", "combined.gsnd");
            document.Save(moved, allowInvalid: true);
            var reopened = AudioDocument.Open(moved);
            Assert.Equal(count + 1, reopened.Definitions.Count);
            member = reopened.Definitions.Last();
            Assert.Equal(Path.Combine(sourceDirectory, "images", "atlas.png"), Path.GetFullPath(member.Resources[0].FilePath!, reopened.BaseDirectory));
            Assert.Equal(Path.Combine(sourceDirectory, "packs", "media.gaf"), Path.GetFullPath(member.Resources[0].AssetsFilePath!, reopened.BaseDirectory));
            Assert.Equal("audio/relative.ogg", member.Resources[0].SourceUri);
            Assert.Equal(originalBytes, File.ReadAllBytes(sourcePath));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>Reserves incoming names before generating suffixes for conflicts.</summary>
    [Fact]
    public void BatchKeepsAvailableIncomingNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "DefinitionImport-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "source.gsnd");
            AudioDefinitionSerializer.SaveAll(path,
                [new AudioDefinition { Key = "shared" }, new AudioDefinition { Key = "shared-2" }]);
            var document = AudioDocument.Create(root);
            document.RenameDefinition("shared");
            Assert.Equal(2, document.ImportDefinitions(path));
            Assert.Equal(new[] { "shared", "shared-3", "shared-2" }, document.Definitions.Select(member => member.Key));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>Cancel, no selection, and malformed inputs never mutate the destination.</summary>
    [Fact]
    public void CancelAndInvalidSourceAreAtomic()
    {
        var root = Path.Combine(Path.GetTempPath(), "DefinitionImport-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "source.gsnd");
            var document = AudioDocument.Create(root);
            document.Save(path, allowInvalid: true);
            var original = document.Definition;
            Assert.Equal(0, document.ImportDefinitions(path, _ => null));
            Assert.Equal(0, document.ImportDefinitions(path, _ => []));
            Assert.Throws<ArgumentException>(() => document.ImportDefinitions(path, _ => [99]));
            File.WriteAllText(path, "{\"Definitions\":[null]}");
            Assert.Throws<InvalidDataException>(() => document.ImportDefinitions(path));
            Assert.False(document.IsDirty);
            Assert.Same(original, document.SelectedDefinition);
            Assert.Single(document.Definitions);
        }
        finally { Directory.Delete(root, true); }
    }
}
