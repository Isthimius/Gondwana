using Gondwana.Tooling.Tilesheets.Editing;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Tilesheets.WinForms.Tests;

public sealed partial class EditorInteractionTests
{
    private static bool InvokeHistoryShortcut(Control editor, Keys keys) =>
        (bool)editor.GetType().GetMethod("ProcessCmdKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(editor, [new Message(), keys])!;

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern nint ReplaceSelection(nint handle, int message, nint canUndo, string text);
    [Fact]
    public void HistoryShortcutsRefreshPropertiesAndPreserveLocalTextUndo() => RunSta(() =>
    {
        var document = TilesheetDocument.Create(Path.GetTempPath());
        using var host = Show(document);
        var editor = host.Editor;
        var fields = PropertyFields.Definition(document, document.MarkChanged);
        var alpha = fields.GetProperties()["PremultiplyAlpha"]!;
        bool original = document.Definition.PremultiplyAlpha;
        alpha.SetValue(fields, !original);
        Application.DoEvents();
        Field<ImageViewport>(editor, "_viewport").Focus();
        Assert.True(InvokeHistoryShortcut(editor, Keys.Control | Keys.Z));
        Application.DoEvents();
        Assert.Equal(original, alpha.GetValue(fields));
        Assert.True(InvokeHistoryShortcut(editor, Keys.Control | Keys.Y));
        Assert.Equal(!original, document.Definition.PremultiplyAlpha);
        using var input = new TextBox();
        host.Controls.Add(input); input.BringToFront(); input.Focus();
        input.Text = "first"; input.SelectAll();
        ReplaceSelection(input.Handle, 0x00C2, 1, "second");
        Assert.True(input.CanUndo);
        Assert.False(UndoRedoMenu.TryShortcut(editor, Keys.Control | Keys.Z, document.History, editor.CommitEdits));
        input.Undo(); Assert.Equal("first", input.Text);
        Assert.Equal(!original, document.Definition.PremultiplyAlpha);
        using var menu = new UndoRedoMenu(() => document.History, editor.CommitEdits);
        Assert.True(menu.Menu.DropDownItems[0].Enabled);
        Assert.Contains("alpha", menu.Menu.DropDownItems[0].Text, StringComparison.OrdinalIgnoreCase);
    });
}
