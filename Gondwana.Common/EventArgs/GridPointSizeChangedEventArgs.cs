using Gondwana.Common.Grid;

namespace Gondwana.Common.EventArgs
{
    public delegate void GridPointSizeChangedEventHandler(GridPointSizeChangedEventArgs e);

    public class GridPointSizeChangedEventArgs : System.EventArgs
    {
        public GridPointMatrix layer;
        public int oldWidth;
        public int oldHeight;
        public int newWidth;
        public int newHeight;

        protected internal GridPointSizeChangedEventArgs(GridPointMatrix matrix, int oldW, int oldH, int newW, int newH)
        {
            layer = matrix;
            oldWidth = oldW;
            oldHeight = oldH;
            newWidth = newW;
            newHeight = newH;
        }
    }
}
