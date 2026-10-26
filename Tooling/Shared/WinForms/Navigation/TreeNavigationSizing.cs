namespace Gondwana.Tooling.WinForms;

/// <summary>Owns a navigation tree's scaled presentation while retaining its nodes, selection, and original images.</summary>
public sealed class TreeNavigationSizing : IDisposable
{
    private readonly TreeView _tree;
    private readonly Func<int, ImageList>? _createImages;
    private readonly NavigationIconSize? _defaultSize;
    private readonly ImageList? _originalImages;
    private readonly Font _originalFont;
    private ImageList? _images;
    private Font? _font;
    private int _dpi;
    private bool _disposed;
    /// <summary>Gets the applied logical size, or null while the original control defaults are in use.</summary>
    public NavigationIconSize? Size { get; private set; }

    internal TreeNavigationSizing(TreeView tree, Func<int, ImageList>? createImages, NavigationIconSize? defaultSize)
    {
        _tree = tree;
        _originalImages = tree.ImageList;
        _originalFont = tree.Font;
        _createImages = createImages ?? (_originalImages is null ? null : pixels => NavigationSizing.CopyImages(_originalImages, pixels));
        _defaultSize = defaultSize;
        NavigationSizing.PreferenceChanged += ReadPreference;
        tree.DpiChangedAfterParent += DpiChanged;
        tree.HandleCreated += DpiChanged;
        tree.Disposed += TreeDisposed;
        ReadPreference();
    }

    private void ReadPreference()
    {
        if (_disposed) return;
        if (_tree.InvokeRequired) { _tree.BeginInvoke((Action)ReadPreference); return; }
        if ((new NavigationPreferenceStore().Read() ?? _defaultSize) is { } size) Apply(size);
    }

    private void DpiChanged(object? sender, EventArgs e)
    {
        if (Size is { } size) Apply(size);
    }

    /// <summary>Applies a logical size without changing the persisted preference or rebuilding nodes.</summary>
    public void Apply(NavigationIconSize size) => Apply(size, false);

    /// <summary>Recreates scaled images after the original thumbnail collection changes.</summary>
    public void RefreshImages()
    {
        if (Size is { } size && _createImages is not null) Apply(size, true);
    }

    private void Apply(NavigationIconSize size, bool refresh)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int dpi = _tree.DeviceDpi;
        int pixels = NavigationSizing.IconPixels(size, dpi);
        if (!refresh && _font is not null && Size == size && _dpi == dpi) return;
        var images = _createImages?.Invoke(pixels);
        Font font;
        try { font = new Font("Segoe UI", NavigationSizing.FontPoints(size), FontStyle.Regular, GraphicsUnit.Point); }
        catch { images?.Dispose(); throw; }
        var previousImages = _images;
        var previousFont = _font;
        var top = _tree.TopNode;
        _tree.BeginUpdate();
        try
        {
            _images = images;
            _font = font;
            Size = size;
            _dpi = dpi;
            _tree.ImageList = images;
            _tree.Font = font;
            _tree.ItemHeight = Math.Max(pixels, (int)Math.Ceiling(font.SizeInPoints * dpi / 72d))
                + Math.Max(1, (int)Math.Round(2 * dpi / 96d));
            // Never assign SelectedNode: selection handlers may open documents or mark an editor dirty.
            if (top is not null && _tree.TopNode != top) _tree.TopNode = top;
        }
        finally
        {
            _tree.EndUpdate();
            previousImages?.Dispose();
            previousFont?.Dispose();
        }
    }

    private void TreeDisposed(object? sender, EventArgs e) => Dispose();

    /// <summary>Unsubscribes notifications and releases only images and fonts owned by this binding.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NavigationSizing.PreferenceChanged -= ReadPreference;
        _tree.DpiChangedAfterParent -= DpiChanged;
        _tree.HandleCreated -= DpiChanged;
        _tree.Disposed -= TreeDisposed;
        if (!_tree.IsDisposed)
        {
            _tree.ImageList = _originalImages;
            _tree.Font = _originalFont;
        }
        _images?.Dispose();
        _font?.Dispose();
    }
}
