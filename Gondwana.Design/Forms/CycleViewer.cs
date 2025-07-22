using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gondwana.Design.Forms
{
    public partial class CycleViewer : Form
    {
        private Gondwana.Common.Drawing.Animation.Cycle _cycle = null;

        private Common.Grid.GridPointMatrix _grid;
        private Common.Grid.GridPointMatrixes _grids;
        private Rendering.VisibleSurface _surface;
        private Common.Drawing.Sprites.Sprite _sprite;

        private CycleViewer()
        {
            InitializeComponent();
        }

        public CycleViewer(Gondwana.Common.Drawing.Animation.Cycle cycle)
            : this()
        {
            _cycle = cycle;
        }

        private void CycleViewer_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                this.Close();
        }

        private void CycleViewer_FormClosing(object sender, FormClosingEventArgs e)
        {
            _sprite.Dispose();
            _surface.Dispose();
            _grids.Dispose();
            _grid.Dispose();

            Gondwana.Engine.Stop();
        }

        private void CycleViewer_Load(object sender, EventArgs e)
        {
            int maxX = 0;
            int maxY = 0;

            foreach (Gondwana.Common.Drawing.Frame frame in _cycle.Sequence)
            {
                if (frame.Size.Width > maxX)
                    maxX = frame.Size.Width;

                if (frame.Size.Height > maxY)
                    maxY = frame.Size.Height;
            }

            this.Size = new Size(maxX, maxY + RectangleToScreen(this.ClientRectangle).Top - this.Top);

            _grid = new Common.Grid.GridPointMatrix(1, 1);
            _grid.CoordinateSystem = new Gondwana.Coordinates.SquareIsoCoordinates();
            _grid.SetGridPointSize(maxX, maxY);
            
            _grids = new Common.Grid.GridPointMatrixes(_grid);
            
            _surface = new Rendering.VisibleSurface(this, _grids);
            
            _sprite = Common.Drawing.Sprites.Sprites.CreateSprite(_grid, _cycle.Sequence.CurrentFrame);
            _sprite.MoveSprite(new PointF(0, 0));
            _sprite.TileAnimator.CurrentCycle = _cycle;
            _sprite.Visible = true;
            _sprite.TileAnimator.StartAnimation();

            Gondwana.Engine.Start();
        }
    }
}
