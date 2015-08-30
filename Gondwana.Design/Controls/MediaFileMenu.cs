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
    public partial class MediaFileMenu : UserControl
    {
        public MediaFileMenu()
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
                MessageBox.Show("'Alias' is a required field.", "Missing Alias", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (Gondwana.Media.MediaFile._mediaFiles.ContainsKey(txtName.Text))
            {
                MessageBox.Show(
                    string.Format("MediaFile alias '{0}' already exists.  Please select another alias, or delete the existing MediaFile.", txtName.Text),
                    "Alias Already Exists", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (string.IsNullOrWhiteSpace(cboName.Text))
            {
                MessageBox.Show("'Source Name' is a required field.", "Missing Source Name", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            Gondwana.Media.MediaFile media = null;

            // if cboType is "Assets Folder"...
            if (cboType.SelectedIndex == 0)
                media = new Media.MediaFile(txtName.Text,Program.State.AssetsDirectory + cboName.Text);
            else
            {
                var resId = GetResourceIdentifierFromListSelection();
                media = new Media.MediaFile(txtName.Text, resId, Media.MediaFile.InferMediaFileType(resId.ResourceName));
            }

            Program.State.IsDirty = true;

            var parent = (ScriptDesigner)this.ParentForm;
            parent.PopulateTreeView();

            if (media.ResourceFileType == EngineResourceFileTypes.Audio)
                parent.treeSelect.SelectedNode = parent.treeSelect.Nodes["ndFile"].Nodes["ndMediaFiles"].Nodes["ndAudio"].Nodes[txtName.Text];
            else
                parent.treeSelect.SelectedNode = parent.treeSelect.Nodes["ndFile"].Nodes["ndMediaFiles"].Nodes["ndVideo"].Nodes[txtName.Text];
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

                    if ((DesignerState.GetAssetType(fileName) == EngineResourceFileTypes.Audio) ||
                        (DesignerState.GetAssetType(fileName) == EngineResourceFileTypes.Video))
                        cboName.Items.Add(fileName);
                }
            }
            else
            {
                foreach (var resFile in Program.State.EngineState.ResourceFiles)
                {
                    foreach (var item in resFile.GetAllNames(EngineResourceFileTypes.Audio))
                        cboName.Items.Add(string.Format("{0} -> Audio -> {1}", Path.GetFileName(resFile.FilePath), item));

                    foreach (var item in resFile.GetAllNames(EngineResourceFileTypes.Video))
                        cboName.Items.Add(string.Format("{0} -> Video -> {1}", Path.GetFileName(resFile.FilePath), item));
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
            var resType = resString[1] == "Audio" ? EngineResourceFileTypes.Audio : EngineResourceFileTypes.Video;
            return new EngineResourceFileIdentifier(resFile, resType, resString[2]);
        }

        private void cboName_SelectedIndexChanged(object sender, EventArgs e)
        {
            AssetViewer viewer = null;


            if (cboType.SelectedIndex == 0)
            {
                // audio and video perform the same in AssetViewer
                viewer = new AssetViewer(Program.State.AssetsDirectory + cboName.Text, EngineResourceFileTypes.Audio);
            }
            else
            {
                var resId = GetResourceIdentifierFromListSelection();
                viewer = new AssetViewer(resId.Data, resId.ResourceType, cboName.Text);
            }

            viewer.Dock = DockStyle.Fill;
            panel1.Controls.Add(viewer);
        }
    }
}
