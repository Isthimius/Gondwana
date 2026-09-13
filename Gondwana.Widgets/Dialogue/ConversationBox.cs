using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Widgets.Dialogue;

/// <summary>
/// Provides an NPC-style conversation panel with speaker, wrapped body text,
/// and pointer or keyboard advance semantics.
/// </summary>
public sealed class ConversationBox : WidgetBase
{
    private const int SpeakerHeight = 30;
    private const int ContinueIndicatorWidth = 28;
    private const int ContinueIndicatorHeight = 22;
    private const int ContentInset = 10;

    private string _speaker;
    private string _text;
    private bool _continueIndicatorVisible = true;

    /// <summary>
    /// Occurs when the player requests the next line or conversation step.
    /// </summary>
    public event Action? AdvanceRequested;

    /// <summary>
    /// Creates a view-level conversation box.
    /// </summary>
    public ConversationBox(RenderSurfaceHostBase renderSurfaceHost,
                           View view,
                           Rectangle bounds,
                           string speaker,
                           string text,
                           string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        _speaker = speaker ?? string.Empty;
        _text = text ?? string.Empty;

        Panel = CreatePanel(renderSurfaceHost, view, bounds);
        SpeakerText = CreateSpeakerText(renderSurfaceHost, view, GetSpeakerBounds(bounds), _speaker);
        BodyText = CreateBodyText(renderSurfaceHost, view, GetBodyBounds(bounds), _text);
        ContinueIndicator = CreateContinueIndicator(renderSurfaceHost, view, GetContinueBounds(bounds));

        Add(Panel);
        Add(SpeakerText);
        Add(BodyText);
        Add(ContinueIndicator);

        CompleteInitialization();
    }

    /// <summary>
    /// Creates a scene-layer conversation box.
    /// </summary>
    public ConversationBox(RenderSurfaceHostBase renderSurfaceHost,
                           SceneLayer sceneLayer,
                           Rectangle bounds,
                           string speaker,
                           string text,
                           string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);

        _speaker = speaker ?? string.Empty;
        _text = text ?? string.Empty;

        Panel = CreatePanel(renderSurfaceHost, sceneLayer, bounds);
        SpeakerText = CreateSpeakerText(renderSurfaceHost, sceneLayer, GetSpeakerBounds(bounds), _speaker);
        BodyText = CreateBodyText(renderSurfaceHost, sceneLayer, GetBodyBounds(bounds), _text);
        ContinueIndicator = CreateContinueIndicator(renderSurfaceHost, sceneLayer, GetContinueBounds(bounds));

        Add(Panel);
        Add(SpeakerText);
        Add(BodyText);
        Add(ContinueIndicator);

