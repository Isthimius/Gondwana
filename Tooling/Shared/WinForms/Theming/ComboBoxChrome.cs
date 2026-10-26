namespace Gondwana.Tooling.WinForms.Theming;

/// <summary>Paints the native combo arrow after Windows paints, retaining native keyboard/dropdown behavior.</summary>
internal sealed class ComboBoxChrome(ComboBox combo) : NativeWindow
{
    public static void Attach(ComboBox combo)
    {
        var chrome = new ComboBoxChrome(combo);

        combo.HandleCreated += (_, _) => chrome.AssignHandle(combo.Handle);
        combo.HandleDestroyed += (_, _) => chrome.ReleaseHandle();
        combo.Disposed += (_, _) => chrome.ReleaseHandle();

        combo.GotFocus += (_, _) => combo.Invalidate();
        combo.LostFocus += (_, _) => combo.Invalidate();

        if (combo.IsHandleCreated)
            chrome.AssignHandle(combo.Handle);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        const int WmPaint = 0x000F;
        const int WmPrintClient = 0x0318;
        const int WmPrint = 0x0317;

        if ((m.Msg != WmPaint && m.Msg != WmPrintClient && m.Msg != WmPrint)
            || combo.IsDisposed
            || combo.DropDownStyle == ComboBoxStyle.Simple)
        {
            return;
        }

        var p = ToolingAppearance.Palette;

        using var graphics = m.Msg != WmPaint ? Graphics.FromHdc(m.WParam) : Graphics.FromHwnd(Handle);

        int width = SystemInformation.VerticalScrollBarWidth;
        var button = new Rectangle(combo.RightToLeft == RightToLeft.Yes ? 1 : combo.ClientSize.Width - width - 1,
            1, width, combo.ClientSize.Height - 2);

        using var background = new SolidBrush(p.Surface);
        using var foreground = new SolidBrush(combo.Enabled ? p.Foreground : p.DisabledForeground);
        using var border = new Pen(combo.Focused ? p.Accent : p.Border);

        graphics.FillRectangle(background, button);
        int x = button.Left + button.Width / 2;
        int y = button.Top + button.Height / 2;
        graphics.FillPolygon(foreground, new Point[]
        {
            new Point(x - 3, y - 1), new Point(x + 3, y - 1), new Point(x, y + 2)
        });

        graphics.DrawRectangle(border, 0, 0, combo.ClientSize.Width - 1, combo.ClientSize.Height - 1);
    }
}
