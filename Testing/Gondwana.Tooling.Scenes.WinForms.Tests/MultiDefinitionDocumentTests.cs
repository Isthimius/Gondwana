using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Scenes.Editing;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

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
            var path = Path.Combine(root, "members.gscn");
            var first = new SceneDefinition { ID = "first" };
            var second = new SceneDefinition { ID = "second" };
            SceneDefinitionSerializer.SaveAll(path, [first, second]);
            var document = SceneDocument.Open(path);
            var original = document.Definition;
            document.SelectDefinition(document.Definitions[1]);
            Assert.False(document.IsDirty);
            Assert.Same(original, document.Definitions[0]);
            Assert.Throws<InvalidDataException>(() => document.RenameDefinition("first"));
            var duplicate = document.DuplicateDefinition();
            Assert.Equal(3, document.Definitions.Count);
            Assert.NotEqual("second", duplicate.ID);
            document.AddDefinition();
            Assert.True(document.RemoveDefinition(duplicate));
            document.SelectDefinition(original);
            var destination = Path.Combine(root, "nested", "members.gscn");
            document.Save(destination, allowInvalid: true);
            Assert.False(document.IsDirty);
            Assert.Same(original, document.SelectedDefinition);
            Assert.Equal(3, SceneDefinitionSerializer.LoadAll(destination).Count);
            Assert.Equal("second", document.Definitions[1].ID);
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
            var document = SceneDocument.Create(root);
            document.Definition.ID = "first";
            document.AddDefinition();
            foreach (var member in document.Definitions)
            {
                member.Source = SceneDefinitionSource.LooseDefinitionFile(Path.Combine(root, "original.gscn"));
                member.TilesheetSources.Add(new() { Tilesheet = "terrain", GtsPath = "terrain.gts" });
            }
            var destination = Path.Combine(root, "nested", "members.gscn");
            document.Save(destination, allowInvalid: true);
            document.Save(destination, allowInvalid: true);
            var json = File.ReadAllText(destination);
            Assert.DoesNotContain(".tmp", json);
            Assert.DoesNotContain("GscnFilePath", json);
            Assert.DoesNotContain(root.Replace("\\", "\\\\"), json);
            var reloaded = SceneDocument.Open(destination);
            Assert.Equal(2, reloaded.Definitions.Count);
            foreach (var member in reloaded.Definitions)
            {
                Assert.Equal(Path.Combine(root, "terrain.gts"), Path.GetFullPath(member.TilesheetSources[0].GtsPath!, reloaded.BaseDirectory));
                Assert.Equal(destination, member.Source.GscnFilePath);
            }
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(destination)!, "*.tmp"));
        }
        finally { Directory.Delete(root, true); }
    }
}
