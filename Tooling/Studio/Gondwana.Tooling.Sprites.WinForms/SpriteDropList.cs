namespace Gondwana.Tooling.Sprites.WinForms;

internal sealed class SpriteDropList : ListBox
{
    private int _dropIndex = -1;

    internal int DropIndex
    {
        get => _dropIndex;
        set
        {
            if (_dropIndex == value)
                return;

            _dropIndex = value;
            Invalidate();
        }
    }

    internal int DropRow(Point client)
    {
        if (!ClientRectangle.Contains(client))
            return -1;

        int index = IndexFromPoint(client);
        return index >= 0 && GetItemRectangle(index).Contains(client) ? index : -1;
    }

    protected override void WndProc(ref Message m)
    {
        const int Paint = 0x000F;
        base.WndProc(ref m);
        if (m.Msg != Paint || _dropIndex < 0 || _dropIndex >= Items.Count || !IsHandleCreated)
            return;

        var row = Rectangle.Intersect(ClientRectangle, GetItemRectangle(_dropIndex));
        if (row.Width < 3 || row.Height < 3)
            return;

        using var graphics = Graphics.FromHwnd(Handle);
        using var pen = new Pen(ToolingAppearance.Palette.Accent, 2);
        graphics.DrawRectangle(pen, row.X + 1, row.Y + 1, row.Width - 2, row.Height - 2);
    }
}
