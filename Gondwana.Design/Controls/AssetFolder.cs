using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

namespace Gondwana.Design.Controls
{
    public partial class AssetFolder : UserControl
    {
        public AssetFolder()
        {
            InitializeComponent();
            lblPath.Text = Program.State.AssetsDirectory;
            Program.State.EngineStateFileLoaded += State_EngineStateFileLoaded;
        }

        private void State_EngineStateFileLoaded(object sender, EventArgs e)
        {
            lblPath.Text = Program.State.AssetsDirectory;
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            Process.Start(Program.State.AssetsDirectory);
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "All files (*.*)|*.*";

            var result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                var fileName = Path.GetFileName(dlg.FileName);
                var fileDest = Program.State.AssetsDirectory + fileName;

                // if file already exists ask if should overwrite
                if (File.Exists(fileDest))
                {
                    var msgResult = MessageBox.Show(
                        string.Format("A file named '{0}' already exists in the Assets folder.  Replace existing file under Assets?", fileName),
                        "Replace File?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    // if file exists but "No" was selected, exit out of method
                    if (msgResult == DialogResult.No)
                        return;
                }

                try
                {
                    File.Copy(dlg.FileName, fileDest, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error Copying File", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
