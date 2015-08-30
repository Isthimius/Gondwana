using Gondwana.Common.Grid;

namespace Gondwana.Common.EventArgs
{
    public delegate void GridPointMatrixWrappingChangedEventHandler(GridPointMatrixWrappingChangedEventArgs e);

    public class GridPointMatrixWrappingChangedEventArgs : System.EventArgs
    {
        public GridPointMatrix layer;
        public bool oldHorizWrapping;
        public bool newHorizWrapping;
        public bool oldVertiWrapping;
        public bool newVertiWrapping;

        protected internal GridPointMatrixWrappingChangedEventArgs(GridPointMatrix _layer, bool _oldHoriz, bool _newHoriz, bool _oldVerti, bool _newVerti)
        {
            layer = _layer;
            oldHorizWrapping = _oldHoriz;
            newHorizWrapping = _newHoriz;
            oldVertiWrapping = _oldVerti;
            newHorizWrapping = _newVerti;
        }
    }
}
