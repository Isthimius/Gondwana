using System.Drawing;
using System.Numerics;
using SkiaSharp;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Widgets.Controls;

namespace Gondwana.Widgets.Dialogs;

/// <summary>
/// Provides a draggable container with panel, title-bar, close, keyboard,
/// and result semantics.
/// </summary>
public abstract class DialogBox : DraggableContainerWidget
{
    protected const int DefaultTitleBarHeight = 36;
    protected const int DefaultCloseButtonSize = 28;

    protected readonly static Color DefaultPanelColor = Color.FromArgb(245, 36, 36, 44);
    protected readonly static Color DefaultPanelBorderColor = Color.FromArgb(255, 140, 140, 155);
    protected readonly static Color DefaultTitleBarColor = Color.FromArgb(255, 57, 57, 72);

    private bool _disposed;

    /// <summary>
    /// Occurs when the dialog closes.
    /// </summary>
    public event Action<DialogResult>? Closed;

    #region constructors

    /// <summary>
    /// Initializes a view-level dialog.
    /// </summary>
    protected DialogBox(RenderSurfaceHostBase renderSurfaceHost,
                        View view,
                        Rectangle bounds,
                        string title,
                        bool showCloseButton = true,
                        string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, bounds.Location, nickname)
    {
        ValidateBounds(bounds);

        DialogSize = bounds.Size;

        Panel = CreatePanel(renderSurfaceHost, view, bounds);
        TitleBar = CreateTitleBar(renderSurfaceHost, view, bounds);
        TitleText = CreateTitleText(renderSurfaceHost, view, bounds, title);

        Add(Panel);
        Add(TitleBar);
        Add(TitleText);

        if (showCloseButton)
        {
            CloseButton = CreateCloseButton(renderSurfaceHost, view, bounds);
            Add(CloseButton, GetCloseButtonOffset(bounds.Size));

            CloseButton.Clicked += OnCloseButtonClicked;
        }

        CompleteInitialization();
    }

    /// <summary>
    /// Initializes a scene-layer dialog.
    /// </summary>
    protected DialogBox(RenderSurfaceHostBase renderSurfaceHost,
                        SceneLayer sceneLayer,
                        Rectangle bounds,
                        string title,
                        bool showCloseButton = true,
                        string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, bounds.Location, nickname)
    {
        ValidateBounds(bounds);

        DialogSize = bounds.Size;

        Panel = CreatePanel(renderSurfaceHost, sceneLayer, bounds);
        TitleBar = CreateTitleBar(renderSurfaceHost, sceneLayer, bounds);
        TitleText = CreateTitleText(renderSurfaceHost, sceneLayer, bounds, title);

        Add(Panel);
        Add(TitleBar);
        Add(TitleText);

        if (showCloseButton)
        {
            CloseButton = CreateCloseButton(renderSurfaceHost, sceneLayer, bounds);
            Add(CloseButton, GetCloseButtonOffset(bounds.Size));

            CloseButton.Clicked += OnCloseButtonClicked;
        }

        CompleteInitialization();
    }

    #endregion constructors

    #region public properties

    /// <summary>
    /// Gets the dialog panel.
    /// </summary>
    public DirectRectangle Panel { get; }

    /// <summary>
    /// Gets the title-bar rectangle.
    /// </summary>
    public DirectRectangle TitleBar { get; }

    /// <summary>
    /// Gets the title text block.
    /// </summary>
    public TextBlock TitleText { get; }

    /// <summary>
    /// Gets the optional close button.
    /// </summary>
    public ButtonWidget? CloseButton { get; }

    /// <summary>
    /// Gets the dialog size.
    /// </summary>
    public Size DialogSize { get; }

    /// <summary>
    /// Gets whether the dialog has closed.
    /// </summary>
    public bool IsClosed { get; private set; }

    /// <summary>
    /// Gets the result selected when the dialog closed.
    /// </summary>
    public DialogResult Result { get; private set; }

    /// <summary>
    /// Gets or sets whether closing the dialog disposes it.
    /// </summary>
    public bool DisposeOnClose { get; set; } = true;

    /// <summary>
    /// Gets or sets the key value used to accept the dialog.
    /// </summary>
    /// <remarks>
    /// The default value is the conventional Enter virtual-key value, 13.
    /// </remarks>
    public int AcceptKey { get; set; } = 13;

    /// <summary>
    /// Gets or sets the key value used to cancel the dialog.
    /// </summary>
    /// <remarks>
    /// The default value is the conventional Escape virtual-key value, 27.
    /// </remarks>
    public int CancelKey { get; set; } = 27;

    #endregion public properties

    #region exposed methods and hooks

    /// <summary>
    /// Closes the dialog with the specified result.
    /// </summary>
    public void Close(DialogResult result = DialogResult.Close)
    {
        if (IsClosed)
            return;

        IsClosed = true;
        Result = result;

        Hide();

        OnClosed(result);
        Closed?.Invoke(result);

        if (DisposeOnClose)
            Dispose();
    }

    /// <summary>
    /// Called after the dialog closes.
    /// </summary>
    protected virtual void OnClosed(DialogResult result)
    {
    }

    /// <summary>
    /// Called when the accept key is pressed.
    /// </summary>
    protected virtual void OnAcceptRequested()
    {
        Close(DialogResult.OK);
    }

    /// <inheritdoc/>
    protected override bool CanStartDrag(WidgetPointerEventArgs args)
    {
        if (!base.CanStartDrag(args))
            return false;

        RectangleF titleBarBounds = TitleBar.GetDrawLocationScreen(args.View);

        return titleBarBounds.Contains(args.ScreenPositionPx.X, args.ScreenPositionPx.Y);
    }

