using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HWG.Spot
{
    public partial class NewGameDialog : Form
    {
        public NewGameDialog()
        {
            InitializeComponent();

            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;

            this.MinimizeBox = false;
            this.MaximizeBox = false;

            this.ShowInTaskbar = false;
        }
    }
}
