using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Scenes;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Menus;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;
using View = Gondwana.Rendering.Views.View;

namespace WidgetsTest;

/// <summary>Executable examples of the recommended callback-oriented menu API.</summary>
internal sealed class WidgetsTestHost : WinFormsGpuGameHost
{
    private MenuBarWidget _menuBar = null!;
    private LabelWidget _status = null!;
    private LabelWidget _instructions = null!;
    private TextBoxWidget _editor = null!;
    private DirectComposite _grid = null!;
    private SKImage? _icon;
    private string _lastCommand = "Ready";
    private string _units = "Pixels";
    private bool _documentIsDirty;

    internal WidgetsTestHost(WinFormGpuRenderSurfaceControl renderSurface) : base(renderSurface) { }

    protected override Scene CreateInitialScene() => Scene.Empty;

    protected override void CreateInitialViews() => RenderSurface.Host.ViewManager.ConfigureSingleFullView();

    protected override void CreateDirectDrawings()
    {
        var host = RenderSurface.Host;
        View view = host.ViewManager.Views[0];
        _instructions = new LabelWidget(host, view, new Rectangle(24, 70, 590, 110),
            "Menus: click, hover, Alt+F/E/V/H, arrows, Enter, Space, Escape.\n" +
            "File > Export > Image demonstrates deep submenus.\n" +
            "Type below to enable Save. Ctrl+S works while editing;\n" +
            "Ctrl+Z is consumed by the editor before menu fallback.")
            .SetFont(SKTypeface.Default, 16);
        _instructions.Show();

        _editor = new TextBoxWidget(host, view, new Rectangle(24, 205, 590, 38),
            placeholder: "Click here, type, then try Ctrl+S or Ctrl+Z");
        _editor.TextChanged += OnDocumentChanged;
        _editor.KeyboardInput += OnEditorKeyboardInput;
        _editor.Show();

        _status = new LabelWidget(host, view, new Rectangle(24, 270, 590, 105))
            .SetFont(SKTypeface.Default, 18);
        _status.Show();
        BuildGrid(view);
        BuildMenuBar(view);
        UpdateStatus();
    }

    private void BuildGrid(View view)
    {
        _grid = new DirectComposite(RenderSurface.Host, DirectDrawingMode.View);
        for (int x = 24; x <= 600; x += 32)
            _grid.Add(new DirectRectangle(Color.FromArgb(90, 120, 150), RenderSurface.Host, view,
                new Rectangle(x, 400, 1, 192)).SetFilled(true));
        for (int y = 400; y <= 592; y += 32)
            _grid.Add(new DirectRectangle(Color.FromArgb(90, 120, 150), RenderSurface.Host, view,
                new Rectangle(24, y, 576, 1)).SetFilled(true));
    }

