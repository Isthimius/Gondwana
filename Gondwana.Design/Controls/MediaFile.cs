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
    public partial class MediaFile : UserControl
    {
        private Media.MediaFile _mediaFile = null;

        private MediaFile()
        {
            InitializeComponent();
        }

        public MediaFile(Media.MediaFile mediaFile)
            : this()
        {
            _mediaFile = mediaFile;

            txtAlias.Text = _mediaFile.Alias;
            lblFileType.Text = string.Format("Type: {0} / {1}", _mediaFile.ResourceFileType.ToString(), _mediaFile.FileType.ToString());
            lblFileName.Text = string.Format("File: {0}", _mediaFile.FileName.ToString());
            lblResourceId.Text = string.Format("Resource: {0}", (_mediaFile.ResourceIdentifier != null ? _mediaFile.ResourceIdentifier.ToString() : "<n/a>"));

            txtVolAll.Text = _mediaFile.VolumeAll.ToString();
            txtVolLeft.Text = _mediaFile.VolumeLeft.ToString();
            txtVolRight.Text = _mediaFile.VolumeRight.ToString();

            chkMuteAll.Checked = _mediaFile.MuteAll;
            chkMuteLeft.Checked = _mediaFile.MuteLeft;
            chkMuteRight.Checked = _mediaFile.MuteRight;

            txtBalance.Text = _mediaFile.Balance.ToString();
            trackBalance.Value = _mediaFile.Balance;

            txtTreble.Text = _mediaFile.VolumeTreble.ToString();
            txtBass.Text = _mediaFile.VolumeBass.ToString();

            chkLoop.Checked = _mediaFile.Looping;
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            Gondwana.Engine.Configuration.Settings.MCIErrorsThrowExceptions = true;
            if (_mediaFile.ResourceFileType == Resource.EngineResourceFileTypes.Video)
                _mediaFile.Play(picVideo);
            else
                _mediaFile.Play();
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            _mediaFile.Pause();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _mediaFile.Stop();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("This will permanently delete this MediaFile definition.  Are you sure you want to do this?",
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                _mediaFile.Dispose();
                Program.State.IsDirty = true;

                var parent = (ScriptDesigner)this.ParentForm;
                parent.PopulateTreeView();
                parent.treeSelect.SelectedNode = parent.treeSelect.Nodes["ndFile"].Nodes["ndMediaFiles"];
                parent.treeSelect_AfterSelect(this, new TreeViewEventArgs(parent.treeSelect.SelectedNode));
            }
        }

        private void txtAlias_Leave(object sender, EventArgs e)
        {
            // if no change, just return
            if (txtAlias.Text == _mediaFile.Alias)
                return;

            // do not allow empty Alias names
            if (string.IsNullOrWhiteSpace(txtAlias.Text))
            {
                txtAlias.Text = _mediaFile.Alias;
                return;
            }

            var nodeName = _mediaFile.ResourceFileType == Resource.EngineResourceFileTypes.Audio ? "ndAudio" : "ndVideo";

            if (Gondwana.Media.MediaFile.GetMediaFile(txtAlias.Text) != null)
            {
                var msg = "Another MediaFile with this Alias already exists.  Changing the existing "
                        + "MediaFile Alias to this value will permanently replace the other MediaFile.  "
                        + "Is this okay?";

                if (MessageBox.Show(msg, "Confirm Change",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.OK)
                {
                    txtAlias.Text = _mediaFile.Alias;
                    return;
                }

                ((ScriptDesigner)this.ParentForm).treeSelect.Nodes["ndFile"].Nodes["ndMediaFiles"].Nodes[nodeName].Nodes[txtAlias.Text].Remove();
            }

            var tmpMediaFile = _mediaFile;
            var oldName = tmpMediaFile.Alias;

            _mediaFile = new Media.MediaFile(_mediaFile, txtAlias.Text, _mediaFile.FileName, _mediaFile.FileType);
            tmpMediaFile.Dispose();

            var node = ((ScriptDesigner)this.ParentForm).treeSelect.Nodes["ndFile"].Nodes["ndMediaFiles"].Nodes[nodeName].Nodes[oldName];
            node.Name = _mediaFile.Alias;
            node.Text = _mediaFile.Alias;

            Program.State.IsDirty = true;
        }

        private void txtVolAll_Leave(object sender, EventArgs e)
        {
            if (txtVolAll.Text == _mediaFile.VolumeAll.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtVolAll.Text) ||
                (int.TryParse(txtVolAll.Text, out val) == false) ||
                val < 0 || val > 1000)
            {
                MessageBox.Show("Value must be an integer between 0 and 1000.", "Invalid Entry", MessageBoxButtons.OK);
                txtVolAll.Text = _mediaFile.VolumeAll.ToString();
                return;
            }

            _mediaFile.VolumeAll = val;
            Program.State.IsDirty = true;
        }

        private void txtVolLeft_Leave(object sender, EventArgs e)
        {
            if (txtVolLeft.Text == _mediaFile.VolumeLeft.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtVolLeft.Text) ||
                (int.TryParse(txtVolLeft.Text, out val) == false) ||
                val < 0 || val > 1000)
            {
                MessageBox.Show("Value must be an integer between 0 and 1000.", "Invalid Entry", MessageBoxButtons.OK);
                txtVolLeft.Text = _mediaFile.VolumeLeft.ToString();
                return;
            }

            _mediaFile.VolumeLeft = val;
            Program.State.IsDirty = true;
        }

        private void txtVolRight_Leave(object sender, EventArgs e)
        {
            if (txtVolRight.Text == _mediaFile.VolumeRight.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtVolRight.Text) ||
                (int.TryParse(txtVolRight.Text, out val) == false) ||
                val < 0 || val > 1000)
            {
                MessageBox.Show("Value must be an integer between 0 and 1000.", "Invalid Entry", MessageBoxButtons.OK);
                txtVolRight.Text = _mediaFile.VolumeRight.ToString();
                return;
            }

            _mediaFile.VolumeRight = val;
            Program.State.IsDirty = true;
        }

        private void txtTreble_Leave(object sender, EventArgs e)
        {
            if (txtTreble.Text == _mediaFile.VolumeTreble.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtTreble.Text) ||
                (int.TryParse(txtTreble.Text, out val) == false) ||
                val < 0 || val > 1000)
            {
                MessageBox.Show("Value must be an integer between 0 and 1000.", "Invalid Entry", MessageBoxButtons.OK);
                txtTreble.Text = _mediaFile.VolumeTreble.ToString();
                return;
            }

            _mediaFile.VolumeTreble = val;
            Program.State.IsDirty = true;
        }

        private void txtBass_Leave(object sender, EventArgs e)
        {
            if (txtBass.Text == _mediaFile.VolumeBass.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtBass.Text) ||
                (int.TryParse(txtBass.Text, out val) == false) ||
                val < 0 || val > 1000)
            {
                MessageBox.Show("Value must be an integer between 0 and 1000.", "Invalid Entry", MessageBoxButtons.OK);
                txtBass.Text = _mediaFile.VolumeBass.ToString();
                return;
            }

            _mediaFile.VolumeBass = val;
            Program.State.IsDirty = true;
        }

        private void chkLoop_CheckStateChanged(object sender, EventArgs e)
        {
            _mediaFile.Looping = (chkLoop.CheckState == CheckState.Checked);
            Program.State.IsDirty = true;
        }

        private void txtBalance_Leave(object sender, EventArgs e)
        {
            if (txtBalance.Text == _mediaFile.Balance.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtBalance.Text) ||
                (int.TryParse(txtBalance.Text, out val) == false) ||
                val < -1000 || val > 1000)
            {
                MessageBox.Show("Value must be an integer between -1000 and 1000.", "Invalid Entry", MessageBoxButtons.OK);
                txtBalance.Text = _mediaFile.Balance.ToString();
                return;
            }

            trackBalance.Value = val;
            _mediaFile.Balance = val;
        }

        private void trackBalance_ValueChanged(object sender, EventArgs e)
        {
            txtBalance.Text = trackBalance.Value.ToString();
            _mediaFile.Balance = trackBalance.Value;
        }

        private void chkMuteAll_CheckStateChanged(object sender, EventArgs e)
        {
            _mediaFile.MuteAll = (chkMuteAll.CheckState == CheckState.Checked);
            Program.State.IsDirty = true;
        }

        private void chkMuteLeft_CheckStateChanged(object sender, EventArgs e)
        {
            _mediaFile.MuteLeft = (chkMuteLeft.CheckState == CheckState.Checked);
            Program.State.IsDirty = true;
        }

        private void chkMuteRight_CheckStateChanged(object sender, EventArgs e)
        {
            _mediaFile.MuteRight = (chkMuteRight.CheckState == CheckState.Checked);
            Program.State.IsDirty = true;
        }
    }
}
