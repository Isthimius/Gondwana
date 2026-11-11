using DefinitionCommand = Gondwana.Tooling.WinForms.ToolbarGlyphs.DefinitionCommand;

namespace Gondwana.Tooling.WinForms;

/// <summary>A compact shared selector for logical definitions inside one physical editor document.</summary>
/// <typeparam name="T">The definition model.</typeparam>
internal sealed class DefinitionSelectionBar<T> : DpiToolStrip where T : class
{
    private readonly ToolStripComboBox _members = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly ToolStripTextBox _identity = new() { Width = 180 };
    private readonly Func<IReadOnlyList<T>> _definitions;
    private readonly Func<T> _selected;
    private readonly Func<T, string> _name;
    private readonly Action<T> _select;
    private readonly Action _refresh;
    private bool _binding;
    private bool _modalRename;

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
#pragma warning disable S107
    internal DefinitionSelectionBar(Func<IReadOnlyList<T>> definitions,
                                    Func<T> selected,
                                    Func<T, string> name,
                                    Action<T> select,
                                    Action add,
                                    Action duplicate,
                                    Action remove,
                                    Action<string> rename,
                                    Action refresh,
                                    string extension,
                                    Func<string, Func<IReadOnlyList<T>, IReadOnlyList<int>?>, int> import)
#pragma warning restore S107
        : base(32, 32)
    {
        _definitions = definitions;
        _selected = selected;
        _name = name;
        _select = select;
        _refresh = refresh;
        Dock = DockStyle.Top;
        GripStyle = ToolStripGripStyle.Hidden;

        // Apply the shared appearance before enabling native ToolStrip hover tooltips.
        // Otherwise it substitutes mouse-move tooltips for the whole strip.
        ShowItemToolTips = false;
        Items.Add(new ToolStripLabel("Definition"));
        Items.Add(_members);

        AddIconButton("Add definition", "AddDefinition", DefinitionCommand.Add, () => Execute(add));

        AddIconButton("Import definition…", "ImportDefinition", DefinitionCommand.Import, () =>
        {
            try
            {
                using var file = new OpenFileDialog
                {
                    Filter = $"{extension.ToUpperInvariant()} definitions|*.{extension}",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (file.ShowDialog(FindForm()) != DialogResult.OK)
                    return;

                if (import(file.FileName, ChooseMembers) == 0)
                    return;

                _refresh();
                RefreshMembers(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(FindForm(), ex.Message, "Import definition", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        });

        AddIconButton("Duplicate definition", "DuplicateDefinition", DefinitionCommand.Duplicate, () => Execute(duplicate));
        AddIconButton("Remove definition", "RemoveDefinition", DefinitionCommand.Remove, () => Execute(remove));

        Items.Add(new ToolStripSeparator());
        Items.Add(_identity);

        SetLogicalWidth(_members, 230);
        SetLogicalWidth(_identity, 180);

        AddIconButton("Rename", "RenameDefinition", DefinitionCommand.Rename, () =>
        {
            if (!_modalRename)
            {
                Execute(() => rename(_identity.Text));
                return;
            }

            if (DefinitionRenameDialog.Show(FindForm(), "Rename definition", _name(_selected()), rename))
            {
                _refresh();
                RefreshMembers(this, EventArgs.Empty);
            }
        });

        _members.SelectedIndexChanged += (_, _) =>
        {
            if (!_binding && _members.SelectedIndex >= 0)
                Execute(() => _select(_definitions()[_members.SelectedIndex]));
        };

        RefreshMembers(this, EventArgs.Empty);
        ToolingAppearance.Apply(this);
        ShowItemToolTips = true;
    }

    /// <summary>Places a modal rename button directly after the definition picker, without an inline name field.</summary>
    internal void UseModalRenameAfterSelector()
    {
        if (_modalRename)
            return;

        _modalRename = true;
        var renameButton = Items.Cast<ToolStripItem>().Single(item => item.Name == "RenameDefinition");
        Items.Remove(_identity);
        Items.Remove(renameButton);
        var trailingSeparator = Items.OfType<ToolStripSeparator>().LastOrDefault();
        if (trailingSeparator is not null)
            Items.Remove(trailingSeparator);

        Items.Insert(2, renameButton);
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

                if (ReferenceEquals(member, _selected()))
                    _members.SelectedIndex = index;
            }

            _identity.Text = _name(_selected());
        }
        finally
        {
            _binding = false;
        }
    }


    private void AddIconButton(string label, string name, DefinitionCommand command, Action action)
    {
        var button = new ToolStripButton
        {
            Name = name,
            Text = label,
            AutoSize = false,
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = label,
            AutoToolTip = false,
            AccessibleName = label
        };

        button.Click += (_, _) => action();

        Items.Add(button);
        SetGlyph(button, pixels => ToolbarGlyphs.Definition(command, pixels, ToolingAppearance.Palette));
    }

    private IReadOnlyList<int>? ChooseMembers(IReadOnlyList<T> definitions)
    {
        if (definitions.Count <= 1)
            return Enumerable.Range(0, definitions.Count).ToArray();

        using var dialog = new Form
        {
            Icon = Gondwana.Tooling.WinForms.ToolingBranding.CreateIcon(),
            Text = "Import definitions",
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(460, 340),
            MinimumSize = new Size(360, 240),
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false
        };

        var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };

        foreach (var member in definitions)
            list.Items.Add(_name(member), true);

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

        ToolingAppearance.Apply(dialog);

        return dialog.ShowDialog(FindForm()) == DialogResult.OK ? list.CheckedIndices.Cast<int>().ToArray() : null;
    }

    private void Execute(Action action)
    {
        try
        {
            action(); _refresh();
            RefreshMembers(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show(FindForm(), ex.Message, "Definition", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}


/// <summary>A compact, themed rename prompt that applies a validated edit only on confirmation.</summary>
internal static class DefinitionRenameDialog
{
    internal static bool Show(IWin32Window? owner, string title, string currentName, Action<string> rename)
    {
        using var dialog = new Form
        {
            Icon = ToolingBranding.CreateIcon(),
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(370, 112),
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false
        };

        var label = new Label { Text = "Name", AutoSize = true, Location = new Point(12, 12) };
        var nameBox = new TextBox
        {
            Name = "DefinitionRenameName",
            Text = currentName,
            Location = new Point(12, 33),
            Size = new Size(346, 25),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        var accept = new Button
        {
            Name = "ConfirmRename",
            Text = "Save",
            Location = new Point(202, 72),
            Size = new Size(75, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(283, 72),
            Size = new Size(75, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };

        accept.Click += (_, _) =>
        {
            string requested = nameBox.Text;
            if (requested == currentName)
            {
                dialog.DialogResult = DialogResult.Cancel;
                dialog.Close();
                return;
            }

            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(requested);
                rename(requested);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(dialog, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nameBox.Focus();
                nameBox.SelectAll();
            }
        };

        dialog.Controls.AddRange([label, nameBox, accept, cancel]);
        dialog.AcceptButton = accept;
        dialog.CancelButton = cancel;
        dialog.Shown += (_, _) => { nameBox.Focus(); nameBox.SelectAll(); };
        ToolingAppearance.Apply(dialog);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }
}
