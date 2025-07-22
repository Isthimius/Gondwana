using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gondwana.Common.Drawing;
using Gondwana.Design.Forms;

namespace Gondwana.Design.Controls
{
    public partial class FramesBar : UserControl
    {
        private PictureBox _selectedPicBox = null;

        public FramesBar()
        {
            InitializeComponent();

            foreach (var tilesheet in Program.State.EngineState.Tilesheets)
                cboTilesheet.Items.Add(tilesheet.Key);
        }

        private Common.Drawing.Tilesheet _source = null;
        public Common.Drawing.Tilesheet Source
        {
            get { return _source; }
            private set
            {
                _source = value;
                DisplayFrames();
            }
        }

        private void cboTilesheet_SelectedIndexChanged(object sender, EventArgs e)
        {
            Source = Program.State.EngineState.Tilesheets[cboTilesheet.Text];
        }

        private void DisplayFrames()
        {
            toolTip.RemoveAll();
            panelFrames.Controls.Clear();

            if (_source != null)
            {
                foreach (var frame in _source.GetFrames())
                {
                    var frameBmp = frame.GetBitmap();
                    if (frameBmp == null)
                        continue;

                    var picBox = new PictureBox();
                    picBox.BorderStyle = BorderStyle.FixedSingle; 
                    picBox.Size = new Size(frame.Size.Width + 2, frame.Size.Height + 2);
                    picBox.BackColor = Color.Red;
                    picBox.SizeMode = PictureBoxSizeMode.CenterImage;
                    picBox.Image = frameBmp;
                    picBox.Tag = frame;
                    picBox.Click += picBox_Click;

                    panelFrames.Controls.Add(picBox);
                    toolTip.SetToolTip(picBox, frame.ToString());
                }
            }
        }

        void picBox_Click(object sender, EventArgs e)
        {
            if (_selectedPicBox != null)
            {
                _selectedPicBox.Padding = new Padding(0);
                _selectedPicBox.Width -= 4;
                _selectedPicBox.Height -= 4;
            }

            _selectedPicBox = (PictureBox)sender;
            Program.State.SelectedFrame = (Frame)_selectedPicBox.Tag;

            _selectedPicBox.Padding = new Padding(2);
            _selectedPicBox.Width += 4;
            _selectedPicBox.Height += 4;
        }
    }
}