        CompleteInitialization();
    }

    /// <summary>
    /// Gets the panel background.
    /// </summary>
    public DirectRectangle Panel { get; }

    /// <summary>
    /// Gets the speaker-name text block.
    /// </summary>
    public TextBlock SpeakerText { get; }

    /// <summary>
    /// Gets the conversation body text block.
    /// </summary>
    public TextBlock BodyText { get; }

    /// <summary>
    /// Gets the small visual indicating that the conversation can advance.
    /// </summary>
    public TextBlock ContinueIndicator { get; }

    /// <summary>
    /// Gets the current speaker name.
    /// </summary>
    public string Speaker => _speaker;

    /// <summary>
    /// Gets the current conversation text.
    /// </summary>
    public string Text => _text;

    /// <summary>
    /// Gets the conversation bounds in its native coordinate space.
    /// </summary>
    public Rectangle Bounds => Mode == DirectDrawingMode.View
        ? Panel.ScreenBounds
        : Panel.WorldBounds;

    /// <summary>
    /// Gets or sets the primary key used to request the next conversation step.
    /// Defaults to Enter (13).
    /// </summary>
    public int PrimaryAdvanceKey { get; set; } = 13;

    /// <summary>
    /// Gets or sets the secondary key used to request the next conversation step.
    /// Defaults to Space (32).
    /// </summary>
    public int SecondaryAdvanceKey { get; set; } = 32;

    /// <summary>
    /// Changes the speaker and body text together.
    /// </summary>
    public ConversationBox SetConversation(string speaker, string text)
    {
        SetSpeaker(speaker);
        SetText(text);
        return this;
    }

    /// <summary>
    /// Changes the speaker name.
    /// </summary>
    public ConversationBox SetSpeaker(string speaker)
    {
        _speaker = speaker ?? string.Empty;
        SpeakerText.SetText(_speaker);
        return this;
    }

    /// <summary>
    /// Changes the conversation body text.
    /// </summary>
    public ConversationBox SetText(string text)
    {
        _text = text ?? string.Empty;
        BodyText.SetText(_text);
        return this;
    }

    /// <summary>
    /// Shows or hides the continue indicator.
    /// </summary>
    public ConversationBox ShowContinueIndicator(bool visible = true)
    {
        _continueIndicatorVisible = visible;
        ContinueIndicator.Visible = visible;
        return this;
    }

    /// <summary>
    /// Sets the speaker and body text colors.
    /// </summary>
    public ConversationBox SetTextColors(SKColor speakerColor, SKColor bodyColor)
    {
        SpeakerText.SetColors(speakerColor, SKColors.Transparent);
        BodyText.SetColors(bodyColor, SKColors.Transparent);
        return this;
    }

    /// <summary>
    /// Sets the panel fill and border colors.
    /// </summary>
    public ConversationBox SetPanelColors(Color fill, Color border)
    {
        Panel.SetColor(fill)
             .SetBorderColor(border);
        Panel.SetPosition(Panel.GetPosition());
        return this;
    }

    /// <summary>
    /// Sets the base Z-order used by the conversation visuals.
    /// </summary>
    public ConversationBox SetConversationZOrder(int zOrder)
    {
        Panel.ZOrder = zOrder;
        SpeakerText.ZOrder = zOrder + 1;
        BodyText.ZOrder = zOrder + 1;
        ContinueIndicator.ZOrder = zOrder + 2;
        return this;
    }

    /// <summary>
    /// Programmatically requests the next conversation step.
    /// </summary>
    public void Advance()
    {
        if (!IsInputEnabled)
            return;

        AdvanceRequested?.Invoke();
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();
        ContinueIndicator.Visible = _continueIndicatorVisible;
    }

    /// <inheritdoc/>
    protected override void OnPointerClick(WidgetPointerEventArgs args)
    {
        base.OnPointerClick(args);

        if (!args.IsPrimaryButton)
            return;

        args.Handled = true;
        Advance();
    }

    /// <inheritdoc/>
    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);

        if (args.KeyAction != KeyAction.Pressed ||
            args.Key != PrimaryAdvanceKey && args.Key != SecondaryAdvanceKey)
        {
            return;
        }

        args.Handled = true;
        Advance();
    }

    private void CompleteInitialization()
    {
        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;
        SetConversationZOrder(0);
    }

    private static DirectRectangle CreatePanel(RenderSurfaceHostBase host,
                                               View view,
                                               Rectangle bounds)
    {
        return ConfigurePanel(new DirectRectangle(Color.FromArgb(238, 24, 25, 32), host, view, bounds));
    }

    private static DirectRectangle CreatePanel(RenderSurfaceHostBase host,
                                               SceneLayer layer,
                                               Rectangle bounds)
    {
        return ConfigurePanel(new DirectRectangle(Color.FromArgb(238, 24, 25, 32), host, layer, bounds));
    }

    private static DirectRectangle ConfigurePanel(DirectRectangle panel)
    {
        return panel.SetFilled(true)
                    .SetBorderColor(Color.FromArgb(245, 205, 205, 215))
                    .SetStrokeWidth(2f)
                    .SetCornerRadius(7f);
    }

    private static TextBlock CreateSpeakerText(RenderSurfaceHostBase host,
                                               View view,
                                               Rectangle bounds,
                                               string speaker)
    {
        return ConfigureSpeakerText(new TextBlock(host, view, bounds), speaker);
    }

    private static TextBlock CreateSpeakerText(RenderSurfaceHostBase host,
                                               SceneLayer layer,
                                               Rectangle bounds,
                                               string speaker)
    {
        return ConfigureSpeakerText(new TextBlock(host, layer, view: null, worldBounds: bounds), speaker);
    }

    private static TextBlock ConfigureSpeakerText(TextBlock textBlock, string speaker)
    {
        return textBlock.SetText(speaker)
                        .SetFont(SKTypeface.Default, 17f, minSize: 11f)
                        .SetColors(new SKColor(255, 224, 145), SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
                        .SetPadding(2f, 0f)
                        .EnableWrapping(false);
    }

    private static TextBlock CreateBodyText(RenderSurfaceHostBase host,
                                            View view,
                                            Rectangle bounds,
                                            string text)
    {
        return ConfigureBodyText(new TextBlock(host, view, bounds), text);
    }

    private static TextBlock CreateBodyText(RenderSurfaceHostBase host,
                                            SceneLayer layer,
                                            Rectangle bounds,
                                            string text)
    {
        return ConfigureBodyText(new TextBlock(host, layer, view: null, worldBounds: bounds), text);
    }

    private static TextBlock ConfigureBodyText(TextBlock textBlock, string text)
    {
        return textBlock.SetText(text)
                        .SetFont(SKTypeface.Default, 16f, minSize: 10f)
                        .SetColors(SKColors.White, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Top)
                        .SetPadding(2f, 2f)
                        .EnableWrapping(true);
    }

    private static TextBlock CreateContinueIndicator(RenderSurfaceHostBase host,
                                                     View view,
                                                     Rectangle bounds)
    {
        return ConfigureContinueIndicator(new TextBlock(host, view, bounds));
    }

    private static TextBlock CreateContinueIndicator(RenderSurfaceHostBase host,
                                                     SceneLayer layer,
                                                     Rectangle bounds)
    {
        return ConfigureContinueIndicator(new TextBlock(host, layer, view: null, worldBounds: bounds));
    }

    private static TextBlock ConfigureContinueIndicator(TextBlock textBlock)
    {
        return textBlock.SetText("▼")
                        .SetFont(SKTypeface.Default, 14f, minSize: 9f)
                        .SetColors(SKColors.White, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .EnableWrapping(false);
    }

    private static Rectangle GetSpeakerBounds(Rectangle bounds)
    {
        return new Rectangle(bounds.X + ContentInset,
                             bounds.Y + 4,
                             Math.Max(1, bounds.Width - ContentInset * 2),
                             SpeakerHeight);
    }

    private static Rectangle GetBodyBounds(Rectangle bounds)
    {
        int top = bounds.Y + SpeakerHeight + 4;
        int bottom = bounds.Bottom - ContinueIndicatorHeight - 4;
        return new Rectangle(bounds.X + ContentInset,
                             top,
                             Math.Max(1, bounds.Width - ContentInset * 2),
                             Math.Max(1, bottom - top));
    }

    private static Rectangle GetContinueBounds(Rectangle bounds)
    {
        return new Rectangle(bounds.Right - ContentInset - ContinueIndicatorWidth,
                             bounds.Bottom - ContinueIndicatorHeight - 4,
                             ContinueIndicatorWidth,
                             ContinueIndicatorHeight);
    }

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        if (bounds.Width < 120 || bounds.Height < SpeakerHeight + ContinueIndicatorHeight + 24)
            throw new ArgumentOutOfRangeException(nameof(bounds), bounds, "Conversation-box bounds are too small.");

        return bounds;
    }
}
