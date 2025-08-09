using System.Drawing;

namespace Gondwana.Grid;

public delegate void SourceGridPointChangedEventHandler(SourceGridPointChangedEventArgs e);

public class SourceGridPointChangedEventArgs : EventArgs
{
    public SceneLayer layer;
    public PointF oldPt;
    public PointF newPt;

    protected internal SourceGridPointChangedEventArgs(SceneLayer matrix, PointF oldP, PointF newP)
    {
        layer = matrix;
        oldPt = oldP;
        newPt = newP;
    }
}
