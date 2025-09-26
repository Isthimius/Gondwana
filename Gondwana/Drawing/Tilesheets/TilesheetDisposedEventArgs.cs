namespace Gondwana.Drawing.Tilesheets;

public class TilesheetDisposedEventArgs : EventArgs
{
    public Tilesheet Tilesheet;

    protected internal TilesheetDisposedEventArgs(Tilesheet tilesheet)
    {
        Tilesheet = tilesheet;
    }
}
