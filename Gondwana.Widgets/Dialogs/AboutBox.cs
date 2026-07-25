using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Controls;
using SkiaSharp;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Widgets.Dialogs;

/// <summary>
/// Represents a conventional application-information dialog.
/// </summary>
public sealed class AboutBox : DialogBox
{
    private const int DefaultWidth = 520;
    private const int DefaultHeight = 330;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutBox"/> class.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns the dialog.</param>
    /// <param name="view">The view in which the dialog is displayed.</param>
    /// <param name="applicationName">The application name.</param>
    /// <param name="version">The application version.</param>
    /// <param name="description">Optional descriptive text.</param>
    /// <param name="copyright">Optional copyright text.</param>
    /// <param name="logo">An optional caller-owned image.</param>
    /// <param name="bounds">
    /// Optional dialog bounds. When omitted, the dialog is centered in the view.
    /// </param>
    /// <param name="nickname">An optional diagnostic nickname.</param>
    public AboutBox(
        RenderSurfaceHostBase renderSurfaceHost,
        View view,
        string applicationName,
        string version,
        string? description = null,
        string? copyright = null,
        SKImage? logo = null,
        Rectangle? bounds = null,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            view,
            ResolveBounds(view, bounds),
            $"About {applicationName}",
            showCloseButton: true,
            nickname: nickname ??
                      "__gondwana_about__")
    {
        ApplicationName = applicationName;
        Version = version;
        Description = description;
        Copyright = copyright;

        Rectangle resolvedBounds =
            ResolveBounds(view, bounds);

        int contentTop =
            resolvedBounds.Top + 52;

        int contentLeft =
            resolvedBounds.Left + 24;

        int contentRightPadding = 24;

        if (logo is not null)
        {
            Logo = new DirectImage(
                    logo,
                    renderSurfaceHost,
                    view,
                    new Rectangle(
                        contentLeft,
                        contentTop,
                        112,
                        112),
                    $"{Nickname}.logo")
                .SetScaleMode(
                    DirectImage.ScaleMode.Fit);

            Logo.ZOrder = 10_002;
            Add(Logo);

            contentLeft += 132;
        }

        HeaderText = new TextBlock(
                renderSurfaceHost,
                view,
                new Rectangle(
                    contentLeft,
                    contentTop,
                    resolvedBounds.Right -
                    contentRightPadding -
                    contentLeft,
                    44),
                $"{Nickname}.header")
            .SetText(applicationName)
            .SetFont(
                SKTypeface.Default,
                26f,
                minSize: 16f)
            .SetColors(
                SKColors.White,
                SKColors.Transparent)
            .SetAlignment(
                SKTextAlign.Left,
                TextBlock.VerticalAlign.Center)
            .EnableWrapping(false);

        HeaderText.ZOrder = 10_002;
        Add(HeaderText);

        VersionText = new TextBlock(
                renderSurfaceHost,
                view,
                new Rectangle(
                    contentLeft,
                    contentTop + 44,
                    resolvedBounds.Right -
                    contentRightPadding -
                    contentLeft,
                    30),
                $"{Nickname}.version")
            .SetText($"Version {version}")
            .SetFont(
                SKTypeface.Default,
                15f,
                minSize: 11f)
            .SetColors(
                new SKColor(
                    205,
                    205,
                    215),
                SKColors.Transparent)
            .SetAlignment(
                SKTextAlign.Left,
                TextBlock.VerticalAlign.Center)
            .EnableWrapping(false);

        VersionText.ZOrder = 10_002;
        Add(VersionText);

        string detailText =
            string.Join(
                Environment.NewLine + Environment.NewLine,
                new[]
                {
                    description,
                    copyright
                }
                .Where(static value =>
                    !string.IsNullOrWhiteSpace(value)));

        DetailsText = new TextBlock(
                renderSurfaceHost,
                view,
                new Rectangle(
                    resolvedBounds.Left + 24,
                    contentTop + 126,
                    resolvedBounds.Width - 48,
                    resolvedBounds.Height - 222),
                $"{Nickname}.details")
            .SetText(detailText)
            .SetFont(
                SKTypeface.Default,
                14f,
                minSize: 10f)
            .SetColors(
                new SKColor(
                    225,
                    225,
                    232),
                SKColors.Transparent)
            .SetAlignment(
                SKTextAlign.Left,
                TextBlock.VerticalAlign.Top)
            .EnableWrapping(true);

        DetailsText.ZOrder = 10_002;
        Add(DetailsText);

        Rectangle okBounds =
            new(
                resolvedBounds.Right - 116,
                resolvedBounds.Bottom - 54,
                92,
                34);

        OkButton = new ButtonWidget(
            renderSurfaceHost,
            view,
            okBounds,
            "OK",
            $"{Nickname}.ok");

        AddChild(
            OkButton,
            new Vector2(
                resolvedBounds.Width - 116,
                resolvedBounds.Height - 54));

        OkButton.SetButtonZOrder(10_003);
        OkButton.Clicked += OnOkClicked;
    }

    /// <summary>
    /// Gets the application name.
    /// </summary>
    public string ApplicationName { get; }

    /// <summary>
    /// Gets the version text.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the optional description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the optional copyright text.
    /// </summary>
    public string? Copyright { get; }

    /// <summary>
    /// Gets the optional logo drawing.
    /// </summary>
    public DirectImage? Logo { get; }

    /// <summary>
    /// Gets the application-name text block.
    /// </summary>
    public TextBlock HeaderText { get; }

    /// <summary>
    /// Gets the version text block.
    /// </summary>
    public TextBlock VersionText { get; }

    /// <summary>
    /// Gets the detail text block.
    /// </summary>
    public TextBlock DetailsText { get; }

    /// <summary>
    /// Gets the OK button.
    /// </summary>
    public ButtonWidget OkButton { get; }

    /// <inheritdoc/>
    protected override void OnAcceptRequested()
    {
        Close(DialogResult.OK);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        OkButton.Clicked -= OnOkClicked;

        base.Dispose();
    }

    private void OnOkClicked()
    {
        Close(DialogResult.OK);
    }

    private static Rectangle ResolveBounds(
        View view,
        Rectangle? bounds)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (bounds is not null)
            return bounds.Value;

        Rectangle viewport =
            view.Viewport.TargetRectPx;

        int availableWidth =
            Math.Max(
                1,
                viewport.Width - 40);

        int availableHeight =
            Math.Max(
                1,
                viewport.Height - 40);

        int width =
            Math.Min(
                DefaultWidth,
                availableWidth);

        int height =
            Math.Min(
                DefaultHeight,
                availableHeight);

        return new Rectangle(
            viewport.Left +
            (viewport.Width - width) / 2,
            viewport.Top +
            (viewport.Height - height) / 2,
            width,
            height);
    }
}
