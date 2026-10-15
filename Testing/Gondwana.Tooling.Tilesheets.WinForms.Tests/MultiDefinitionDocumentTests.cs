using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Tilesheets.Editing;

namespace Gondwana.Tooling.Tilesheets.WinForms.Tests;

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
            var path = Path.Combine(root, "members.gts");
            var first = new TilesheetDefinition { Name = "first" };
            var second = new TilesheetDefinition { Name = "second" };
            TilesheetDefinitionSerializer.SaveAll(path, [first, second]);
            var document = TilesheetDocument.Open(path);
            var original = document.Definition;
            document.SelectDefinition(document.Definitions[1]);
            Assert.False(document.IsDirty);
            Assert.Same(original, document.Definitions[0]);
            Assert.Throws<InvalidDataException>(() => document.RenameDefinition("first"));
            var duplicate = document.DuplicateDefinition();
            Assert.Equal(3, document.Definitions.Count);
            Assert.NotEqual("second", duplicate.Name);
            document.AddDefinition();
            Assert.True(document.RemoveDefinition(duplicate));
            document.SelectDefinition(original);
            var destination = Path.Combine(root, "nested", "members.gts");
            document.Save(destination, allowInvalid: true);
            Assert.False(document.IsDirty);
            Assert.Same(original, document.SelectedDefinition);
            Assert.Equal(3, TilesheetDefinitionSerializer.LoadAll(destination).Count);
            Assert.Equal("second", document.Definitions[1].Name);
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
            var document = TilesheetDocument.Create(root);
            document.Definition.Name = "first";
            document.AddDefinition();
            foreach (var member in document.Definitions)
            {
                member.Source = TilesheetDefinitionSource.LooseDefinitionFile(Path.Combine(root, "original.gts"));
                member.Image.FilePath = "art.png";
            }
            var destination = Path.Combine(root, "nested", "members.gts");
            document.Save(destination, allowInvalid: true);
            document.Save(destination, allowInvalid: true);
            var json = File.ReadAllText(destination);
            Assert.DoesNotContain(".tmp", json);
            Assert.DoesNotContain("GtsFilePath", json);
            Assert.DoesNotContain(root.Replace("\\", "\\\\"), json);
            var reloaded = TilesheetDocument.Open(destination);
            Assert.Equal(2, reloaded.Definitions.Count);
            foreach (var member in reloaded.Definitions)
            {
                Assert.Equal(Path.Combine(root, "art.png"), Path.GetFullPath(member.Image.FilePath!, reloaded.BaseDirectory));
                Assert.Equal(destination, member.Source.GtsFilePath);
            }
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(destination)!, "*.tmp"));
        }
        finally { Directory.Delete(root, true); }
    }
}