    /// <inheritdoc/>
    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);

        if (args.Handled || args.KeyAction != KeyAction.Pressed)
            return;

        if (args.Key == CancelKey)
        {
            args.Handled = true;
            Close(DialogResult.Cancel);
            return;
        }

        if (args.Key == AcceptKey)
        {
            args.Handled = true;
            OnAcceptRequested();
        }
    }

    /// <inheritdoc/>
    protected override void OnCancelled()
    {
        base.OnCancelled();
        Close(DialogResult.Cancel);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (CloseButton is not null)
        {
            CloseButton.Clicked -= OnCloseButtonClicked;
        }

        base.Dispose();
    }

    #endregion exposed methods and hooks

    #region private methods

    private void CompleteInitialization()
    {
        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;
        IsPointerInputEnabled = true;
    }

    private void OnCloseButtonClicked()
    {
        Close(DialogResult.Cancel);
    }

    private static void ValidateBounds(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= DefaultTitleBarHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                bounds,
                "Dialog bounds must have positive width and enough height for the title bar.");
        }
    }

    private static Vector2 GetCloseButtonOffset(Size size)
    {
        return new Vector2(size.Width - DefaultCloseButtonSize - 4, 4);
    }

    private static DirectRectangle CreatePanel(RenderSurfaceHostBase host,
                                               View view,
                                               Rectangle bounds)
    {
        return ConfigurePanel(new DirectRectangle(DefaultPanelColor,
                              host,
                              view,
                              bounds));
    }

    private static DirectRectangle CreatePanel(RenderSurfaceHostBase host,
                                               SceneLayer layer,
                                               Rectangle bounds)
    {
        return ConfigurePanel(new DirectRectangle(DefaultPanelColor,
                              host,
                              layer,
                              bounds));
    }

    private static DirectRectangle ConfigurePanel(DirectRectangle panel)
    {
        panel.SetFilled(true)
             .SetBorderColor(DefaultPanelBorderColor)
             .SetStrokeWidth(2f)
             .SetCornerRadius(8f);

        panel.ZOrder = 10_000;

        return panel;
    }

    private static DirectRectangle CreateTitleBar(RenderSurfaceHostBase host,
                                                  View view,
                                                  Rectangle bounds)
    {
        return ConfigureTitleBar(new DirectRectangle(DefaultTitleBarColor,
                                                     host,
                                                     view,
                                                     GetTitleBarBounds(bounds)));
    }

    private static DirectRectangle CreateTitleBar(RenderSurfaceHostBase host,
                                                  SceneLayer layer,
                                                  Rectangle bounds)
    {
        return ConfigureTitleBar(new DirectRectangle(DefaultTitleBarColor,
                                                     host,
                                                     layer,
                                                     GetTitleBarBounds(bounds)));
    }

    private static DirectRectangle ConfigureTitleBar(DirectRectangle titleBar)
    {
        titleBar.SetFilled(true)
                .SetCornerRadius(8f);

        titleBar.ZOrder = 10_001;

        return titleBar;
    }

    private static TextBlock CreateTitleText(RenderSurfaceHostBase host,
                                             View view,
                                             Rectangle bounds,
                                             string title)
    {
        return ConfigureTitleText(new TextBlock(host, view, GetTitleTextBounds(bounds)), title);
    }

    private static TextBlock CreateTitleText(RenderSurfaceHostBase host,
                                             SceneLayer layer,
                                             Rectangle bounds,
                                             string title)
    {
        return ConfigureTitleText(new TextBlock(host, layer, view: null, worldBounds: GetTitleTextBounds(bounds)), title);
    }

    private static TextBlock ConfigureTitleText(TextBlock titleText, string title)
    {
        titleText.SetText(title)
                 .SetFont(SKTypeface.Default, 18f, minSize: 12f)
                 .SetColors(SKColors.White, SKColors.Transparent)
                 .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
                 .EnableWrapping(false);

        titleText.HorizontalPadding = 12f;
        titleText.ZOrder = 10_002;

        return titleText;
    }

    private static ButtonWidget CreateCloseButton(RenderSurfaceHostBase host,
                                                  View view,
                                                  Rectangle bounds)
    {
        return ConfigureCloseButton(new ButtonWidget(host, view, GetCloseButtonBounds(bounds), "×"));
    }

    private static ButtonWidget CreateCloseButton(RenderSurfaceHostBase host,
                                                  SceneLayer layer,
                                                  Rectangle bounds)
    {
        return ConfigureCloseButton(new ButtonWidget(host, layer, GetCloseButtonBounds(bounds), "×"));
    }

    private static ButtonWidget ConfigureCloseButton(ButtonWidget closeButton)
    {
        closeButton.SetBackgroundColors(Color.FromArgb(0, 0, 0, 0),
                                        Color.FromArgb(255, 110, 55, 62),
                                        Color.FromArgb(255, 145, 45, 55))
                   .SetTextColor(Color.White)
                   .SetButtonZOrder(10_003);

        return closeButton;
    }

    private static Rectangle GetTitleBarBounds(Rectangle bounds)
    {
        return new Rectangle(bounds.X, bounds.Y, bounds.Width, DefaultTitleBarHeight);
    }

    private static Rectangle GetTitleTextBounds(
        Rectangle bounds)
    {
        return new Rectangle(bounds.X, bounds.Y, bounds.Width - DefaultCloseButtonSize - 12, DefaultTitleBarHeight);
    }

    private static Rectangle GetCloseButtonBounds(
        Rectangle bounds)
    {
        return new Rectangle(bounds.Right - DefaultCloseButtonSize - 4, bounds.Y + 4, DefaultCloseButtonSize, DefaultCloseButtonSize);
    }

    #endregion private methods
}
