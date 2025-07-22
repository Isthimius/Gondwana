using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Gondwana.Resource;

namespace Gondwana.Design.Controls
{
    public partial class TilesheetMenu : UserControl
    {
        public TilesheetMenu()
        {
            InitializeComponent();
            cboType.SelectedIndex = 0;
        }

        private void btnNewCancel_Click(object sender, EventArgs e)
        {
            // New
            if (btnNewCancel.Text == "New")
            {
                btnNewCancel.Text = "Cancel";
                btnSave.Visible = true;
                
                txtName.Enabled = true;
                
                cboType.Enabled = true;
                cboName.Enabled = true;

                panel1.Visible = true;

                SetNameDropDownList();
            }
            // Cancel
            else
            {
                btnNewCancel.Text = "New";
                btnSave.Visible = false;

                txtName.Text = string.Empty;
                txtName.Enabled = false;
                
                cboType.SelectedIndex = 0;
                cboType.Enabled = false;

                panel1.Visible = false;
                
                cboName.Text = string.Empty;
                cboName.Enabled = false;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("'Name' is a required field.", "Missing Name", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (Gondwana.Common.Drawing.Tilesheet.AllTilesheets.Any(x => x.Name == txtName.Text))
            {
                MessageBox.Show(
                    string.Format("Tilesheet '{0}' already exists.  Please select another name, or delete the existing Tilesheet.", txtName.Text),
                    "Name Already Exists", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (string.IsNullOrWhiteSpace(cboName.Text))
            {
                MessageBox.Show("'Source Name' is a required field.", "Missing Source Name", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            Gondwana.Common.Drawing.Tilesheet tilesheet = null;

            // if cboType is "Assets Folder"...
            if (cboType.SelectedIndex == 0)
                tilesheet = new Gondwana.Common.Drawing.Tilesheet(txtName.Text, Program.State.AssetsDirectory + cboName.Text);
            else
                tilesheet = new Gondwana.Common.Drawing.Tilesheet(GetResourceIdentifierFromListSelection(), txtName.Text);

            Program.State.IsDirty = true;

            var parent = (ScriptDesigner)this.ParentForm;
            parent.PopulateTreeView();
            parent.treeSelect.SelectedNode = parent.treeSelect.Nodes["ndFile"].Nodes["ndTilesheets"].Nodes[txtName.Text];
        }

        private void SetNameDropDownList()
        {
            panel1.Controls.Clear();
            cboName.Items.Clear();

            // if cboType is "Assets Folder"...
            if (cboType.SelectedIndex == 0)
            {
                foreach (var file in Directory.GetFiles(Program.State.AssetsDirectory))
                {
                    var fileName = Path.GetFileName(file);

                    if (DesignerState.GetAssetType(fileName) == EngineResourceFileTypes.Bitmap)
                        cboName.Items.Add(fileName);
                }
            }
            else
            {
                foreach (var resFile in Program.State.EngineState.ResourceFiles)
                {
                    foreach (var item in resFile.GetAllNames(EngineResourceFileTypes.Bitmap))
                    {
                        cboName.Items.Add(string.Format("{0} -> {1}", Path.GetFileName(resFile.FilePath), item));
                    }
                }
            }
        }

        private void cboType_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetNameDropDownList();
        }

        private EngineResourceFileIdentifier GetResourceIdentifierFromListSelection()
        {
            var resString = cboName.Text.Split(new string[] { " -> " }, StringSplitOptions.None);
            var resFile = Program.State.EngineState.ResourceFiles.First(x => Path.GetFileName(x.FilePath) == resString[0]);
            return new EngineResourceFileIdentifier(resFile, EngineResourceFileTypes.Bitmap, resString[1]);
        }

        private void cboName_SelectedIndexChanged(object sender, EventArgs e)
        {
            AssetViewer viewer = null;

            if (cboType.SelectedIndex == 0)
                viewer = new AssetViewer(Program.State.AssetsDirectory + cboName.Text, EngineResourceFileTypes.Bitmap);
            else
                viewer = new AssetViewer(GetResourceIdentifierFromListSelection().Data, EngineResourceFileTypes.Bitmap, cboName.Text);

            viewer.Dock = DockStyle.Fill;
            panel1.Controls.Add(viewer);
        }
    }
}
