using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gondwana.Design.Controls
{
    public partial class ProjectFile : UserControl
    {
        public ProjectFile()
        {
            InitializeComponent();
            UpdateFields();
            BindValueBag();

            Program.State.EngineStateFileLoaded += State_EngineStateFileChanged;
            Program.State.EngineStateFileSaved += State_EngineStateFileChanged;
            Program.State.EngineStateOnDirty += State_EngineStateOnDirty;
        }

        void State_EngineStateFileChanged(object sender, EventArgs e)
        {
            UpdateFields();
            BindValueBag();
        }

        void State_EngineStateOnDirty(object sender, EventArgs e)
        {
            cmdSave.Enabled = true;
        }

        private void cmdSave_Click(object sender, EventArgs e)
        {
            Program.State.Save(Program.State.IsBinary);
        }

        private void cmdSaveAs_Click(object sender, EventArgs e)
        {
            Program.State.Save(null);
        }

        private void cmdOpen_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = DesignerState.GetDialogFilter(null);

            var result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                bool isBinary = DesignerState.IsBinaryExtension(dlg.FileName);
                Program.State.LoadEngineState(dlg.FileName, isBinary);
            }
        }

        private void cmdNew_Click(object sender, EventArgs e)
        {
            Program.State.LoadEngineState();
        }

        private void UpdateFields()
        {
            lblCurrentFile.Text = Program.State.CurrentFile;
            chkIsBinary.Checked = Program.State.IsBinary;
            chkIsBinary.Enabled = (Program.State.CurrentFile == Program.State.NoFile);
            cmdSaveAs.Enabled = !chkIsBinary.Enabled;
            cmdSave.Enabled = Program.State.IsDirty;
        }

        private void chkIsBinary_Click(object sender, EventArgs e)
        {
            Program.State.IsBinary = chkIsBinary.Checked;
            Program.State.IsDirty = true;
        }

        private void BindValueBag()
        {
            dgValueBag.AutoGenerateColumns = true;
            var dt = DesignerState.GetDataTableFromValueBag(Program.State.EngineState.ValueBag);
            dgValueBag.DataSource = dt;

            dt.RowChanged += dt_RowChanged;
        }

        private void dt_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            Program.State.EngineState.ValueBag = DesignerState.GetValueBagFromDataTable((DataTable)dgValueBag.DataSource);
            Program.State.IsDirty = true;
        }

        private void dgValueBag_Leave(object sender, EventArgs e)
        {
            Program.State.EngineState.ValueBag = DesignerState.GetValueBagFromDataTable((DataTable)dgValueBag.DataSource);
        }

        private void dgValueBag_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            MessageBox.Show(e.Exception.Message, "Duplicate Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
