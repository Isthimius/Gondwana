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
/// Platform-neutral game behavior is compiled from Spot.Shared.
/// </summary>
internal sealed partial class SpotGameHost : AvaloniaGpuGameHost
{
    private const int PersistentMenuHeight = SpotMenuFactory.Height;
    private const int ScoreToggleKey = (int)Key.Tab;

    private Scene ActiveScene => Scene
        ?? throw new InvalidOperationException("Spot scene has not been created.");

    private RenderSurfaceHostBase SurfaceHost => RenderSurface.Host;
    private int SurfaceWidth => RenderSurface.Adapter.Width;
    private int SurfaceHeight => RenderSurface.Adapter.Height;

    internal SpotGameHost(AvaloniaGpuRenderSurfaceControl renderSurface)
        : base(renderSurface)
    {
    }

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

    partial void ConfigurePlatformKeyboardInput(KeyboardEventPoller keyboard)
    {
        RegisterRange(keyboard, Key.A, Key.Z);
        RegisterRange(keyboard, Key.D0, Key.D9);
        RegisterRange(keyboard, Key.NumPad0, Key.NumPad9);

        foreach (Key key in new[]
                 {
                     Key.Back,
                     Key.Enter,
                     Key.Space,
                     Key.End,
                     Key.Home,
                     Key.Left,
                     Key.Right,
                     Key.Delete,
                     Key.Add,
                     Key.Subtract,
                     Key.Multiply,
                     Key.Divide,
                     Key.Decimal
                 })
        {
            keyboard.StartMonitoringKey((int)key);
        }

        static void RegisterRange(KeyboardEventPoller poller, Key first, Key last)
        {
            for (int key = (int)first; key <= (int)last; key++)
                poller.StartMonitoringKey(key);
        }
    }

    partial void ConfigureNewGameDialogForPlatform(NewGameDialog dialog)
    {
        dialog.ConfigureTextInput(ConfigureTextBoxForAvalonia);
    }

    partial void PersistGameState()
    {
        Engine.Instance.State.SaveToFile("savegame.json", false, true);
    }

    private static void ConfigureTextBoxForAvalonia(TextBoxWidget textBox)
    {
        textBox.SubmitKey = (int)Key.Enter;
        textBox.BackspaceKey = (int)Key.Back;
        textBox.DeleteKey = (int)Key.Delete;
        textBox.LeftKey = (int)Key.Left;
        textBox.RightKey = (int)Key.Right;
        textBox.HomeKey = (int)Key.Home;
        textBox.EndKey = (int)Key.End;
        textBox.CharacterResolver = ResolveAvaloniaCharacter;
    }

    private static char? ResolveAvaloniaCharacter(WidgetKeyboardEventArgs args)
    {
        if ((args.Modifiers & (KeyboardModifierState.Ctrl | KeyboardModifierState.Alt)) != 0)
            return null;

        bool shift = (args.Modifiers & KeyboardModifierState.Shift) != 0;
        var key = (Key)args.Key;

        if (key is >= Key.A and <= Key.Z)
        {
            char letter = (char)('a' + ((int)key - (int)Key.A));
            return shift ? char.ToUpperInvariant(letter) : letter;
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            int digit = (int)key - (int)Key.D0;
            if (!shift)
                return (char)('0' + digit);

            const string shiftedDigits = ")!@#$%^&*(";
            return shiftedDigits[digit];
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
            return (char)('0' + ((int)key - (int)Key.NumPad0));

        return key switch
        {
            Key.Space => ' ',
            Key.Add => '+',
            Key.Subtract => '-',
            Key.Multiply => '*',
            Key.Divide => '/',
            Key.Decimal => '.',
            _ => null
        };
    }
}
