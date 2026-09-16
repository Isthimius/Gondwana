using System.Drawing;
using System.Globalization;
using System.Text.Json;

namespace Gondwana.Tooling.Tilesheets.Editing;

internal enum OverlayKind { Regions, SelectedRegion, Frames, SelectedFrame, Margin, Padding, Overhang, Collision }

/// <summary>Application preferences, deliberately separate from GTS document serialization.</summary>
internal sealed class OverlaySettings
{
    private static readonly Lazy<OverlaySettings> Shared = new(() => Load(Path.Combine(
        AppContext.BaseDirectory, "Gondwana.Tooling.Tilesheets.WinForms.settings.json")));
    public static OverlaySettings Default => Shared.Value;
    private readonly Dictionary<OverlayKind, Color> _colors = new()
    {
        [OverlayKind.Regions] = Color.SlateBlue,
        [OverlayKind.SelectedRegion] = Color.DeepSkyBlue,
        [OverlayKind.Frames] = Color.SeaGreen,
        [OverlayKind.SelectedFrame] = Color.White,
        [OverlayKind.Margin] = Color.Goldenrod,
        [OverlayKind.Padding] = Color.DimGray,
        [OverlayKind.Overhang] = Color.Orchid,
        [OverlayKind.Collision] = Color.Tomato
    };

    public string FilePath { get; }
    public string? Warning { get; private set; }
    public event EventHandler? Changed;
    private OverlaySettings(string path) => FilePath = Path.GetFullPath(path);
    public Color this[OverlayKind kind] => _colors[kind];

    public static OverlaySettings Load(string path)
    {
        var settings = new OverlaySettings(path);
        try
        {
            if (!File.Exists(settings.FilePath)) return settings;
            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(settings.FilePath));
            if (data?.OverlayColors is null) throw new JsonException("OverlayColors is missing.");
            foreach (var (key, value) in data.OverlayColors)
            {
                if (!Enum.TryParse<OverlayKind>(key, out var kind) || !Enum.IsDefined(kind)) continue;
                if (value is { Length: 7 } && value[0] == '#' &&
                    int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                    settings._colors[kind] = Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
                else
                    settings.Warning = "Some overlay colors in the settings file are invalid; defaults are being used for those colors.";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            settings.Warning = "Could not load overlay settings; using defaults. " + ex.Message;
        }
        return settings;
    }

    public void SetColor(OverlayKind kind, Color color)
    {
        // Persist first so failures cannot silently leave the UI claiming a saved preference.
        var colors = _colors.ToDictionary(pair => pair.Key.ToString(), pair => Hex(pair.Value));
        colors[kind.ToString()] = Hex(color);
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new SettingsData { OverlayColors = colors },
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        _colors[kind] = Color.FromArgb(color.R, color.G, color.B);
        Warning = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    private sealed class SettingsData
    {
        public Dictionary<string, string>? OverlayColors { get; set; }
    }
}
