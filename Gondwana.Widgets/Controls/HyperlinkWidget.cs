using System.Drawing;
using SkiaSharp;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Represents an interactive text link that opens an external URI.
/// </summary>
public sealed class HyperlinkWidget : WidgetBase
{
    private readonly IExternalUriLauncher _uriLauncher;
    private readonly SKColor _normalColor = new(80, 150, 255);
    private readonly SKColor _hoverColor = new(125, 185, 255);

    #region events

    /// <summary>
    /// Occurs after the link has been opened successfully.
    /// </summary>
    public event Action<Uri>? LinkOpened;

    /// <summary>
    /// Occurs when an attempt to open the link fails.
    /// </summary>
    public event Action<Uri, Exception>? LinkOpenFailed;

    #endregion

    #region constructors

    /// <summary>
    /// Initializes a view-level hyperlink widget.
    /// </summary>
    /// <param name="uriLauncher">The service used to open external URIs.</param>
    /// <param name="renderSurfaceHost">The render surface host.</param>
    /// <param name="view">The view to associate the widget with.</param>
    /// <param name="bounds">The bounds of the widget.</param>
    /// <param name="text">The text to display for the link.</param>
    /// <param name="navigateUri">The URI to navigate to when the link is activated.</param>
    /// <param name="nickname">An optional nickname for the widget.</param>
    public HyperlinkWidget(IExternalUriLauncher uriLauncher,
                           RenderSurfaceHostBase renderSurfaceHost,
                           View view,
                           Rectangle bounds,
                           string text,
                           Uri navigateUri,
                           string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, bounds.Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(uriLauncher);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(navigateUri);

        _uriLauncher = uriLauncher;
        NavigateUri = navigateUri;

        Label = CreateLabel(renderSurfaceHost, view, bounds, text);

        Add(Label);
        CompleteInitialization();
    }

    /// <summary>
    /// Initializes a scene-layer hyperlink widget.
    /// </summary>
    /// <param name="uriLauncher">The service used to open external URIs.</param>
    /// <param name="renderSurfaceHost">The render surface host.</param>
    /// <param name="sceneLayer">The scene layer to associate the widget with.</param>
    /// <param name="bounds">The bounds of the widget.</param>
    /// <param name="text">The text to display for the link.</param>
    /// <param name="navigateUri">The URI to navigate to when the link is activated.</param>
    /// <param name="nickname">An optional nickname for the widget.</param>
    public HyperlinkWidget(IExternalUriLauncher uriLauncher,
                           RenderSurfaceHostBase renderSurfaceHost,
                           SceneLayer sceneLayer,
                           Rectangle bounds,
                           string text,
                           Uri navigateUri,
                           string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, bounds.Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(uriLauncher);
        ArgumentNullException.ThrowIfNull(sceneLayer);
        ArgumentNullException.ThrowIfNull(navigateUri);

        _uriLauncher = uriLauncher;
        NavigateUri = navigateUri;

        Label = CreateLabel(renderSurfaceHost, sceneLayer, bounds, text);

        Add(Label);
        CompleteInitialization();
    }

    #endregion constructors

    #region public properties

    /// <summary>
    /// Gets or sets the URI opened when the widget is activated.
    /// </summary>
    public Uri NavigateUri { get; set; }

    /// <summary>
    /// Gets the text block used to display the link.
    /// </summary>
    public TextBlock Label { get; }

    #endregion public properties
    
    #region public methods

    /// <summary>
    /// Opens the configured URI.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask OpenAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInputEnabled)
            return;

        Uri uri = NavigateUri;

        try
        {
            await _uriLauncher.OpenAsync(uri, cancellationToken);
            LinkOpened?.Invoke(uri);
        }
        catch (Exception ex)
        {
            LinkOpenFailed?.Invoke(uri, ex);
        }
    }

    #endregion public methods

    #region protected methods

    /// <inheritdoc/>
    protected override void OnPointerEnter(WidgetPointerEventArgs args)
    {
        base.OnPointerEnter(args);
        Label.SetColors(_hoverColor, SKColors.Transparent);
    }

    /// <inheritdoc/>
    protected override void OnPointerLeave(WidgetPointerEventArgs args)
    {
        base.OnPointerLeave(args);
        Label.SetColors(_normalColor, SKColors.Transparent);
    }

    /// <inheritdoc/>
    protected override void OnPointerClick(WidgetPointerEventArgs args)
    {
        base.OnPointerClick(args);

        if (!args.IsPrimaryButton)
            return;

        args.Handled = true;
        _ = OpenAsync();
    }

    /// <inheritdoc/>
    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);

        if (args.KeyAction != KeyAction.Pressed)
            return;

        // Standard Enter and Space virtual-key values.
        if (args.Key is not 13 and not 32)
            return;

        args.Handled = true;
        _ = OpenAsync();
    }

    #endregion protected methods

    #region private methods

    private void CompleteInitialization()
    {
        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;
    }

    private TextBlock CreateLabel(RenderSurfaceHostBase host,
                                  View view,
                                  Rectangle bounds,
                                  string text)
    {
        return new TextBlock(host, view, bounds)
            .SetText(text)
            .SetFont(SKTypeface.Default, 16f, minSize: 10f)
            .SetColors(_normalColor, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false);
    }

    private TextBlock CreateLabel(RenderSurfaceHostBase host,
                                  SceneLayer layer,
                                  Rectangle bounds,
                                  string text)
    {
        return new TextBlock(host, layer, view: null, worldBounds: bounds)
            .SetText(text)
            .SetFont(SKTypeface.Default, 16f, minSize: 10f)
            .SetColors(_normalColor, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false);
    }

    #endregion private methods
}