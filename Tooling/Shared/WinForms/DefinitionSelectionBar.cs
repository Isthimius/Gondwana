namespace Gondwana.Tooling.WinForms;

/// <summary>A compact shared selector for logical definitions inside one physical editor document.</summary>
/// <typeparam name="T">The definition model.</typeparam>
internal sealed class DefinitionSelectionBar<T> : ToolStrip where T : class
{
    private readonly ToolStripComboBox _members = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly ToolStripTextBox _identity = new() { Width = 180 };
    private readonly Func<IReadOnlyList<T>> _definitions;
    private readonly Func<T> _selected;
    private readonly Func<T, string> _name;
    private readonly Action<T> _select;
    private readonly Action _refresh;
    private bool _binding;

    /// <summary>Creates the shared member selector and edit commands.</summary>
    /// <param name="definitions">All document members.</param>
    /// <param name="selected">The current member.</param>
    /// <param name="name">The identity accessor.</param>
    /// <param name="select">The selection operation.</param>
    /// <param name="add">The add operation.</param>
    /// <param name="duplicate">The duplicate operation.</param>
    /// <param name="remove">The remove operation.</param>
    /// <param name="rename">The validated rename operation.</param>
    /// <param name="refresh">Refreshes the editor after a member operation.</param>
    /// <param name="extension">The accepted native definition extension.</param>
    /// <param name="import">Imports a file using the shared member picker.</param>
    internal DefinitionSelectionBar(Func<IReadOnlyList<T>> definitions, Func<T> selected, Func<T, string> name,
        Action<T> select, Action add, Action duplicate, Action remove, Action<string> rename, Action refresh, string extension,
        Func<string, Func<IReadOnlyList<T>, IReadOnlyList<int>?>, int> import)
    {
        _definitions = definitions;
        _selected = selected;
        _name = name;
        _select = select;
        _refresh = refresh;
        Dock = DockStyle.Top;
        GripStyle = ToolStripGripStyle.Hidden;
        Items.Add(new ToolStripLabel("Definition"));
        Items.Add(_members);
        Items.Add("Add definition", null, (_, _) => Execute(add));
        Items.Add("Import definition…", null, (_, _) =>
        {
            try
            {
                using var file = new OpenFileDialog { Filter = $"{extension.ToUpperInvariant()} definitions|*.{extension}", CheckFileExists = true, Multiselect = false };
                if (file.ShowDialog(FindForm()) != DialogResult.OK) return;
                if (import(file.FileName, ChooseMembers) == 0) return;
                _refresh();
                RefreshMembers(this, EventArgs.Empty);
            }
            catch (Exception ex) { MessageBox.Show(FindForm(), ex.Message, "Import definition", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        });
        Items.Add("Duplicate definition", null, (_, _) => Execute(duplicate));
        Items.Add("Remove definition", null, (_, _) => Execute(remove));
        Items.Add(new ToolStripSeparator());
        Items.Add(_identity);
        Items.Add("Rename", null, (_, _) => Execute(() => rename(_identity.Text)));
        _members.SelectedIndexChanged += (_, _) =>
        {
            if (!_binding && _members.SelectedIndex >= 0)
                Execute(() => _select(_definitions()[_members.SelectedIndex]));
        };
        RefreshMembers(this, EventArgs.Empty);
    }

    /// <summary>Refreshes displayed identities without changing selection or document dirty state.</summary>
    /// <param name="sender">The change source.</param>
    /// <param name="e">The change event.</param>
    internal void RefreshMembers(object? sender, EventArgs e)
    {
        _binding = true;
        try
        {
            _members.Items.Clear();
            foreach (var member in _definitions())
            {
                int index = _members.Items.Add(_name(member));
                if (ReferenceEquals(member, _selected())) _members.SelectedIndex = index;
            }
            _identity.Text = _name(_selected());
        }
        finally { _binding = false; }
    }

    private IReadOnlyList<int>? ChooseMembers(IReadOnlyList<T> definitions)
    {
        if (definitions.Count <= 1) return Enumerable.Range(0, definitions.Count).ToArray();
        using var dialog = new Form
        {
            Text = "Import definitions", StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(460, 340), MinimumSize = new Size(360, 240),
            MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false
        };
        var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        foreach (var member in definitions) list.Items.Add(_name(member), true);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.LeftToRight };
        var all = new Button { Text = "Select all", AutoSize = true };
        var none = new Button { Text = "Select none", AutoSize = true };
        var accept = new Button { Text = "Import", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        all.Click += (_, _) => { for (int i = 0; i < list.Items.Count; i++) list.SetItemChecked(i, true); };
        none.Click += (_, _) => { for (int i = 0; i < list.Items.Count; i++) list.SetItemChecked(i, false); };
        buttons.Controls.AddRange([all, none, accept, cancel]);
        dialog.Controls.Add(list);
        dialog.Controls.Add(buttons);
        dialog.AcceptButton = accept;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog(FindForm()) == DialogResult.OK ? list.CheckedIndices.Cast<int>().ToArray() : null;
    }

    private void Execute(Action action)
    {
        try { action(); _refresh(); RefreshMembers(this, EventArgs.Empty); }
        catch (Exception ex) { MessageBox.Show(FindForm(), ex.Message, "Definition", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
