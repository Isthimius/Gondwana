using Gondwana.Drawing.Animation.GANI;
using Gondwana.Tooling.Animations.Editing;

namespace Gondwana.Tooling.Animations.WinForms.Tests;

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
            var path = Path.Combine(root, "members.gani");
            var first = new AnimationDefinition { Key = "first" };
            var second = new AnimationDefinition { Key = "second" };
            AnimationDefinitionSerializer.SaveAll(path, [first, second]);
            var document = AnimationDocument.Open(path);
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
            var destination = Path.Combine(root, "nested", "members.gani");
            document.Save(destination, allowInvalid: true);
            Assert.False(document.IsDirty);
            Assert.Same(original, document.SelectedDefinition);
            Assert.Equal(3, AnimationDefinitionSerializer.LoadAll(destination).Count);
            Assert.Equal("second", document.Definitions[1].Key);
        }
        finally { Directory.Delete(root, true); }
    }
}
