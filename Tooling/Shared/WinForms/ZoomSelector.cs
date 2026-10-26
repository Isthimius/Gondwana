using System.Globalization;

namespace Gondwana.Tooling.WinForms;

/// <summary>
/// Synchronizes a read-only zoom selector with preview magnification, including
/// wheel-generated percentages outside the fixed choices.
/// </summary>
internal sealed class ZoomSelector
{
    private static readonly float[] Presets = [.25f, .5f, 1f, 2f, 4f];
    private readonly ComboBox _combo;
    private readonly Action<float?> _setZoom;
    private bool _updating;
    private string? _customLabel;
    private float _customZoom;

    /// <summary>Initializes preset and Fit choices on an existing combo box.</summary>
    /// <param name="combo">The read-only WinForms combo box.</param>
    /// <param name="setZoom">Receives a preset zoom or null when Fit is chosen.</param>
    internal ZoomSelector(ComboBox combo, Action<float?> setZoom)
    {
        _combo = combo;
        _setZoom = setZoom;
        _combo.Items.AddRange(["25%", "50%", "100%", "200%", "400%", "Fit"]);
        _combo.SelectedIndex = 2;
        _combo.SelectedIndexChanged += SelectionChanged;
    }

    /// <summary>Displays the actual percentage, or Fit while auto fitting is active.</summary>
    /// <param name="zoom">Current preview magnification.</param>
    /// <param name="fit">Whether Fit mode is active.</param>
    internal void Show(float zoom, bool fit)
    {
        _updating = true;
        try
        {
            if (_customLabel is not null)
            {
                _combo.Items.Remove(_customLabel);
                _customLabel = null;
            }

            if (fit)
            {
                _combo.SelectedIndex = 5;
                return;
            }

            int preset = Array.FindIndex(Presets, value => MathF.Abs(value - zoom) < .0001f);
            if (preset >= 0)
            {
                _combo.SelectedIndex = preset;
                return;
            }

            // A replaceable item allows arbitrary wheel zoom values in a DropDownList.
            _customZoom = zoom;
            _customLabel = (zoom * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%";
            _combo.Items.Add(_customLabel);
            _combo.SelectedIndex = _combo.Items.Count - 1;
        }
        finally
        {
            _updating = false;
        }
    }

    private void SelectionChanged(object? sender, EventArgs e)
    {
        if (_updating)
            return;

        int selected = _combo.SelectedIndex;
        if (selected == 5)
            _setZoom(null);
        else if (selected >= 0 && selected < Presets.Length)
            _setZoom(Presets[selected]);
        else if (selected == 6)
            _setZoom(_customZoom);
    }
}
