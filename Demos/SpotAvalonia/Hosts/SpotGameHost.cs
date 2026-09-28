using Avalonia.Input;
using Gondwana.Avalonia.Hosting;
using Gondwana.Avalonia.Rendering;
using Gondwana.Input.Keyboard;
using Gondwana.Scenes;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Spot;

/// <summary>
/// Hosts the Spot demo on Gondwana's Avalonia GPU runtime and delegates game behavior
/// to the platform-neutral runtime in Spot.Shared.
/// </summary>
internal sealed partial class SpotGameHost : AvaloniaGpuGameHost
{
    private static readonly int ScoreToggleKey = GetAvaloniaKeyCode("Tab");

    private readonly SpotGameRuntime _runtime;

    internal SpotGameHost(AvaloniaGpuRenderSurfaceControl renderSurface)
        : base(renderSurface)
    {
        _runtime = new SpotGameRuntime(
            renderSurface.Host,
            scoreToggleKey: ScoreToggleKey,
            widgetInputRouterAccessor: () => WidgetInputRouter,
            configurePlatformKeyboardInput: ConfigurePlatformKeyboardInput,
            configureNewGameDialogForPlatform: ConfigureNewGameDialogForPlatform,
            persistGameState: PersistGameState);
    }

    internal bool AudioAvailable => _runtime.AudioAvailable;

    internal NewGameOptions? LastNewGameOptions => _runtime.LastNewGameOptions;

    internal void BeginPostSplashStartup() => _runtime.BeginPostSplashStartup();

    internal void OpenNewGameDialog(NewGameOptions? options = null)
        => _runtime.OpenNewGameDialog(options);

    internal void SetMusicEnabled(bool enabled) => _runtime.SetMusicEnabled(enabled);

    internal void SetSoundEffectsEnabled(bool enabled) => _runtime.SetSoundEffectsEnabled(enabled);

    internal void SetJiggleEnabled(bool enabled) => _runtime.SetJiggleEnabled(enabled);

    internal void SetCloudsEnabled(bool enabled) => _runtime.SetCloudsEnabled(enabled);

    protected override Scene CreateInitialScene()
    {
        Logging.EngineLogger.SetLogLevel(LogLevel.Information);
        return _runtime.CreateInitialScene();
    }

    protected override void OnSceneGraphCreated() => _runtime.OnSceneGraphCreated();

    protected override void OnMouseAdapterInitialized() => _runtime.OnMouseAdapterInitialized();

    protected override void OnKeyboardAdapterInitialized() => _runtime.OnKeyboardAdapterInitialized();

    protected override void UnhookEvents() => _runtime.UnhookEvents();

    protected override void CreateDirectDrawings()
    {
        // Deliberately empty: startup presentation is created in BeginPostSplashStartup()
        // so it does not appear beneath the Gondwana splash.
    }

    protected override void OnEngineStarted()
    {
        // Deliberately empty: startup music begins in BeginPostSplashStartup()
        // after the Gondwana splash has fully faded out.
    }

    private static void ConfigureNewGameDialogForPlatform(NewGameDialog dialog)
    {
        dialog.ConfigureTextInput(ConfigureTextBoxForAvalonia);
    }

    private static void ConfigurePlatformKeyboardInput(KeyboardEventPoller keyboard)
    {
        foreach (Key key in Enum.GetValues<Key>())
        {
            int keyCode = (int)key;
            if (keyCode <= 0 || keyCode == ScoreToggleKey)
                continue;

            keyboard.StartMonitoringKey(keyCode, TranslateAvaloniaKeyToVirtualKey(key).ToString());
        }
    }

    private static void PersistGameState()
    {
        Gondwana.Engine.Instance.State.SaveToFile("savegame.json", false, true);
    }

    private static void ConfigureTextBoxForAvalonia(TextBoxWidget textBox)
    {
        textBox.SubmitKey = 13;
        textBox.BackspaceKey = 8;
        textBox.DeleteKey = 46;
        textBox.LeftKey = 37;
        textBox.RightKey = 39;
        textBox.HomeKey = 36;
        textBox.EndKey = 35;
        textBox.CharacterResolver = ResolveVirtualKeyCharacter;
    }

    private static char? ResolveVirtualKeyCharacter(WidgetKeyboardEventArgs args)
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

    private static int TranslateAvaloniaKeyToVirtualKey(Key key)
    {
        string? keyName = Enum.GetName(typeof(Key), key);
        if (string.IsNullOrEmpty(keyName))
            return (int)key;

        if (keyName.Length == 1 && keyName[0] is >= 'A' and <= 'Z')
            return keyName[0];

        if (keyName.Length == 2 && keyName[0] == 'D' && char.IsAsciiDigit(keyName[1]))
            return keyName[1];

        if (keyName.StartsWith("NumPad", StringComparison.Ordinal) &&
            keyName.Length == 7 &&
            char.IsAsciiDigit(keyName[6]))
        {
            return 96 + (keyName[6] - '0');
        }

        if (keyName.Length > 1 &&
            keyName[0] == 'F' &&
            int.TryParse(keyName[1..], out int functionNumber) &&
            functionNumber is >= 1 and <= 24)
        {
            return 111 + functionNumber;
        }

        return keyName switch
        {
            "Back" or "Backspace" => 8,
            "Enter" or "Return" => 13,
            "Escape" => 27,
            "Space" => 32,
            "PageUp" => 33,
            "PageDown" => 34,
            "End" => 35,
            "Home" => 36,
            "Left" => 37,
            "Up" => 38,
            "Right" => 39,
            "Down" => 40,
            "Insert" => 45,
            "Delete" => 46,
            "Multiply" => 106,
            "Add" => 107,
            "Subtract" => 109,
            "Decimal" => 110,
            "Divide" => 111,
            "OemSemicolon" => 186,
            "OemPlus" => 187,
            "OemComma" => 188,
            "OemMinus" => 189,
            "OemPeriod" => 190,
            "OemQuestion" => 191,
            "OemTilde" => 192,
            "OemOpenBrackets" => 219,
            "OemPipe" => 220,
            "OemCloseBrackets" => 221,
            "OemQuotes" => 222,
            _ => (int)key
        };
    }

    private static int GetAvaloniaKeyCode(params string[] names)
    {
        foreach (string name in names)
        {
            if (Enum.TryParse(name, ignoreCase: false, out Key key))
                return (int)key;
        }

        throw new InvalidOperationException(
            $"None of the Avalonia key names [{string.Join(", ", names)}] are available.");
    }
}
