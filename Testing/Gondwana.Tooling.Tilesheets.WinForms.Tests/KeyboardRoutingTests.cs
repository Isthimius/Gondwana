using System.Runtime.InteropServices;
using Gondwana.Tooling.Tilesheets.Editing;

namespace Gondwana.Tooling.Tilesheets.WinForms.Tests;

public sealed partial class EditorInteractionTests
{
    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] state);
    [DllImport("user32.dll")]
    private static extern bool SetKeyboardState(byte[] state);
    [DllImport("user32.dll")]
    private static extern nint GetFocus();
    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint window, int message, nint key, nint data);

    // Exercise WinForms' message preprocessing starting at the actual focused HWND,
    // including child controls and docking panes, rather than invoking a host override.
    private static void RouteKey(Keys keys)
    {
        var saved = new byte[256];
        Assert.True(GetKeyboardState(saved));
        var state = new byte[256];
        if (keys.HasFlag(Keys.Control)) state[(int)Keys.ControlKey] = 0x80;
        if (keys.HasFlag(Keys.Shift)) state[(int)Keys.ShiftKey] = 0x80;
        Assert.True(SetKeyboardState(state));
        try
        {
            Assert.True(PostMessage(GetFocus(), 0x100, (nint)(keys & Keys.KeyCode), 0));
            Application.DoEvents();
        }
        finally { Assert.True(SetKeyboardState(saved)); }
        Application.DoEvents();
    }

    [Theory]
    [InlineData("canvas")]
    [InlineData("property")]
    [InlineData("propertyValue")]
    [InlineData("validation")]
    [InlineData("region")]
    public void FocusedCanvasRoutesKeyboardMessagesToDocument(string location) => RunSta(() =>
    {
        using var document = TilesheetDocument.Create(Path.GetTempPath());
        using var host = Show(document);
        document.RenameDefinition("changed");
        switch (location)
        {
            case "property":
            case "propertyValue":
                var grid = Field<PropertyGrid>(host.Editor, "_definitionProperties");
                grid.SelectedGridItem = Items(grid).First(item => item.PropertyDescriptor?.Name == "Name");
                grid.Focus();
                if (location == "propertyValue")
                    Assert.True(Descendants(grid).OfType<TextBox>().Single(t => t.Visible).Focus());
                break;
            case "validation": Field<TextBox>(host.Editor, "_validation").Focus(); break;
            case "region": Field<ComboBox>(host.Editor, "_regions").Focus(); break;
            default: Field<ImageViewport>(host.Editor, "_viewport").Focus(); break;
        }
        RouteKey(Keys.Control | Keys.Z);
        Assert.Equal("Untitled", document.Definition.Name);
        RouteKey(Keys.Control | Keys.Y);
        Assert.Equal("changed", document.Definition.Name);
    });
}
