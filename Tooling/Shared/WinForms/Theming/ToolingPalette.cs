namespace Gondwana.Tooling.WinForms.Theming;

public enum ToolingTheme { Dark, DarkBlue, Light }

/// <summary>Semantic UI colors. Asset pixels, preview backgrounds and overlay colors are separate.</summary>
public sealed record ToolingPalette(Color Background, Color Surface, Color Foreground,
    Color MutedForeground, Color Selection, Color SelectionForeground, Color Accent,
    Color Hover, Color Border, Color DisabledForeground)
{
    public static string Name(ToolingTheme theme) => theme == ToolingTheme.DarkBlue ? "Dark Blue" : theme.ToString();

    public static ToolingPalette For(ToolingTheme theme) => theme switch
    {
        ToolingTheme.DarkBlue => new(Color.FromArgb(25, 31, 42), Color.FromArgb(34, 43, 58),
            Color.FromArgb(225, 230, 238), Color.FromArgb(172, 184, 201), Color.FromArgb(44, 87, 134),
            Color.White, Color.FromArgb(65, 120, 180), Color.FromArgb(43, 58, 79),
            Color.FromArgb(73, 88, 108), Color.FromArgb(150, 163, 182)),
        ToolingTheme.Light => new(Color.FromArgb(250, 250, 250), Color.FromArgb(239, 241, 244),
            Color.FromArgb(31, 35, 41), Color.FromArgb(83, 91, 104), Color.FromArgb(30, 94, 160),
            Color.White, Color.FromArgb(30, 94, 160), Color.FromArgb(219, 231, 245),
            Color.FromArgb(167, 174, 185), Color.FromArgb(105, 112, 123)),
        _ => new(Color.FromArgb(30, 30, 30), Color.FromArgb(45, 45, 48), Color.Gainsboro,
            Color.Silver, Color.FromArgb(62, 62, 66), Color.White, Color.FromArgb(0, 122, 204),
            Color.FromArgb(62, 62, 66), Color.DimGray, Color.Silver)
    };
}
