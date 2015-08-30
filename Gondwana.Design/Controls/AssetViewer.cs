using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Gondwana.Resource;
using System.IO;
using Gondwana.Design.Forms;
using System.Diagnostics;

namespace Gondwana.Design.Controls
{
    public partial class AssetViewer : UserControl
    {
        private string _file = null;
        private Stream _stream;
        private EngineResourceFileTypes _type;

        private AssetViewer()
        {
            InitializeComponent();
        }

        public AssetViewer(string file, EngineResourceFileTypes type)
            : this()
        {
            _file = file;

            using (var stream = new FileStream(file, FileMode.Open))
            {
                _stream = new MemoryStream();
                stream.Position = 0;
                stream.CopyTo(_stream);
                _type = type;
            }

            try
            {
                DisplayAsset();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Viewing File", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public AssetViewer(Stream stream, EngineResourceFileTypes type, string nameTempFileAs = null)
            : this()
        {
            if (string.IsNullOrWhiteSpace(nameTempFileAs))
                nameTempFileAs = Guid.NewGuid().ToString();

            var file = string.Format("{0}\\{1}", Program.State.TempDirectory, nameTempFileAs);
            using (var fileStream = File.Create(file))
            {
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(fileStream);
            }

            _file = file;
            _stream = stream;
            _type = type;

            try
            {
                DisplayAsset();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Viewing File", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisplayAsset()
        {
            PictureBox picBox = null;

            switch (_type)
            {
                case EngineResourceFileTypes.Bitmap:
                    btnBackground.Visible = true;
                    btnMask.Visible = true;
                    axWMP.Visible = false;

                    picBox = new PictureBox();
                    picBox.SizeMode = PictureBoxSizeMode.AutoSize;
                    picBox.Image = new Bitmap(_stream);
                    picBox.MouseClick += picBox_MouseClick;

                    this.panel1.Controls.Add(picBox);
                    break;

                case EngineResourceFileTypes.Audio:
                case EngineResourceFileTypes.Video:
                    btnBackground.Visible = false;
                    btnMask.Visible = false;
                    panel1.Dock = DockStyle.Fill;

                    if (!string.IsNullOrWhiteSpace(_file))
                    {
                        axWMP.settings.autoStart = false;
                        axWMP.Visible = true;
                        axWMP.URL = _file;
                        axWMP.Dock = DockStyle.Fill;
                    }

                    break;

                case EngineResourceFileTypes.Cursor:
                    btnBackground.Visible = false;
                    btnMask.Visible = false;
                    axWMP.Visible = false;

                    picBox = new PictureBox();
                    picBox.SizeMode = PictureBoxSizeMode.AutoSize;
                    picBox.BorderStyle = BorderStyle.Fixed3D;

                    var cursor = new Cursor(_file);
                    var bmp = new Bitmap(cursor.Size.Width, cursor.Size.Height);
                    using (var graphics = Graphics.FromImage(bmp))
                        cursor.Draw(graphics, new Rectangle(new Point(), cursor.Size));

                    picBox.Image = bmp;
                    picBox.MouseClick += picBox_MouseClick;

                    this.panel1.Controls.Add(picBox);

                    break;

                case EngineResourceFileTypes.Misc:
                    btnBackground.Text = "Open File";
                    btnBackground.Visible = true;
                    btnMask.Visible = false;
                    axWMP.Visible = false;

                    break;

                default:
                    btnBackground.Visible = false;
                    btnMask.Visible = false;
                    axWMP.Visible = false;

                    break;
            }
        }

        private void picBox_MouseClick(object sender, MouseEventArgs e)
        {
            var popUp = BitmapChangeColorPopUp._singleton;
            if (popUp != null)
            {
                var picBox = (PictureBox)sender;
                var bmp = (Bitmap)picBox.Image;
                var color = bmp.GetPixel(e.X, e.Y);

                popUp.BackColor = color;
            }
        }

        private void btnBackground_Click(object sender, EventArgs e)
        {
            if (btnBackground.Text == "Open File")
            {
                try
                {
                    Process.Start(_file);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error Opening File", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
                BitmapChangeColorPopUp.Open(new Bitmap(_stream), false);
        }

        private void btnMask_Click(object sender, EventArgs e)
        {
            BitmapChangeColorPopUp.Open(new Bitmap(_stream), true);
        }

        private void AssetViewer_SizeChanged(object sender, EventArgs e)
        {
            panel1.MaximumSize = new Size(this.Width - 6, this.Height - 35);
        }

        private void axWMP_Leave(object sender, EventArgs e)
        {
            axWMP.close();
            axWMP.URL = null;
        }

        private void AssetViewer_Leave(object sender, EventArgs e)
        {
            axWMP.close();
            axWMP.URL = null;
        }
    }
}
