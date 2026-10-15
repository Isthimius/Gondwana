using Gondwana.Audio.GSND;
using Gondwana.Tooling.Audio.Editing;

namespace Gondwana.Tooling.Audio.WinForms.Tests;

/// <summary>Verifies the editor owns the physical document independently of its active member.</summary>
public sealed class MultiDefinitionDocumentTests
{
    /// <summary>Selection, duplication, removal and Save As retain every other logical member.</summary>
    [Fact]
    public void EditAndSavePreservesAllMembers()
    {
        var root = Path.Combine(Path.GetTempPath(), "MultiEditor-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "members.gsnd");
            var first = new AudioDefinition { Key = "first" };
            var second = new AudioDefinition { Key = "second" };
            AudioDefinitionSerializer.SaveAll(path, [first, second]);
            var document = AudioDocument.Open(path);
            var original = document.Definition;
            document.SelectDefinition(document.Definitions[1]);
            Assert.False(document.IsDirty);
            Assert.Same(original, document.Definitions[0]);
            Assert.Throws<InvalidDataException>(() => document.RenameDefinition("first"));
            var duplicate = document.DuplicateDefinition();
            Assert.Equal(3, document.Definitions.Count);
            Assert.NotEqual("second", duplicate.Key);
            document.AddDefinition();
            Assert.True(document.RemoveDefinition(duplicate));
            document.SelectDefinition(original);
            var destination = Path.Combine(root, "nested", "members.gsnd");
            document.Save(destination, allowInvalid: true);
            Assert.False(document.IsDirty);
            Assert.Same(original, document.SelectedDefinition);
            Assert.Equal(3, AudioDefinitionSerializer.LoadAll(destination).Count);
            Assert.Equal("second", document.Definitions[1].Key);
        }
        finally { Directory.Delete(root, true); }
    }
    /// <summary>Atomic Save As never persists staging names and preserves every member's dependency base.</summary>
    [Fact]
    public void AtomicSaveUsesFinalPathForEveryMember()
    {
        var root = Path.Combine(Path.GetTempPath(), "PortableEditor-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var document = AudioDocument.Create(root);
            document.Definition.Key = "first";
            document.AddDefinition();
            foreach (var member in document.Definitions)
            {
                member.Source = AudioDefinitionSource.LooseDefinitionFile(Path.Combine(root, "original.gsnd"));
                member.Resources.Add(new() { Key = "sound", FilePath = "sound.wav" });
            }
            var destination = Path.Combine(root, "nested", "members.gsnd");
            document.Save(destination, allowInvalid: true);
            document.Save(destination, allowInvalid: true);
            var json = File.ReadAllText(destination);
            Assert.DoesNotContain(".tmp", json);
            Assert.DoesNotContain("GsndFilePath", json);
            Assert.DoesNotContain(root.Replace("\\", "\\\\"), json);
            var reloaded = AudioDocument.Open(destination);
            Assert.Equal(2, reloaded.Definitions.Count);
            foreach (var member in reloaded.Definitions)
            {
                Assert.Equal(Path.Combine(root, "sound.wav"), Path.GetFullPath(member.Resources[0].FilePath!, reloaded.BaseDirectory));
                Assert.Equal(destination, member.Source.GsndFilePath);
            }
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(destination)!, "*.tmp"));
        }
        finally { Directory.Delete(root, true); }
    }
}
