using Gondwana.Drawing;

namespace Gondwana.EventArgs;

public delegate void TilesheetDisposedHandler(TilesheetDisposedEventArgs e);

public class TilesheetDisposedEventArgs : System.EventArgs
{
    public Tilesheet Tilesheet;

    protected internal TilesheetDisposedEventArgs(Tilesheet tilesheet)
    {
        Tilesheet = tilesheet;
    }
}
