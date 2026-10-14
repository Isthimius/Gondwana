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
    internal DefinitionSelectionBar(Func<IReadOnlyList<T>> definitions, Func<T> selected, Func<T, string> name,
        Action<T> select, Action add, Action duplicate, Action remove, Action<string> rename, Action refresh)
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

    private void Execute(Action action)
    {
        try { action(); _refresh(); RefreshMembers(this, EventArgs.Empty); }
        catch (Exception ex) { MessageBox.Show(FindForm(), ex.Message, "Definition", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
