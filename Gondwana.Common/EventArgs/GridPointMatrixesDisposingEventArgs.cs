using Gondwana.Common.Grid;

namespace Gondwana.Common.EventArgs
{
    public delegate void GridPointMatrixesDisposingEventHandler(GridPointMatrixesDisposingEventArgs e);

    public class GridPointMatrixesDisposingEventArgs : System.EventArgs
    {
        public GridPointMatrixes Matrixes;

        protected internal GridPointMatrixesDisposingEventArgs(GridPointMatrixes matrixLayers)
        {
            Matrixes = matrixLayers;
        }
    }
}
