using System.ComponentModel;
using Gondwana.Tooling.Audio.Editing;

namespace Gondwana.Tooling.Audio.WinForms.Tests;

public sealed partial class AudioEditorTests
{
    [Fact]
    public void GroupedResourceReplayRefreshesListLocationsAndPropertyBindings() => RunSta(() =>
    {
        using var document = AudioDocument.Create(Path.GetTempPath());
        using var host = new Form { Opacity = 0, ShowInTaskbar = false, Size = new Size(1100, 800) };
        using var editor = new AudioEditorControl(document);
        host.Controls.Add(editor); host.Show(); Application.DoEvents();
        editor.AddLooseFiles([Path.Combine(Path.GetTempPath(), "one.wav"), Path.Combine(Path.GetTempPath(), "two.ogg")]);
        Application.DoEvents();
        var list = Descendants(editor).OfType<ListView>().Single();
        var grid = Descendants(editor).OfType<PropertyGrid>().Single();
        Assert.Equal(2, list.Items.Count);
        var resource = document.Definition.Resources[0];
        document.History.Undo(); Application.DoEvents(); Assert.Empty(list.Items); Assert.Null(grid.SelectedObject);
        Assert.False(document.History.CanUndo);
        document.History.Redo(); Application.DoEvents(); Assert.Same(resource, list.Items[0].Tag);
        list.Items[0].Selected = true; Application.DoEvents();
        var adapter = grid.SelectedObject!;
        var descriptors = TypeDescriptor.GetProperties(adapter);
        descriptors["Key"]!.SetValue(adapter, "renamed"); Application.DoEvents(); Assert.Equal("renamed", list.Items[0].Text);
        document.History.Undo(); Application.DoEvents(); Assert.Equal("one", list.Items[0].Text);
        string location = list.Items[0].SubItems[2].Text;
        descriptors["FilePath"]!.SetValue(adapter, "replacement.wav"); Application.DoEvents(); Assert.NotEqual(location, list.Items[0].SubItems[2].Text);
        document.History.Undo(); Application.DoEvents(); Assert.Equal(location, list.Items[0].SubItems[2].Text);
        document.Remove(resource); Application.DoEvents(); document.History.Undo(); Application.DoEvents();
        Assert.Same(resource, list.Items[0].Tag);
    });
}
