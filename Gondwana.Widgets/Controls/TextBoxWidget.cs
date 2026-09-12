using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Provides a single-line editable text box with keyboard caret navigation.
/// </summary>
/// <remarks>
/// Gondwana keyboard events expose host key codes. The default key bindings and
/// character resolver use Windows virtual-key values, matching the existing
/// widget keyboard conventions. Other hosts can replace <see cref="CharacterResolver"/>
/// and the configurable special-key properties without subclassing the widget.
/// Text can also be supplied directly through <see cref="InsertText(string?)"/>.
/// </remarks>
public sealed class TextBoxWidget : WidgetBase
{
    private Color _backgroundColor = Color.FromArgb(255, 32, 32, 40);
    private Color _borderColor = Color.FromArgb(255, 135, 135, 150);
    private Color _focusedBorderColor = Color.FromArgb(255, 105, 155, 230);
    private SKColor _textColor = SKColors.White;
    private SKColor _placeholderColor = new(170, 170, 180);

    private string _text = string.Empty;
    private string _placeholder = string.Empty;
    private int _caretIndex;
    private int? _maxLength;

    /// <summary>
    /// Occurs when the text changes.
    /// </summary>
    public event Action<string>? TextChanged;

    /// <summary>
    /// Occurs when the configured submit key is pressed.
    /// </summary>
    public event Action<string>? Submitted;

    /// <summary>
    /// Creates a view-level text box.
    /// </summary>
    public TextBoxWidget(RenderSurfaceHostBase renderSurfaceHost,
                         View view,
                         Rectangle bounds,
                         string text = "",
                         string? placeholder = null,
                         string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        _text = NormalizeText(text);
        _placeholder = placeholder ?? string.Empty;
        _caretIndex = _text.Length;

        Background = CreateBackground(renderSurfaceHost, view, bounds);
        TextBlock = CreateTextBlock(renderSurfaceHost, view, bounds);

        Add(Background);
        Add(TextBlock);

        CompleteInitialization();
    }

    /// <summary>
    /// Creates a scene-layer text box.
    /// </summary>
    public TextBoxWidget(RenderSurfaceHostBase renderSurfaceHost,
                         SceneLayer sceneLayer,
                         Rectangle bounds,
                         string text = "",
                         string? placeholder = null,
                         string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);

        _text = NormalizeText(text);
        _placeholder = placeholder ?? string.Empty;
        _caretIndex = _text.Length;

        Background = CreateBackground(renderSurfaceHost, sceneLayer, bounds);
        TextBlock = CreateTextBlock(renderSurfaceHost, sceneLayer, bounds);

        Add(Background);
        Add(TextBlock);

