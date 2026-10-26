using System.Runtime.CompilerServices;
using Gondwana.Tooling.WinForms.Theming;

namespace Gondwana.Tooling.WinForms;

/// <summary>Explicit opt-in navigation sizing; never walks editor controls or changes theme infrastructure.</summary>
public static class NavigationSizing
{
    private static readonly ConditionalWeakTable<TreeView, TreeNavigationSizing> Trees = new();
    internal static event Action? PreferenceChanged;

    /// <summary>Registers a tree for immediate size changes and DPI updates, releasing resources when it is disposed.</summary>
    /// <param name="tree">Navigation tree to resize.</param>
    /// <param name="createImages">Optional factory for a new owned image list at the supplied device-pixel size.</param>
    /// <param name="defaultSize">Optional fallback; null leaves existing defaults untouched until a size is saved.</param>
    /// <returns>The tree's presentation binding.</returns>
    public static TreeNavigationSizing Attach(TreeView tree, Func<int, ImageList>? createImages = null,
        NavigationIconSize? defaultSize = null) =>
        Trees.GetValue(tree, key => new TreeNavigationSizing(key, createImages, defaultSize));

    /// <summary>Refreshes scaled copies after a tree's original thumbnail list changes, without rebuilding its nodes.</summary>
    public static void RefreshImages(TreeView tree)
    {
        if (Trees.TryGetValue(tree, out var binding)) binding.RefreshImages();
    }

    /// <summary>Copies image keys and indices into an independent list; the caller retains ownership of the source.</summary>
    public static ImageList CopyImages(ImageList source, int pixels)
    {
        var result = new ImageList { ImageSize = new Size(pixels, pixels), ColorDepth = ColorDepth.Depth32Bit };
        try
        {
            for (int index = 0; index < source.Images.Count; index++)
            {
                using var image = source.Images[index]; // ImageList returns a new bitmap; the source list remains intact.
                result.Images.Add(source.Images.Keys[index] ?? string.Empty, image);
                _ = result.Handle; // Materialize native copies before releasing the source image.
            }
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    /// <summary>Creates the shared radio-style Icon Size menu and synchronizes saved choices when the host is activated.</summary>
    /// <param name="owner">Shell owning the menu.</param>
    /// <param name="store">Optional isolated store for tests or embedding hosts.</param>
    /// <param name="notify">Reports failed saves; the current presentation is retained.</param>
    public static ToolStripMenuItem CreateMenu(Form owner, NavigationPreferenceStore? store = null,
        Action<string>? notify = null)
    {
        store ??= new NavigationPreferenceStore();
        notify ??= message => ThemedMessageBox.Show(owner, message, "Icon Size", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        var menu = new ToolStripMenuItem("Icon Size");
        void RefreshChecks()
        {
            var size = store.Read() ?? NavigationIconSize.Small;
            foreach (ToolStripMenuItem item in menu.DropDownItems) item.Checked = Equals(item.Tag, size);
        }
        void RefreshPreference(object? sender, EventArgs e)
        {
            PreferenceChanged?.Invoke();
            RefreshChecks();
        }
        foreach (var size in Enum.GetValues<NavigationIconSize>())
        {
            var item = new ToolStripMenuItem(size.ToString()) { Tag = size };
            item.Click += (_, _) =>
            {
                if (!store.Write(size)) { notify("The icon size preference could not be saved. Check your settings folder permissions."); return; }
                PreferenceChanged?.Invoke();
                RefreshChecks();
            };
            menu.DropDownItems.Add(item);
        }
        owner.Activated += RefreshPreference;
        menu.DropDownOpening += RefreshPreference;
        menu.Disposed += (_, _) => owner.Activated -= RefreshPreference;
        RefreshChecks();
        return menu;
    }

    /// <summary>Converts logical icon pixels to device pixels, respecting ImageList's 256-pixel limit.</summary>
    public static int IconPixels(NavigationIconSize size, int dpi)
    {
        if (!Enum.IsDefined(size)) throw new ArgumentOutOfRangeException(nameof(size));
        if (dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi));
        return Math.Clamp((int)Math.Round((int)size * dpi / 96d), 1, 256);
    }

    /// <summary>Gets the font size in points; points already scale with DPI and must not be scaled twice.</summary>
    public static float FontPoints(NavigationIconSize size) => size switch
    {
        NavigationIconSize.Small => 9f,
        NavigationIconSize.Medium => 11f,
        NavigationIconSize.Large => 13f,
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };
}
