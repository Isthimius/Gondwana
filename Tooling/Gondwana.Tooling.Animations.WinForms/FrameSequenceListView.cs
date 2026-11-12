namespace Gondwana.Tooling.Animations.WinForms;

internal sealed class FrameSequenceListView : ListView
{
    private int _insertionIndex = -1;

    internal int InsertionIndex
    {
        get => _insertionIndex;
        set
        {
            if (_insertionIndex == value)
                return;

            _insertionIndex = value;
            Invalidate();
        }
    }

    internal int GetInsertionIndex(Point point)
    {
        if (!ClientRectangle.Contains(point))
            return -1;

        if (Items.Count == 0)
            return 0;

        int first = TopItem?.Index ?? 0;
        for (int index = first; index < Items.Count; index++)
        {
            Rectangle row = GetItemRect(index);
            if (point.Y < row.Top + row.Height / 2)
                return index;
        }

        return Items.Count;
    }

    protected override void WndProc(ref Message m)
    {
        const int Paint = 0x000F;
        base.WndProc(ref m);

        // The native insertion mark does not render in Details view.
        if (m.Msg != Paint || _insertionIndex < 0 || _insertionIndex > Items.Count || !IsHandleCreated)
            return;

        int y = Items.Count == 0 ? Font.Height * 2
            : _insertionIndex == Items.Count ? GetItemRect(Items.Count - 1).Bottom
            : GetItemRect(_insertionIndex).Top;
        if (y < 0 || ClientSize.Height < 2)
            return;

        y = Math.Min(y, ClientSize.Height - 2);
        using var graphics = Graphics.FromHwnd(Handle);
        using var pen = new Pen(ToolingAppearance.Palette.Accent, 2);
        graphics.DrawLine(pen, 0, y, ClientSize.Width, y);
    }
}
