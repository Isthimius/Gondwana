using Gondwana.Grid;

namespace Gondwana.EventArgs;

public delegate void VisibleChangedEventHandler(VisibleChangedEventArgs e);

public class VisibleChangedEventArgs : System.EventArgs
{
    public GridPointMatrix Matrix;
    public bool oldVisibleValue;
    public bool newVisibleValue;

    protected internal VisibleChangedEventArgs(GridPointMatrix matrix, bool oldValue, bool newValue)
    {
        Matrix = matrix;
        oldVisibleValue = oldValue;
        newVisibleValue = newValue;
    }
}
