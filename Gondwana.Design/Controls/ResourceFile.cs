using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Gondwana.Common;
using System.IO;
using Gondwana.Resource;

namespace Gondwana.Design.Controls
{
    public partial class ResourceFile : UserControl
    {
        private EngineResourceFile _engineResourceFile;

        public ResourceFile(EngineResourceFile engineResourceFile)
        {
            InitializeComponent();
            _engineResourceFile = engineResourceFile;
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            if (_engineResourceFile != null)
            {
                lblName.Text = Path.GetFileNameWithoutExtension(_engineResourceFile.FilePath);
                lblPassword.Text = _engineResourceFile.Password;
                lblIsEncrypted.Text = "Encrypted: " + _engineResourceFile.IsEncrypted.ToString();
            }
            else
            {
                lblName.Text = "<no file>";
                lblPassword.Text = "<N/A>";
                lblIsEncrypted.Text = "Encrypted: " + (false).ToString();
            }
        }

        private void tvEntries_AfterSelect(object sender, TreeViewEventArgs e)
        {
            splitResourceLR.Panel2.Controls.Clear();

            if (e.Node.Parent != null)
            {
                // TODO: load control in right panel
            }
        }
    }
}
