using Gondwana.Tooling.Tilesheets.Editing;

namespace Gondwana.Tooling.Tilesheets.WinForms;

/// <summary>Independent visibility and color controls within the overlay dropdown.</summary>
internal sealed class OverlayLegendRow : UserControl
{
    private readonly OverlaySettings _settings;
    private readonly OverlayKind _kind;
    private readonly Panel _swatch;

    public OverlayLegendRow(OverlayKind kind, string label, OverlaySettings settings, Action<bool> visibilityChanged, Action chooseColor)
    {
        _kind = kind;
        _settings = settings;
        Size = new Size(450, 32);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        var toggle = new CheckBox { Text = label, Checked = true, Dock = DockStyle.Fill, AutoSize = false };
        toggle.CheckedChanged += (_, _) => visibilityChanged(toggle.Checked);
        _swatch = new Panel { Dock = DockStyle.Fill, Margin = new Padding(4, 7, 4, 7), BorderStyle = BorderStyle.FixedSingle };
        _swatch.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(_settings[_kind]);
            e.Graphics.FillRectangle(brush, _swatch.ClientRectangle);
        };
        var button = new Button { Text = "...", UseCompatibleTextRendering = true, Dock = DockStyle.Fill,
            Margin = new Padding(2), AccessibleName = $"Choose {label} color" };
        button.Click += (_, _) => chooseColor();
        layout.Controls.Add(toggle, 0, 0);
        layout.Controls.Add(_swatch, 1, 0);
        layout.Controls.Add(button, 2, 0);
        Controls.Add(layout);
        DarkTheme.Apply(this);
        _settings.Changed += SettingsChanged;
        SettingsChanged(this, EventArgs.Empty);
    }

    private void SettingsChanged(object? sender, EventArgs e) => _swatch.Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _settings.Changed -= SettingsChanged;
        base.Dispose(disposing);
    }
}
