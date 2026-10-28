using System.Drawing.Drawing2D;

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
    private const int IconSize = 32;
    private const float IconScale = IconSize / 48f;
    private readonly List<Bitmap> _icons = [];

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
        ImageScalingSize = new Size(IconSize, IconSize);
        // Apply the shared appearance before enabling native ToolStrip hover tooltips.
        // Otherwise it substitutes mouse-move tooltips for the whole strip.
        ShowItemToolTips = false;
        Disposed += (_, _) =>
        {
            foreach (var icon in _icons) icon.Dispose();
            _icons.Clear();
        };
        Items.Add(new ToolStripLabel("Definition"));
        Items.Add(_members);
        AddIconButton("Add definition", "AddDefinition", DefinitionCommand.Add, () => Execute(add));
        AddIconButton("Import definition…", "ImportDefinition", DefinitionCommand.Import, () =>
        {
            try
            {
                using var file = new OpenFileDialog { Filter = $"{extension.ToUpperInvariant()} definitions|*.{extension}", CheckFileExists = true, Multiselect = false };
                if (file.ShowDialog(FindForm()) != DialogResult.OK) return;
                if (import(file.FileName, ChooseMembers) == 0) return;
                _refresh();
                RefreshMembers(this, EventArgs.Empty);
            }
            catch (Exception ex) { ThemedMessageBox.Show(FindForm(), ex.Message, "Import definition", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        });
        AddIconButton("Duplicate definition", "DuplicateDefinition", DefinitionCommand.Duplicate, () => Execute(duplicate));
        AddIconButton("Remove definition", "RemoveDefinition", DefinitionCommand.Remove, () => Execute(remove));
        Items.Add(new ToolStripSeparator());
        Items.Add(_identity);
        AddIconButton("Rename", "RenameDefinition", DefinitionCommand.Rename, () => Execute(() => rename(_identity.Text)));
        _members.SelectedIndexChanged += (_, _) =>
        {
            if (!_binding && _members.SelectedIndex >= 0)
                Execute(() => _select(_definitions()[_members.SelectedIndex]));
        };
        RefreshMembers(this, EventArgs.Empty);
        ToolingAppearance.Apply(this);
        ShowItemToolTips = true;
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


    private enum DefinitionCommand { Add, Import, Duplicate, Remove, Rename }

    private void AddIconButton(string label, string name, DefinitionCommand command, Action action)
    {
        var button = new ToolStripButton
        {
            Name = name,
            Text = label,
            AutoSize = false,
            Size = new Size(IconSize, IconSize),
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = DrawIcon(command),
            ToolTipText = label,
            AutoToolTip = false,
            AccessibleName = label
        };
        button.Click += (_, _) => action();
        Items.Add(button);
    }

    private Bitmap DrawIcon(DefinitionCommand command)
    {
        var image = new Bitmap(IconSize, IconSize);
        using (var graphics = Graphics.FromImage(image))
        using (var pen = new Pen(ToolingAppearance.Palette.Foreground, 3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        using (var accent = new Pen(ToolingAppearance.Palette.Accent, 3.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.ScaleTransform(IconScale, IconScale);
            switch (command)
            {
                case DefinitionCommand.Add:
                    graphics.DrawLines(pen, new PointF[] { new(10, 7), new(27, 7), new(36, 16), new(36, 27) });
                    graphics.DrawLines(pen, new PointF[] { new(10, 7), new(10, 41), new(25, 41) });
                    graphics.DrawLines(pen, new PointF[] { new(27, 7), new(27, 16), new(36, 16) });
                    graphics.DrawLine(accent, 34, 30, 34, 44);
                    graphics.DrawLine(accent, 27, 37, 41, 37);
                    break;
                case DefinitionCommand.Import:
                    graphics.DrawLines(pen, new PointF[] { new(9, 31), new(9, 41), new(39, 41), new(39, 31) });
                    graphics.DrawLine(accent, 24, 6, 24, 31);
                    graphics.DrawLines(accent, new PointF[] { new(14, 22), new(24, 32), new(34, 22) });
                    break;
                case DefinitionCommand.Duplicate:
                    graphics.DrawRectangle(pen, 8, 7, 25, 29);
                    graphics.DrawRectangle(pen, 16, 15, 25, 29);
                    break;
                case DefinitionCommand.Remove:
                    graphics.DrawLine(pen, 10, 13, 38, 13);
                    graphics.DrawLine(pen, 18, 8, 30, 8);
                    graphics.DrawLines(pen, new PointF[] { new(13, 18), new(16, 41), new(32, 41), new(35, 18) });
                    graphics.DrawLine(pen, 21, 23, 21, 35);
                    graphics.DrawLine(pen, 27, 23, 27, 35);
                    break;
                case DefinitionCommand.Rename:
                    graphics.DrawLines(pen, new PointF[] { new(9, 8), new(30, 8), new(37, 15), new(37, 23) });
                    graphics.DrawLines(pen, new PointF[] { new(9, 8), new(9, 41), new(19, 41) });
                    graphics.DrawLines(pen, new PointF[] { new(30, 8), new(30, 15), new(37, 15) });
                    graphics.DrawLines(accent, new PointF[] { new(22, 35), new(36, 21), new(42, 27), new(28, 41), new(22, 41), new(22, 35) });
                    graphics.DrawLine(accent, 33, 24, 39, 30);
                    break;
            }
        }

        _icons.Add(image);
        return image;
    }

    private IReadOnlyList<int>? ChooseMembers(IReadOnlyList<T> definitions)
    {
        if (definitions.Count <= 1) return Enumerable.Range(0, definitions.Count).ToArray();
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
        Gondwana.Tooling.WinForms.Theming.ToolingAppearance.Apply(dialog);
        return dialog.ShowDialog(FindForm()) == DialogResult.OK ? list.CheckedIndices.Cast<int>().ToArray() : null;
    }

    private void Execute(Action action)
    {
        try { action(); _refresh(); RefreshMembers(this, EventArgs.Empty); }
        catch (Exception ex) { ThemedMessageBox.Show(FindForm(), ex.Message, "Definition", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
