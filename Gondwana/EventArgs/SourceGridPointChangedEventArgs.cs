using Gondwana.Grid;
using System.Drawing;

namespace Gondwana.EventArgs;

public delegate void SourceGridPointChangedEventHandler(SourceGridPointChangedEventArgs e);

public class SourceGridPointChangedEventArgs : System.EventArgs
{
    public GridPointMatrix layer;
    public PointF oldPt;
    public PointF newPt;

    protected internal SourceGridPointChangedEventArgs(GridPointMatrix matrix, PointF oldP, PointF newP)
    {
        layer = matrix;
        oldPt = oldP;
        newPt = newP;
    }
}
