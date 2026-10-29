using System.ComponentModel;
using Gondwana.Drawing;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

public sealed partial class SceneEditorTests
{
    [Theory]
    [InlineData(DialogResult.OK)]
    [InlineData(DialogResult.Cancel)]
    public void CollectionEditorCapturesInPlaceChangesAndHonorsCancel(DialogResult result) => RunSta(() =>
    {
        using var document = SceneDocument.Create(Path.GetTempPath());
        var groups = document.Definition.CollisionGroups;
        var property = TypeDescriptor.GetProperties(document.Definition)["CollisionGroups"]!;
        var wrapperType = typeof(ScenePropertyView).GetNestedType("TrackedEditor", System.Reflection.BindingFlags.NonPublic)!;
        var wrapper = (System.Drawing.Design.UITypeEditor)Activator.CreateInstance(wrapperType,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, [document, document.Definition, property, new MutatingCollectionEditor()], null)!;
        wrapper.EditValue(null, new DialogService(result), groups);
        if (result == DialogResult.Cancel)
        {
            Assert.Empty(groups); Assert.False(document.History.CanUndo);
        }
        else
        {
            Assert.Equal("new group", Assert.Single(groups));
            document.History.Undo(); Assert.Empty(groups);
            document.History.Redo(); Assert.Equal("new group", Assert.Single(groups));
        }
        Assert.False(document.History.InTransaction);
    });

    private sealed class MutatingCollectionEditor : System.Drawing.Design.UITypeEditor
    {
        public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider provider, object? value)
        {
            ((List<string>)value!).Add("new group");
            using var dialog = new Form();
            ((System.Windows.Forms.Design.IWindowsFormsEditorService)provider.GetService(typeof(System.Windows.Forms.Design.IWindowsFormsEditorService))!).ShowDialog(dialog);
            return value;
        }
    }

    private sealed class DialogService(DialogResult result) : IServiceProvider, System.Windows.Forms.Design.IWindowsFormsEditorService
    {
        public object? GetService(Type serviceType) => serviceType == typeof(System.Windows.Forms.Design.IWindowsFormsEditorService) ? this : null;
        public void CloseDropDown() { }
        public void DropDownControl(Control? control) { }
        public DialogResult ShowDialog(Form dialog) => result;
    }

    private static bool InvokeHistoryShortcut(Control editor, Keys keys) =>
        (bool)editor.GetType().GetMethod("ProcessCmdKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(editor, [new Message(), keys])!;

    [Fact]
    public void HistoryShortcutsRestoreSparseTilesAndRefreshPropertyValues() => RunSta(() =>
    {
        using var document = SceneDocument.Create(Path.GetTempPath());
        var layer = document.AddLayer();
        using var editor = new SceneEditorControl(document);
        using var host = new Form { Size = new(1500, 950), Opacity = 0, ShowInTaskbar = false };
        host.Controls.Add(editor); host.Show(); Application.DoEvents();
        var adapter = Descendants(editor).OfType<PropertyGrid>().Select(g => g.SelectedObject).OfType<TilePropertyAdapter>().Single();
        adapter.Transform = TileTransform.Rotate90;
        Descendants(editor).OfType<ScenePreviewControl>().Single().Focus();
        Assert.True(InvokeHistoryShortcut(editor, Keys.Control | Keys.Z));
        Application.DoEvents(); Assert.Empty(layer.Tiles); Assert.Equal(TileTransform.Identity, adapter.Transform);
        Assert.True(InvokeHistoryShortcut(editor, Keys.Control | Keys.Y));
        Application.DoEvents(); Assert.Equal(TileTransform.Rotate90, adapter.Transform);
        var view = new ScenePropertyView(document, layer);
        var width = TypeDescriptor.GetProperties(view)["Columns"]!;
        width.SetValue(view, 25); Assert.Equal(25, layer.Columns);
        document.History.Undo(); Application.DoEvents(); Assert.Equal(10, width.GetValue(view));
        using var input = new TextBox(); host.Controls.Add(input); input.BringToFront(); input.Focus();
        Assert.False(UndoRedoMenu.TryShortcut(editor, Keys.Control | Keys.Z, document.History, editor.CommitEdits));
        Assert.Single(layer.Tiles);
        using var menu = new UndoRedoMenu(() => document.History, editor.CommitEdits);
        Assert.True(menu.Menu.DropDownItems[1].Enabled);
    });
}
