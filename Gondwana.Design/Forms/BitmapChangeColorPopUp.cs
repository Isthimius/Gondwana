using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Gondwana.Common.Drawing;
using System.IO;

namespace Gondwana.Design.Forms
{
    public partial class BitmapChangeColorPopUp : Form
    {
        #region static
        public static BitmapChangeColorPopUp _singleton = null;

        public static BitmapChangeColorPopUp Open(Bitmap bitmap, bool createMask)
        {
            if (_singleton == null)
                _singleton = new BitmapChangeColorPopUp();

            _singleton._bmp = bitmap;
            _singleton._backColor = Tilesheet.InferBackgroundColor(bitmap);
            _singleton.picBox.BackColor = _singleton._backColor;
            _singleton._isMask = createMask;

            if (!createMask)
            {
                _singleton.lblMessage.Text = "Select color to change";
                _singleton.Text = "Change Background";
            }
            else
            {
                _singleton.lblMessage.Text = "Select transparency key";
                _singleton.Text = "Create Mask";
            }

            _singleton.Show();

            return _singleton;
        }
        #endregion

        private Bitmap _bmp;
        private bool _isMask;
        private Color _backColor;

        private BitmapChangeColorPopUp()
        {
            InitializeComponent();
        }

        public Color BackColor
        {
            get
            {
                return _backColor;
            }
            set
            {
                _backColor = value;
                picBox.BackColor = _backColor;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            Bitmap newBmp;
            string filename = null;

            if (Utility.ShowInputDialog(ref filename, "Enter new file name...") == DialogResult.OK)
            {
                if (string.IsNullOrWhiteSpace(filename))
                {
                    MessageBox.Show("Please enter a new file name.", "File Name Required",
                        MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                if (!filename.EndsWith(".bmp"))
                    filename += ".bmp";

                filename = Program.State.AssetsDirectory + filename;

                if (File.Exists(filename))
                {
                    MessageBox.Show("This file already exists; please enter a new file name.", "File Already Exists",
                        MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                if (!_isMask)
                    newBmp = Gondwana.Common.Drawing.Tilesheet.RemapColor(this._bmp, this._backColor, Color.Black);
                else
                    newBmp = Gondwana.Common.Drawing.Tilesheet.CreateMask(this._bmp, this._backColor);

                newBmp.Save(filename);
            }
            else
                return;

            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void BitmapChangeColorPopUp_FormClosed(object sender, FormClosedEventArgs e)
        {
            _singleton = null;
        }
    }
}
