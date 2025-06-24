namespace Gondwana.Grid;

public delegate void GridPointMatrixesDisposingEventHandler(GridPointMatrixesDisposingEventArgs e);

public class GridPointMatrixesDisposingEventArgs : EventArgs
{
    public GridPointMatrixes Matrixes;

    protected internal GridPointMatrixesDisposingEventArgs(GridPointMatrixes matrixLayers)
    {
        Matrixes = matrixLayers;
    }
}
