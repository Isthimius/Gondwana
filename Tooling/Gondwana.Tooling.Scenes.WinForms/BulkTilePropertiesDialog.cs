using System.ComponentModel;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Scenes.WinForms;

/// <summary>Detached values; no document edits occur until the user confirms checked properties.</summary>
internal sealed class BulkTilePropertiesDialog : Form
{
    internal static readonly PropertyDescriptor[] EditableProperties = TypeDescriptor.GetProperties(typeof(TilePropertyAdapter))
        .Cast<PropertyDescriptor>().Where(p => !p.IsReadOnly && p.Name is not "Id" and not "Nickname").ToArray();
    private readonly CheckedListBox _apply = new() { Dock = DockStyle.Left, Width = 220, CheckOnClick = true };
    private readonly PropertyGrid _grid = new() { Dock = DockStyle.Fill, ToolbarVisible = false };
    private readonly Values _values;
    internal IReadOnlyDictionary<string, object?> Changes => _apply.CheckedItems.Cast<string>()
        .ToDictionary(name => name, name => _values.Edits[name]);

    internal BulkTilePropertiesDialog(IReadOnlyList<SceneLayerTileDefinition> tiles)
    {
        Text = $"Edit selected properties — {tiles.Count} authored tiles";
        Size = new(760, 580); StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        _values = new Values(tiles);
        _grid.SelectedObject = _values;
        foreach (var property in EditableProperties) _apply.Items.Add(property.Name);
        var instruction = new Label
        {
            Dock = DockStyle.Top,
            Height = 52,
            Text = "Check the properties to apply and enter their values. Blank mixed values must be set explicitly.\nEmpty cells are skipped; unchecked properties and tile identities remain unchanged."
        };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var ok = new Button { Text = "Apply" };
        ok.Click += (_, _) =>
        {
            _grid.Focus();
            foreach (string name in _apply.CheckedItems)
            {
                if (_values.Edits.ContainsKey(name)) continue;
                if (_values.Mixed.Contains(name))
                {
                    MessageBox.Show(this, $"Enter a value for {name}, or uncheck it.", "Mixed property");
                    return;
                }
                _values.Edits[name] = _values.Initial[name];
            }
            DialogResult = DialogResult.OK;
        };
        buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
        Controls.Add(_grid); Controls.Add(_apply); Controls.Add(instruction); Controls.Add(buttons);
        CancelButton = cancel; AcceptButton = ok;
        ToolingAppearance.Apply(this);
    }

    internal sealed class Values : CustomTypeDescriptor
    {
        internal readonly Dictionary<string, object?> Initial = [];
        internal readonly Dictionary<string, object?> Edits = [];
        internal readonly HashSet<string> Mixed = [];
        internal Values(IReadOnlyList<SceneLayerTileDefinition> tiles)
        {
            foreach (var property in EditableProperties)
            {
                var model = typeof(SceneLayerTileDefinition).GetProperty(property.Name)!;
                var value = model.GetValue(tiles[0]);
                Initial[property.Name] = value;
                if (tiles.Skip(1).Any(t => !Equals(value, model.GetValue(t)))) Mixed.Add(property.Name);
            }
        }
        public override PropertyDescriptorCollection GetProperties() => GetProperties(null);
        public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes) =>
            new(EditableProperties.Select(p => new ValueProperty(p, this)).ToArray());
        public override object GetPropertyOwner(PropertyDescriptor? pd) => this;
    }

    private sealed class ValueProperty(PropertyDescriptor source, Values values) : PropertyDescriptor(source)
    {
        public override Type ComponentType => typeof(Values);
        public override Type PropertyType => source.PropertyType;
        public override bool IsReadOnly => false;
        public override string DisplayName => source.DisplayName + (values.Mixed.Contains(Name) && !values.Edits.ContainsKey(Name) ? " (mixed)" : "");
        public override object? GetValue(object? component) => values.Edits.TryGetValue(Name, out var edit) ? edit :
            values.Mixed.Contains(Name) ? null : values.Initial[Name];
        public override void SetValue(object? component, object? value) { values.Edits[Name] = value; OnValueChanged(component, EventArgs.Empty); }
        public override bool CanResetValue(object component) => false;
        public override void ResetValue(object component) { }
        public override bool ShouldSerializeValue(object component) => false;
    }
}
