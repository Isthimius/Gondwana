using Gondwana.Grid;

namespace Gondwana.Common.EventArgs
{
    public delegate void GridPointMatrixAddRemoveHandler(GridPointMatrixAddRemoveEventArgs e);

    public class GridPointMatrixAddRemoveEventArgs : System.EventArgs
    {
        public GridPointMatrixes Layers;
        public GridPointMatrix Layer;

        protected internal GridPointMatrixAddRemoveEventArgs(GridPointMatrixes grids, GridPointMatrix grid)
        {
            Layers = grids;
            Layer = grid;
        }
    }
}
