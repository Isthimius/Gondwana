using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gondwana.Design.Controls
{
    public partial class Cycle : UserControl
    {
        private Gondwana.Common.Drawing.Animation.Cycle _cycle = null;
        private Common.Grid.GridPointMatrix _grid;
        private Common.Grid.GridPointMatrixes _grids;
        private Rendering.VisibleSurface _surface;
        private Common.Drawing.Sprites.Sprite _sprite;

        public Cycle()
        {
            InitializeComponent();
        }

        public Cycle(Gondwana.Common.Drawing.Animation.Cycle animCycle)
            : this()
        {
            _cycle = animCycle;
            panelBottom.Visible = true;
            PopulateNextDropdown();
            txtKey.Text = _cycle.CycleKey;
            txtThrottle.Text = _cycle.ThrottleTime.ToString();
            cboNext.SelectedText = (_cycle.NextCycle == null ? "<none>" : _cycle.NextCycle.CycleKey);
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            var frmTest = new Gondwana.Design.Forms.CycleViewer(_cycle);
            frmTest.ShowDialog(this);
        }

        private void btnDispose_Click(object sender, EventArgs e)
        {
            _cycle.Dispose();
        }

        private void btnClone_Click(object sender, EventArgs e)
        {
            _cycle.Clone();
        }

        private void PopulateNextDropdown()
        {
            cboNext.Items.Clear();
            cboNext.Items.Add("<none>");

            foreach (var cycle in Program.State.EngineState.Cycles.Keys)
                cboNext.Items.Add(cycle);
        }
    }
}
