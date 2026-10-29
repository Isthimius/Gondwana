using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.WinForms;

/// <summary>Shared host routing; history remains in the active document and text input retains local undo.</summary>
internal sealed class UndoRedoMenu : IDisposable
{
    private readonly Func<EditHistory?> _history;
    private readonly Func<bool> _commit;
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
        bool redo = keys == (Keys.Control | Keys.Y) || keys == (Keys.Control | Keys.Shift | Keys.Z);
        if (!redo && keys != (Keys.Control | Keys.Z)) return false;
        var focus = owner.FindForm() as Control ?? owner;
        while (focus.Controls.Cast<Control>().FirstOrDefault(c => c.ContainsFocus) is { } child) focus = child;
        // Includes the PropertyGrid's in-place TextBox; let WinForms commit/cancel it normally.
        if (focus is TextBoxBase { ReadOnly: false } or ComboBox or UpDownBase) return false;
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