    private void BuildMenuBar(View view)
    {
        // SKImage is shared/caller-owned, matching DirectImage's normal ownership model.
        using var bitmap = new SKBitmap(18, 18);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = SKColors.Gold, IsAntialias = true };
            canvas.DrawCircle(9, 9, 7, paint);
        }
        _icon = SKImage.FromBitmap(bitmap);
        _menuBar = new MenuBarWidget(RenderSurface.Host, view,
            new Rectangle(0, 0, view.Viewport.TargetRectPx.Width, 32));
        _menuBar.AddMenu("File", BuildFileMenu, mnemonic: 'F')
            .AddMenu("Edit", BuildEditMenu, mnemonic: 'E')
            .AddMenu("View", BuildViewMenu, mnemonic: 'V')
            .AddMenu("Help", help => help.AddItem("About", OnAbout, mnemonic: 'A'), mnemonic: 'H');
        _menuBar.Show();
    }

    private void BuildFileMenu(MenuDropDownWidget fileMenu)
    {
        fileMenu.AddItem("New", OnNew, key: "file.new", shortcut: KeyGesture.Ctrl('N'), mnemonic: 'N')
            .AddItem("Open...", OnOpen, key: "file.open", shortcut: KeyGesture.Ctrl('O'), mnemonic: 'O')
            .AddSubMenu("Recent Files", BuildRecentFiles, mnemonic: 'R')
            .AddSubMenu("Export", export => export.AddSubMenu("Image", image => image
                .AddItem("PNG", OnExportPng, key: "file.export.png", shortcut: KeyGesture.CtrlShift('P'), mnemonic: 'P')
                .AddItem("JPEG", OnExportJpeg, mnemonic: 'J'), mnemonic: 'I'), mnemonic: 'E')
            .AddSeparator()
            .AddItem("Save", OnSave, enabled: false, key: "file.save", shortcut: KeyGesture.Ctrl('S'), mnemonic: 'S')
            .AddSeparator()
            .AddItem("Exit", OnExit, mnemonic: 'X');
    }

    private void BuildRecentFiles(MenuDropDownWidget recent)
    {
        foreach (string file in new[] { "Game.gws", "Demo.gws" })
            recent.AddItem(file, () => OnOpenRecent(file));
    }

    private void BuildEditMenu(MenuDropDownWidget editMenu)
    {
        editMenu.AddItem("Undo", OnUndo, shortcut: KeyGesture.Ctrl('Z'), mnemonic: 'U')
            .AddItem("Redo", OnRedo, enabled: false, key: "edit.redo", shortcut: KeyGesture.Ctrl('Y'), mnemonic: 'R');
    }

    private void BuildViewMenu(MenuDropDownWidget viewMenu)
    {
        viewMenu.AddCheckItem("Show Grid", OnToggleGrid, isChecked: true, key: "view.grid",
                shortcut: KeyGesture.Ctrl('G'), mnemonic: 'G')
            .AddSeparator()
            .AddRadioItem("Pixels", OnUsePixels, "units", isChecked: true, key: "view.pixels", mnemonic: 'P')
            .AddRadioItem("Tiles", OnUseTiles, "units", key: "view.tiles", mnemonic: 'T')
            .AddSeparator()
            .AddItem("An item with an icon", OnIconCommand, icon: _icon, mnemonic: 'I')
            .AddSubMenu("Animation", animations => animations
                .AddRadioItem("None", OnAnimationNone, "animation")
                .AddRadioItem("Fade", OnAnimationFade, "animation")
                .AddRadioItem("Fade and Reveal", OnAnimationReveal, "animation", isChecked: true), mnemonic: 'A');
    }

    private void OnNew()
    {
        _editor.SetText("");
        SetDocumentDirty(false);
        // Programmatic state changes are silent: explicitly apply the same state to the drawing.
        _menuBar.GetItem("view.grid").SetChecked(true);
        _grid.SetIsVisible(true);
        SetStatus("New (grid reset)");
    }
    private void OnOpen() { SetDocumentDirty(true); SetStatus("Open (sample document modified)"); }
    private void OnOpenRecent(string file) { SetDocumentDirty(true); SetStatus($"Opened {file}"); }
    private void OnSave() { SetDocumentDirty(false); SetStatus("Save"); }
    private void OnExit() => RenderSurface.BeginInvoke(() => RenderSurface.FindForm()?.Close());
    private void OnExportPng() => SetStatus("Export PNG");
    private void OnExportJpeg() => SetStatus("Export JPEG");
    private void OnUndo() { _menuBar["edit.redo"].SetEnabled(true); SetStatus("Menu Undo (Redo enabled)"); }
    private void OnRedo() { _menuBar["edit.redo"].SetEnabled(false); SetStatus("Menu Redo"); }
    private void OnToggleGrid(bool isChecked) { _grid.SetIsVisible(isChecked); SetStatus("Show Grid changed"); }
    private void OnUsePixels() { _units = "Pixels"; SetStatus("Units changed"); }
    private void OnUseTiles() { _units = "Tiles"; SetStatus("Units changed"); }
    private void OnIconCommand() => SetStatus("Icon command");
    private void OnAbout() => SetStatus("Gondwana menus: callbacks, keys, gestures and nested ownership");
    private void OnAnimationNone() { _menuBar.DropDownAnimation = MenuDropDownAnimation.None; SetStatus("Animation: None"); }
    private void OnAnimationFade() { _menuBar.DropDownAnimation = MenuDropDownAnimation.Fade; SetStatus("Animation: Fade"); }
    private void OnAnimationReveal() { _menuBar.DropDownAnimation = MenuDropDownAnimation.FadeAndReveal; SetStatus("Animation: Fade and Reveal"); }
    private void OnDocumentChanged(string text) { SetDocumentDirty(true); SetStatus("Document edited"); }

    private void OnEditorKeyboardInput(WidgetKeyboardEventArgs args)
    {
        if (args.KeyAction != KeyAction.Pressed || args.Key != 'Z' || args.Modifiers != KeyboardModifierState.Ctrl) return;
        args.Handled = true;
        SetStatus("Editor consumed Ctrl+Z; menu Undo did not execute");
    }

    private void SetDocumentDirty(bool dirty)
    {
        _documentIsDirty = dirty;
        MenuItemWidget saveItem = _menuBar.GetItem("file.save");
        saveItem.SetEnabled(_documentIsDirty);
    }
    private void SetStatus(string command) { _lastCommand = command; UpdateStatus(); }
    private void UpdateStatus() => _status.SetText(
        $"Last command: {_lastCommand}\nShow Grid: {(_menuBar["view.grid"].IsChecked ? "On" : "Off")}\n" +
        $"Units: {_units}    Save: {(_documentIsDirty ? "Enabled" : "Disabled")}");

    protected override void OnKeyboardAdapterInitialized()
    {
        // Hosts monitor keys explicitly; menu gestures use the same adapter key codes.
        var keyboard = Engine.Input.KeyboardEventPoller!;
        const double repeatIntervalSec = 0.10;
        for (int key = 'A'; key <= 'Z'; key++)
            keyboard.StartMonitoringKey(key, timeBetweenEvents: repeatIntervalSec);
        foreach (int key in new[] { 8, 13, 27, 32, 35, 36, 37, 38, 39, 40, 46 })
            keyboard.StartMonitoringKey(key, timeBetweenEvents: repeatIntervalSec);
    }

    protected override void UnhookEvents()
    {
        if (_editor is null) return;
        _editor.TextChanged -= OnDocumentChanged;
        _editor.KeyboardInput -= OnEditorKeyboardInput;
    }

    protected override void OnDisposing()
    {
        _menuBar?.Dispose();
        _editor?.Dispose();
        _instructions?.Dispose();
        _status?.Dispose();
        _grid?.Dispose();
        _icon?.Dispose();
    }
}
