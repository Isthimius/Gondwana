﻿using Gondwana.Common.Grid;

namespace Gondwana.Common.EventArgs
{
    public delegate void GridPointMatrixDisposingEventHandler(GridPointMatrixDisposingEventArgs e);

    public class GridPointMatrixDisposingEventArgs : System.EventArgs
    {
        public GridPointMatrix Matrix;

        protected internal GridPointMatrixDisposingEventArgs(GridPointMatrix matrix)
        {
            Matrix = matrix;
        }
    }
}