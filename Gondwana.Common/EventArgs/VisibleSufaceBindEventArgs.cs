﻿using Gondwana.Common.Grid;

namespace Gondwana.Common.EventArgs
{
    public delegate void VisibleSurfaceBindEventHandler(VisibleSufaceBindEventArgs e);

    public class VisibleSufaceBindEventArgs : System.EventArgs
    {
        public VisibleSurfaceBase Surface;
        public GridPointMatrixes OldBindValue;
        public GridPointMatrixes NewBindValue;

        public VisibleSufaceBindEventArgs(VisibleSurfaceBase surface, GridPointMatrixes oldBind, GridPointMatrixes newBind)
        {
            Surface = surface;
            OldBindValue = oldBind;
            NewBindValue = newBind;
        }
    }
}