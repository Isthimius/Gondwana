using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.WinForms;

/// <summary>Shared host routing; history remains in the active document and text input retains local undo.</summary>
internal sealed class UndoRedoMenu : IDisposable
{
    // Register at the editor boundary: a child (notably PropertyGrid's internal
    // controls) may consume a command before it reaches a parent's ProcessCmdKey.
    internal static IDisposable Route(Control editor, Func<EditHistory?> history, Func<bool> commit) =>
        new ShortcutFilter(editor, history, commit);

    private sealed class ShortcutFilter : IMessageFilter, IDisposable
    {
        private readonly Control _editor;
        private readonly Func<EditHistory?> _history;
        private readonly Func<bool> _commit;

        internal ShortcutFilter(Control editor, Func<EditHistory?> history, Func<bool> commit)
        {
            _editor = editor; _history = history; _commit = commit;
            Application.AddMessageFilter(this);
        }

        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg != 0x100 || _editor.IsDisposed || !_editor.Enabled) return false;
            var target = Control.FromChildHandle(message.HWnd);
            if (target is null || (target != _editor && !_editor.Contains(target))) return false;
            return TryShortcut(_editor, (Keys)(int)message.WParam | Control.ModifierKeys, _history(), _commit);
        }

        public void Dispose() => Application.RemoveMessageFilter(this);
    }
    private readonly Func<EditHistory?> _history;
    private readonly Func<bool> _commit;
    // Accelerators are registered by Route, not by these display labels. Registering
    // unconditional menu shortcuts would also intercept native text-editing undo.
    private readonly ToolStripMenuItem _undo = new("&Undo") { ShortcutKeyDisplayString = "Ctrl+Z" };
    private readonly ToolStripMenuItem _redo = new("&Redo") { ShortcutKeyDisplayString = "Ctrl+Y" };
    private EditHistory? _observed;
    public ToolStripMenuItem Menu { get; } = new("&Edit");

    public UndoRedoMenu(Func<EditHistory?> history, Func<bool> commit)
    {
        _history = history; _commit = commit;
        Menu.DropDownItems.AddRange([_undo, _redo]);
        Menu.DropDownOpening += (_, _) => Refresh();
        _undo.Click += (_, _) => Perform(_history(), _commit, redo: false);
        _redo.Click += (_, _) => Perform(_history(), _commit, redo: true);
        Refresh();
    }

    public void Refresh()
    {
        var current = _history();
        if (!ReferenceEquals(current, _observed))
        {
            if (_observed is not null) _observed.Changed -= Changed;
            _observed = current;
            if (_observed is not null) _observed.Changed += Changed;
        }
        _undo.Enabled = current?.CanUndo == true;
        _redo.Enabled = current?.CanRedo == true;
        _undo.Text = "&Undo" + (current?.UndoDescription is { } undo ? " " + undo : "");
        _redo.Text = "&Redo" + (current?.RedoDescription is { } redo ? " " + redo : "");
    }

    private void Changed(object? sender, EventArgs e) => Refresh();

    internal static bool TryShortcut(Control owner, Keys keys, EditHistory? history, Func<bool> commit)
    {
        if (!owner.Enabled) return false;
        bool redo = keys == (Keys.Control | Keys.Y) || keys == (Keys.Control | Keys.Shift | Keys.Z);
        if (!redo && keys != (Keys.Control | Keys.Z)) return false;
        var focus = owner.FindForm() as Control ?? owner;
        while (focus.Controls.Cast<Control>().FirstOrDefault(c => c.ContainsFocus) is { } child) focus = child;
        // Drop-down-only selectors are navigation, not native text editors.
        // PropertyGrid keeps its TextBox focused even when no text is being edited.
        bool inPropertyGrid = false;
        for (var parent = focus.Parent; parent is not null; parent = parent.Parent)
            if (parent is PropertyGrid) { inPropertyGrid = true; break; }
        if (focus is TextBoxBase { ReadOnly: false } text && (!inPropertyGrid || text.Modified)) return false;
        if (focus is ComboBox { DropDownStyle: not ComboBoxStyle.DropDownList } or UpDownBase) return false;
        if (history is null) return false;
        Perform(history, commit, redo);
        if (!focus.IsDisposed && focus.CanFocus) focus.Focus();
        return true;
    }

    private static void Perform(EditHistory? history, Func<bool> commit, bool redo)
    {
        if (history is null || !(redo ? history.CanRedo : history.CanUndo) || !commit()) return;
        if (redo) history.Redo(); else history.Undo();
    }

    public void Dispose()
    {
        if (_observed is not null) _observed.Changed -= Changed;
        _observed = null;
        Menu.Dispose();
    }
}
