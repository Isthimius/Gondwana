namespace Gondwana.Grid;

public delegate void GridPointMatrixDisposingEventHandler(GridPointMatrixDisposingEventArgs e);

public class GridPointMatrixDisposingEventArgs : EventArgs
{
    public GridPointMatrix Matrix;

    protected internal GridPointMatrixDisposingEventArgs(GridPointMatrix matrix)
    {
        Matrix = matrix;
    }
}