        CompleteInitialization();
    }

    /// <summary>
    /// Gets the text-box background and border drawing.
    /// </summary>
    public DirectRectangle Background { get; }

    /// <summary>
    /// Gets the text drawing used by the text box.
    /// </summary>
    public TextBlock TextBlock { get; }

    /// <summary>
    /// Gets or sets the current text.
    /// </summary>
    public string Text
    {
        get => _text;
        set => SetText(value);
    }

    /// <summary>
    /// Gets or sets placeholder text shown while the control is empty and unfocused.
    /// </summary>
    public string Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value ?? string.Empty;
            RefreshDisplayedText();
        }
    }

    /// <summary>
    /// Gets or sets the caret position in the range zero through <see cref="Text"/> length.
    /// </summary>
    public int CaretIndex
    {
        get => _caretIndex;
        set
        {
            _caretIndex = Math.Clamp(value, 0, _text.Length);
            RefreshDisplayedText();
        }
    }

    /// <summary>
    /// Gets or sets the maximum permitted text length. A null value means unlimited.
    /// </summary>
    public int? MaxLength
    {
        get => _maxLength;
        set
        {
            if (value.HasValue && value.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            _maxLength = value;

            if (_maxLength.HasValue && _text.Length > _maxLength.Value)
                SetText(_text[.._maxLength.Value]);
        }
    }

    /// <summary>
    /// Gets or sets whether user editing is disabled.
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Gets the text-box bounds in its native coordinate space.
    /// </summary>
    public Rectangle Bounds => Mode == DirectDrawingMode.View
        ? Background.ScreenBounds
        : Background.WorldBounds;

    /// <summary>
    /// Gets or sets the key code used to submit the text. Defaults to Enter (13).
    /// </summary>
    public int SubmitKey { get; set; } = 13;

    /// <summary>
    /// Gets or sets the key code used for backspace. Defaults to 8.
    /// </summary>
    public int BackspaceKey { get; set; } = 8;

    /// <summary>
    /// Gets or sets the key code used for Delete. Defaults to 46.
    /// </summary>
    public int DeleteKey { get; set; } = 46;

    /// <summary>
    /// Gets or sets the key code used to move left. Defaults to 37.
    /// </summary>
    public int LeftKey { get; set; } = 37;

    /// <summary>
    /// Gets or sets the key code used to move right. Defaults to 39.
    /// </summary>
    public int RightKey { get; set; } = 39;

    /// <summary>
    /// Gets or sets the key code used to move to the beginning. Defaults to 36.
    /// </summary>
    public int HomeKey { get; set; } = 36;

    /// <summary>
    /// Gets or sets the key code used to move to the end. Defaults to 35.
    /// </summary>
    public int EndKey { get; set; } = 35;

    /// <summary>
    /// Gets or sets the function used to convert routed key events into printable characters.
    /// </summary>
    public Func<WidgetKeyboardEventArgs, char?> CharacterResolver { get; set; } = ResolveWindowsVirtualKeyCharacter;

    /// <summary>
    /// Replaces the text and moves the caret to the end.
    /// </summary>
    public TextBoxWidget SetText(string? text)
    {
        string normalized = NormalizeText(text);

        if (MaxLength.HasValue && normalized.Length > MaxLength.Value)
            normalized = normalized[..MaxLength.Value];

        if (_text == normalized)
        {
            _caretIndex = Math.Min(_caretIndex, _text.Length);
            RefreshDisplayedText();
            return this;
        }

        _text = normalized;
        _caretIndex = _text.Length;
        RefreshDisplayedText();
        TextChanged?.Invoke(_text);
        return this;
    }

    /// <summary>
    /// Inserts text at the current caret position.
    /// </summary>
    public TextBoxWidget InsertText(string? text)
    {
        if (IsReadOnly || string.IsNullOrEmpty(text))
            return this;

        string insertion = NormalizeText(text);
        if (insertion.Length == 0)
            return this;

        if (MaxLength.HasValue)
        {
            int available = MaxLength.Value - _text.Length;
            if (available <= 0)
                return this;

            if (insertion.Length > available)
                insertion = insertion[..available];
        }

        _text = _text.Insert(_caretIndex, insertion);
        _caretIndex += insertion.Length;
        RefreshDisplayedText();
        TextChanged?.Invoke(_text);
        return this;
    }

    /// <summary>
    /// Removes the character before the caret when possible.
    /// </summary>
    public TextBoxWidget Backspace()
    {
        if (IsReadOnly || _caretIndex <= 0 || _text.Length == 0)
            return this;

        _text = _text.Remove(_caretIndex - 1, 1);
        _caretIndex--;
        RefreshDisplayedText();
        TextChanged?.Invoke(_text);
        return this;
    }

    /// <summary>
    /// Removes the character at the caret when possible.
    /// </summary>
    public TextBoxWidget Delete()
    {
        if (IsReadOnly || _caretIndex < 0 || _caretIndex >= _text.Length)
            return this;

        _text = _text.Remove(_caretIndex, 1);
        RefreshDisplayedText();
        TextChanged?.Invoke(_text);
        return this;
    }

    /// <summary>
    /// Moves the caret by the specified character delta.
    /// </summary>
    public TextBoxWidget MoveCaret(int delta)
    {
        CaretIndex = Math.Clamp(_caretIndex + delta, 0, _text.Length);
        return this;
    }

    /// <summary>
    /// Sets the background and border colors used for normal and focused states.
    /// </summary>
    public TextBoxWidget SetColors(Color background,
                                   Color border,
                                   Color focusedBorder)
    {
        _backgroundColor = background;
        _borderColor = border;
        _focusedBorderColor = focusedBorder;

        Background.SetColor(_backgroundColor)
                  .SetBorderColor(IsFocused ? _focusedBorderColor : _borderColor);
        Refresh(Background);
        return this;
    }

    /// <summary>
    /// Sets the text and placeholder colors.
    /// </summary>
    public TextBoxWidget SetTextColors(SKColor text, SKColor placeholder)
    {
        _textColor = text;
        _placeholderColor = placeholder;
        RefreshDisplayedText();
        return this;
    }

    /// <summary>
    /// Sets the base Z-order used by the text-box visuals.
    /// </summary>
    public TextBoxWidget SetTextBoxZOrder(int zOrder)
    {
        Background.ZOrder = zOrder;
        TextBlock.ZOrder = zOrder + 1;
        return this;
    }

    /// <inheritdoc/>
    protected override void OnPointerClick(WidgetPointerEventArgs args)
    {
        base.OnPointerClick(args);

        if (!args.IsPrimaryButton)
            return;

        _caretIndex = _text.Length;
        RefreshDisplayedText();
        args.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnFocusGained()
    {
        base.OnFocusGained();
        Background.SetBorderColor(_focusedBorderColor);
        Refresh(Background);
        RefreshDisplayedText();
    }

    /// <inheritdoc/>
    protected override void OnFocusLost()
    {
        base.OnFocusLost();
        Background.SetBorderColor(_borderColor);
        Refresh(Background);
        RefreshDisplayedText();
    }

    /// <inheritdoc/>
    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);

        if (args.KeyAction is not KeyAction.Pressed and not KeyAction.Repeated)
            return;

        if (args.Key == SubmitKey)
        {
            args.Handled = true;
            Submitted?.Invoke(_text);
            return;
        }

        if (args.Key == LeftKey)
        {
            args.Handled = true;
            MoveCaret(-1);
            return;
        }

        if (args.Key == RightKey)
        {
            args.Handled = true;
            MoveCaret(1);
            return;
        }

        if (args.Key == HomeKey)
        {
            args.Handled = true;
            CaretIndex = 0;
            return;
        }

        if (args.Key == EndKey)
        {
            args.Handled = true;
            CaretIndex = _text.Length;
            return;
        }

        if (args.Key == BackspaceKey)
        {
            args.Handled = true;
            Backspace();
            return;
        }

        if (args.Key == DeleteKey)
        {
            args.Handled = true;
            Delete();
            return;
        }

        if (IsReadOnly)
            return;

        char? character = CharacterResolver(args);
        if (!character.HasValue)
            return;

        args.Handled = true;
        InsertText(character.Value.ToString());
    }

    private void CompleteInitialization()
    {
        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;
        SetTextBoxZOrder(0);
        RefreshDisplayedText();
    }

    private void RefreshDisplayedText()
    {
        if (IsFocused)
        {
            string withCaret = _text.Insert(_caretIndex, "|");
            TextBlock.SetText(withCaret)
                     .SetColors(_textColor, SKColors.Transparent);
            return;
        }

        bool showPlaceholder = _text.Length == 0 && _placeholder.Length > 0;
        TextBlock.SetText(showPlaceholder ? _placeholder : _text)
                 .SetColors(showPlaceholder ? _placeholderColor : _textColor, SKColors.Transparent);
    }

    private DirectRectangle CreateBackground(RenderSurfaceHostBase host,
                                             View view,
                                             Rectangle bounds)
    {
        return ConfigureBackground(new DirectRectangle(_backgroundColor, host, view, bounds));
    }

    private DirectRectangle CreateBackground(RenderSurfaceHostBase host,
                                             SceneLayer layer,
                                             Rectangle bounds)
    {
        return ConfigureBackground(new DirectRectangle(_backgroundColor, host, layer, bounds));
    }

    private DirectRectangle ConfigureBackground(DirectRectangle background)
    {
        return background.SetFilled(true)
                         .SetBorderColor(_borderColor)
                         .SetStrokeWidth(1.5f)
                         .SetCornerRadius(4f);
    }

    private static TextBlock CreateTextBlock(RenderSurfaceHostBase host,
                                             View view,
                                             Rectangle bounds)
    {
        return ConfigureTextBlock(new TextBlock(host, view, bounds));
    }

    private static TextBlock CreateTextBlock(RenderSurfaceHostBase host,
                                             SceneLayer layer,
                                             Rectangle bounds)
    {
        return ConfigureTextBlock(new TextBlock(host, layer, view: null, worldBounds: bounds));
    }

    private static TextBlock ConfigureTextBlock(TextBlock textBlock)
    {
        return textBlock.SetFont(SKTypeface.Default, 16f, minSize: 10f)
                        .SetColors(SKColors.White, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
                        .SetPadding(8f, 0f)
                        .EnableWrapping(false);
    }

    private static string NormalizeText(string? text)
    {
        return (text ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }

    private static char? ResolveWindowsVirtualKeyCharacter(WidgetKeyboardEventArgs args)
    {
        if ((args.Modifiers & (KeyboardModifierState.Ctrl | KeyboardModifierState.Alt)) != 0)
            return null;

        bool shift = (args.Modifiers & KeyboardModifierState.Shift) != 0;
        int key = args.Key;

        if (key is >= 65 and <= 90)
        {
            char letter = (char)key;
            return shift ? letter : char.ToLowerInvariant(letter);
        }

        if (key is >= 48 and <= 57)
        {
            if (!shift)
                return (char)key;

            const string shiftedDigits = ")!@#$%^&*(";
            return shiftedDigits[key - 48];
        }

        if (key is >= 96 and <= 105)
            return (char)('0' + key - 96);

        return key switch
        {
            32 => ' ',
            106 => '*',
            107 => '+',
            109 => '-',
            110 => '.',
            111 => '/',
            186 => shift ? ':' : ';',
            187 => shift ? '+' : '=',
            188 => shift ? '<' : ',',
            189 => shift ? '_' : '-',
            190 => shift ? '>' : '.',
            191 => shift ? '?' : '/',
            192 => shift ? '~' : '`',
            219 => shift ? '{' : '[',
            220 => shift ? '|' : '\\',
            221 => shift ? '}' : ']',
            222 => shift ? '"' : '\'',
            _ => null
        };
    }

    private static void Refresh(DirectRectangle rectangle)
    {
        rectangle.SetPosition(rectangle.GetPosition());
    }

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        if (bounds.Width < 40 || bounds.Height < 18)
            throw new ArgumentOutOfRangeException(nameof(bounds), bounds, "Text-box bounds are too small.");

        return bounds;
    }
}
