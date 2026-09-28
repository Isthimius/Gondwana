using Avalonia.Input;
using Gondwana.Avalonia.Hosting;
using Gondwana.Avalonia.Rendering;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;

namespace Gondwana.Demos.Spot;

/// <summary>
/// Hosts the Spot demo on Gondwana's Avalonia GPU runtime.
/// Game-specific responsibilities are supplied by the shared Spot host partials.
/// </summary>
internal sealed partial class SpotGameHost : AvaloniaGpuGameHost
{
    private const int PersistentMenuHeight = 32;

    private static readonly int ScoreToggleKey = GetAvaloniaKeyCode("Tab");

    private Scene ActiveScene => Scene
        ?? throw new InvalidOperationException("Spot scene has not been created.");

    private RenderSurfaceHostBase SurfaceHost => RenderSurface.Host;
    private int SurfaceWidth => SurfaceHost.Backbuffer.Width;
    private int SurfaceHeight => SurfaceHost.Backbuffer.Height;

    internal SpotGameHost(AvaloniaGpuRenderSurfaceControl renderSurface)
        : base(renderSurface)
    {
    }

    internal bool AudioAvailable => _music is not null;

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

    partial void ConfigureNewGameDialogForPlatform(NewGameDialog dialog)
    {
        dialog.ConfigureTextInput(ConfigureTextBoxForAvalonia);
    }

    partial void ConfigurePlatformKeyboardInput(KeyboardEventPoller keyboard)
    {
        foreach (Key key in Enum.GetValues<Key>())
        {
            int keyCode = (int)key;
            if (keyCode > 0)
                keyboard.StartMonitoringKey(keyCode);
        }
    }

    partial void PersistGameState()
    {
        Engine.Instance.State.SaveToFile("savegame.json", false, true);
    }

    private static void ConfigureTextBoxForAvalonia(TextBoxWidget textBox)
    {
        textBox.SubmitKey = GetAvaloniaKeyCode("Enter", "Return");
        textBox.BackspaceKey = GetAvaloniaKeyCode("Back", "Backspace");
        textBox.DeleteKey = GetAvaloniaKeyCode("Delete");
        textBox.LeftKey = GetAvaloniaKeyCode("Left");
        textBox.RightKey = GetAvaloniaKeyCode("Right");
        textBox.HomeKey = GetAvaloniaKeyCode("Home");
        textBox.EndKey = GetAvaloniaKeyCode("End");
        textBox.CharacterResolver = ResolveAvaloniaCharacter;
    }

    private static char? ResolveAvaloniaCharacter(WidgetKeyboardEventArgs args)
    {
        if ((args.Modifiers & (KeyboardModifierState.Ctrl | KeyboardModifierState.Alt)) != 0)
            return null;

        string? keyName = Enum.GetName(typeof(Key), args.Key);
        if (string.IsNullOrEmpty(keyName))
            return null;

        bool shift = (args.Modifiers & KeyboardModifierState.Shift) != 0;

        if (keyName.Length == 1 && keyName[0] is >= 'A' and <= 'Z')
            return shift ? keyName[0] : char.ToLowerInvariant(keyName[0]);

        if (keyName.Length == 2 && keyName[0] == 'D' && char.IsAsciiDigit(keyName[1]))
            return shift ? ResolveShiftedDigit(keyName[1]) : keyName[1];

        if (keyName.StartsWith("NumPad", StringComparison.Ordinal) &&
            keyName.Length == 7 &&
            char.IsAsciiDigit(keyName[6]))
        {
            return keyName[6];
        }

        return keyName switch
        {
            "Space" => ' ',
            "OemMinus" => shift ? '_' : '-',
            "OemPlus" => shift ? '+' : '=',
            "OemOpenBrackets" => shift ? '{' : '[',
            "OemCloseBrackets" => shift ? '}' : ']',
            "OemPipe" => shift ? '|' : '\\',
            "OemSemicolon" => shift ? ':' : ';',
            "OemQuotes" => shift ? '"' : '\'',
            "OemTilde" => shift ? '~' : '`',
            "OemComma" => shift ? '<' : ',',
            "OemPeriod" => shift ? '>' : '.',
            "OemQuestion" => shift ? '?' : '/',
            _ => null
        };
    }

    private static char ResolveShiftedDigit(char digit)
    {
        return digit switch
        {
            '1' => '!',
            '2' => '@',
            '3' => '#',
            '4' => '$',
            '5' => '%',
            '6' => '^',
            '7' => '&',
            '8' => '*',
            '9' => '(',
            '0' => ')',
            _ => digit
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
