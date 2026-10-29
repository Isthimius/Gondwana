using System.Runtime.InteropServices;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Audio.WinForms;
using Gondwana.Tooling.Editing;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.Tilesheets.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    private static dynamic AuthoringModel(Control editor) => editor.GetType().GetProperty("Document")!.GetValue(editor)!;
    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] state);
    [DllImport("user32.dll")]
    private static extern bool SetKeyboardState(byte[] state);
    [DllImport("user32.dll")]
    private static extern nint GetFocus();
    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint window, int message, nint key, nint data);
    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
    private static extern nint ReplaceText(nint window, int message, nint canUndo, string value);

    // Exercise WinForms' message preprocessing starting at the actual focused HWND,
    // including child controls and docking panes, rather than invoking a host override.
    private static void RouteKey(Keys keys)
    {
        var saved = new byte[256];
        Assert.True(GetKeyboardState(saved));
        var state = new byte[256];
        if (keys.HasFlag(Keys.Control)) state[(int)Keys.ControlKey] = 0x80;
        if (keys.HasFlag(Keys.Shift)) state[(int)Keys.ShiftKey] = 0x80;
        Assert.True(SetKeyboardState(state));
        try
        {
            Assert.True(PostMessage(GetFocus(), 0x100, (nint)(keys & Keys.KeyCode), 0));
            Application.DoEvents();
        }
        finally { Assert.True(SetKeyboardState(saved)); }
        Application.DoEvents();
    }

    [Theory]
    [InlineData("gts")]
    [InlineData("gscn")]
    public void FocusedPaneRoutesHistoryMessages(string format) => RunSta(root =>
    {
        using var studio = Host(root);
        var child = studio.NewDocument(format);
        var original = child.Document.SelectedMember;
        if (child.Document.Editor is TilesheetEditorControl tiles) tiles.Document.RenameDefinition("changed");
        else ((SceneEditorControl)child.Document.Editor).Document.RenameDefinition("changed");
        var control = Descendants(child.Document.Editor).First(c => c.GetType().Name is "ImageViewport" or "ScenePreviewControl");
        Assert.True(control.Focus());
        RouteKey(Keys.Control | Keys.Z);
        Assert.Equal(original, child.Document.SelectedMember);
        RouteKey(Keys.Control | Keys.Y);
        Assert.Equal("changed", child.Document.SelectedMember);
    });

    [Theory]
    [InlineData("gts", false)]
    [InlineData("gscn", false)]
    [InlineData("gts", true)]
    [InlineData("gscn", true)]
    [InlineData("gani", false)]
    [InlineData("gspr", false)]
    [InlineData("gsnd", false)]
    [InlineData("gani", true)]
    [InlineData("gspr", true)]
    [InlineData("gsnd", true)]
    public void KeyboardAndMenuAreEquivalentFromNormalFocus(string format, bool standalone) => RunSta(root =>
    {
        var path = Path.Combine(root, "keys." + format);
        File.WriteAllText(path, NativeJson(format));
        Type editorType = format switch
        {
            "gts" => typeof(TilesheetEditorControl),
            "gscn" => typeof(SceneEditorControl),
            "gani" => typeof(AnimationEditorControl),
            "gspr" => typeof(SpriteEditorControl),
            _ => typeof(AudioEditorControl)
        };
        dynamic Model(Control control) => control.GetType().GetProperty("Document")!.GetValue(control)!;
        using var studio = standalone ? null : Host(root);
        using var shell = standalone ? (Form)Activator.CreateInstance(
            editorType.Assembly.GetTypes()
                .Single(t => t.Name == "MainForm"))! : null;
        Control editor;
        if (shell is not null)
        {
            shell.Show(); Application.DoEvents();
            shell.GetType().GetMethod("OpenDocument", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(shell, [path]);
            Application.DoEvents();
            editor = Descendants(shell).Where(c => c.GetType() == editorType).Single(c => (string)Model(c).FilePath == path);
        }
        else editor = studio!.OpenDocument(path).Document.Editor;
        var host = shell ?? studio!;
        var menu = host.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "&Edit");
        EditHistory history = Model(editor).History;
        string Name() => format == "gts" ? (string)Model(editor).Definition.Name : format == "gscn" ? (string)Model(editor).Definition.ID : (string)Model(editor).Definition.Key;
        void Rename(string name)
        {
            Model(editor).RenameDefinition(name);
        }
        var original = Name();
        var targets = Descendants(editor).Where(c => c.Visible && c.CanFocus &&
            (c is TreeView or ListView or ListBox || c is PropertyGrid { SelectedObject: not null } || c is ComboBox { DropDownStyle: ComboBoxStyle.DropDownList } ||
             c.GetType().Name is "ImageViewport" or "ScenePreviewControl")).ToArray();
        Assert.NotEmpty(targets);
        foreach (var target in targets)
        {
            Rename("changed");
            Application.DoEvents();
            if (!target.Visible || !target.CanFocus) continue;
            target.Focus();
            Assert.True(target.ContainsFocus, target.GetType().FullName);
            RouteKey(Keys.Control | Keys.Z);
            Assert.Equal(original, Name());
            RouteKey(Keys.Control | Keys.Y);
            Assert.Equal("changed", Name());
            RouteKey(Keys.Control | Keys.Z);
            RouteKey(Keys.Control | Keys.Shift | Keys.Z);
            Assert.Equal("changed", Name());
            menu.DropDownItems[0].PerformClick(); Assert.Equal(original, Name());
            menu.DropDownItems[1].PerformClick(); Assert.Equal("changed", Name());
            menu.DropDownItems[0].PerformClick(); Assert.Equal(original, Name());
            history.Clear();
        }
        // An active text editor keeps native undo; a single document shortcut
        // then undoes exactly one operation even when both editor and host route it.
        using var input = new TextBox { Dock = DockStyle.Top };
        editor.Controls.Add(input); input.BringToFront();
        Rename("first"); Rename("second"); Application.DoEvents();
        input.Focus(); input.Text = "before"; input.SelectAll();
        ReplaceText(input.Handle, 0xC2, 1, "after");
        Assert.True(input.CanUndo);
        RouteKey(Keys.Control | Keys.Z);
        Assert.Equal("before", input.Text); Assert.Equal("second", Name());
        var navigation = targets.First(c => c.Visible && c.CanFocus && c is not PropertyGrid);
        navigation.Focus();
        RouteKey(Keys.Control | Keys.Z); Assert.Equal("first", Name());
        RouteKey(Keys.Control | Keys.Y); Assert.Equal("second", Name());
        history.Clear();
        Assert.False(menu.DropDownItems[0].Enabled); Assert.False(menu.DropDownItems[1].Enabled);
        RouteKey(Keys.Control | Keys.Z); RouteKey(Keys.Control | Keys.Y);
        Assert.Equal("second", Name());

        if (editor is SpriteEditorControl spriteEditor) spriteEditor.SelectSprite(spriteEditor.Document.AddSprite());
        if (editor is AudioEditorControl audioEditor)
        {
            audioEditor.Document.AddUri("sound", "https://example.test/sound.ogg");
            Application.DoEvents();
            Descendants(editor).OfType<ListView>().Single().Items[0].Selected = true;
        }
        Application.DoEvents();
        string propertyName = format switch { "gts" => "Name", "gscn" => "ID", "gani" => "NextCycleKey", "gspr" => "Nickname", _ => "Key" };
        var grid = Descendants(editor).OfType<PropertyGrid>().First(g => g.SelectedObject is not null &&
            System.ComponentModel.TypeDescriptor.GetProperties(g.SelectedObject)[propertyName] is not null);
        var adapter = grid.SelectedObject!;
        var property = System.ComponentModel.TypeDescriptor.GetProperties(adapter)[propertyName]!;
        history.Clear(); Rename("ready"); Application.DoEvents();
        var originalValue = property.GetValue(adapter);
        grid.SelectedGridItem = GridItems(grid).Single(item => item.PropertyDescriptor?.Name == propertyName);
        grid.Focus();
        var valueEditor = Descendants(grid).OfType<TextBox>().Single(t => t.Visible);
        valueEditor.Focus();
        string oldText = valueEditor.Text;
        valueEditor.SelectAll(); ReplaceText(valueEditor.Handle, 0xC2, 1, "pending");
        Assert.True(valueEditor.Modified);
        RouteKey(Keys.Control | Keys.Z);
        Assert.Equal(oldText, valueEditor.Text);
        Assert.Equal(originalValue, property.GetValue(adapter));
        Assert.Equal("ready", Name());
        valueEditor.SelectAll(); ReplaceText(valueEditor.Handle, 0xC2, 1, "pending");
        menu.DropDownItems[0].PerformClick(); Application.DoEvents();
        Assert.Equal(originalValue, property.GetValue(adapter));
        Assert.Equal("ready", Name());
        menu.DropDownItems[1].PerformClick(); Application.DoEvents();
        Assert.Equal("pending", property.GetValue(adapter));
        menu.DropDownItems[0].PerformClick(); Application.DoEvents();
        history.MarkSaved();
    });

    private static IEnumerable<GridItem> GridItems(PropertyGrid grid)
    {
        var root = grid.SelectedGridItem;
        while (root.Parent is not null) root = root.Parent;
        IEnumerable<GridItem> Walk(GridItem item)
        {
            yield return item;
            foreach (GridItem child in item.GridItems)
                foreach (var descendant in Walk(child)) yield return descendant;
        }
        return Walk(root);
    }

    [Theory]
    [InlineData("gani")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void KeyboardSwitchesIndependentNativeDocumentHistories(string format) => RunSta(root =>
    {
        using var studio = Host(root);
        var first = studio.NewDocument(format);
        AuthoringModel(first.Document.Editor).RenameDefinition("first edit");
        var second = studio.NewDocument(format);
        AuthoringModel(second.Document.Editor).RenameDefinition("second edit");
        void FocusNavigation(Control editor)
        {
            Application.DoEvents();
            var target = Descendants(editor).First(c => c.Visible && c.CanFocus && c is TreeView or ListView or ListBox);
            target.Focus(); Assert.True(target.ContainsFocus);
        }
        FocusNavigation(second.Document.Editor);
        RouteKey(Keys.Control | Keys.Z);
        Assert.True(first.Document.History!.CanUndo); Assert.False(second.Document.History!.CanUndo);
        Assert.Equal("first edit", first.Document.SelectedMember);
        first.Activate(); FocusNavigation(first.Document.Editor);
        RouteKey(Keys.Control | Keys.Z);
        Assert.False(first.Document.History.CanUndo); Assert.True(second.Document.History.CanRedo);
        RouteKey(Keys.Control | Keys.Y); Assert.Equal("first edit", first.Document.SelectedMember);
        Assert.NotEqual("second edit", second.Document.SelectedMember);
    });
}
