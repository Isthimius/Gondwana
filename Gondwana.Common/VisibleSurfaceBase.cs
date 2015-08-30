using Gondwana.Common.EventArgs;
using Gondwana.Common.Grid;
using System;
using System.Drawing;

namespace Gondwana.Common
{
    public abstract class VisibleSurfaceBase : IDisposable
    {
        protected VisibleSurfaceBase(int width, int height)
        {
            Width = width;
            Height = height;
            VisibleSurfaces.Add(this);
        }

        ~VisibleSurfaceBase()
        {
            Dispose();
        }

        public virtual Graphics DC { get; protected internal set; }
        public virtual IBackbuffer Buffer { get; protected internal set; }

        public virtual int Height { get; protected internal set; }
        public virtual int Width { get; protected internal set; }
        public virtual bool RedrawDirtyRectangleOnly { get; protected internal set; }

        abstract public void Erase();
        abstract public void RenderBackbuffer(bool onlyDirtyRectangle);
        abstract public void Bind(GridPointMatrixes layers);

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
            VisibleSurfaces.Remove(this);
            Buffer.Dispose();
        }
    }
}