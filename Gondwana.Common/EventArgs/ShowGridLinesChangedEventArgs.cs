using Gondwana.Common.Grid;

namespace Gondwana.Common.EventArgs
{
    public delegate void ShowGridLinesChangedEventHandler(ShowGridLinesChangedEventArgs e);

    public class ShowGridLinesChangedEventArgs : System.EventArgs
    {
        public GridPointMatrix Matrix;
        public bool oldValue;
        public bool newValue;

        protected internal ShowGridLinesChangedEventArgs(GridPointMatrix matrix, bool oldVal, bool newVal)
        {
            Matrix = matrix;
            oldValue = oldVal;
            newValue = newVal;
        }
    }
}
