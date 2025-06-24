using Gondwana.Grid;
using System.Drawing;

namespace Gondwana.Drawing;

internal delegate void RefreshQueueAreaAddedEventHandler(RefreshQueueAreaAddedEventArgs e);

internal class RefreshQueueAreaAddedEventArgs : EventArgs
{
    internal GridPointMatrix layer;
    internal Rectangle area;

    internal RefreshQueueAreaAddedEventArgs(GridPointMatrix _layer, Rectangle _area)
    {
        layer = _layer;
        area = _area;
    }
}
