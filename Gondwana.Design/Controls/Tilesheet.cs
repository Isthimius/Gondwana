using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace Gondwana.Design.Controls
{
    public partial class Tilesheet : UserControl
    {
        private const int RGBMAX = 255;

        #region ctor
        private Tilesheet()
        {
            InitializeComponent();
        }

        public Tilesheet(Gondwana.Common.Drawing.Tilesheet sheet) : this()
        {
            this.Sheet = sheet;
        }

        public Tilesheet(string file) : this(new Gondwana.Common.Drawing.Tilesheet(file)) { }
        #endregion

        private Gondwana.Common.Drawing.Tilesheet _sheet;
        public Gondwana.Common.Drawing.Tilesheet Sheet
        {
            get { return _sheet; }
            set
            {
                _sheet = value;
                Populatefields();
            }
        }

        private void Populatefields()
        {
            txtInitialOffsetX.Text = _sheet.InitialOffsetX.ToString();
            txtInitialOffsetY.Text = _sheet.InitialOffsetY.ToString();
            txtPixelsBetweenTilesX.Text = _sheet.XPixelsBetweenTiles.ToString();
            txtPixelsBetweenTilesY.Text = _sheet.YPixelsBetweenTiles.ToString();
            txtTileSizeX.Text = _sheet.TileSize.Width.ToString();
            txtTileSizeY.Text = _sheet.TileSize.Height.ToString();
            txtName.Text = _sheet.Name.ToString();
            txtExtraTopSpace.Text = _sheet.ExtraTopSpace.ToString();
            lblImageSource.Text = GetImageSourceText();

            cboMask.Items.Clear();
            cboMask.Items.AddRange(GetTilesheetsForDropdown().ToArray());
            if (_sheet.Mask == null)
                cboMask.Text = "<none>";
            else
                cboMask.Text = _sheet.Mask.Name;

            BindValueBag();
            DrawBmpWithGridLines();
        }
        
        private string GetImageSourceText()
        {
            if (_sheet.ResourceIdentifier == null)
                return _sheet.ImageFilePath;
            else
                return _sheet.ResourceIdentifier.ToString();
        }
        
        private List<string> GetTilesheetsForDropdown()
        {
            var sheets = new List<string>();
            sheets.Add("<none>");

            foreach (var sheet in Gondwana.Common.Drawing.Tilesheet.AllTilesheets)
                sheets.Add(sheet.Name);

            return sheets;
        }

        private void DrawBmpWithGridLines()
        {
            picSource.Image = (Bitmap)_sheet.Bmp.Clone();
            
            if ((_sheet.TileSize.Width <= 0) || (_sheet.TileSize.Height <= 0))
                return;

            Color backColor = Gondwana.Common.Drawing.Tilesheet.InferBackgroundColor(_sheet.Bmp);
            Color penColor = Color.FromArgb(RGBMAX - backColor.R, RGBMAX - backColor.G, RGBMAX - backColor.B);

            int w = picSource.Image.Width;
            int h = picSource.Image.Height;
            int x = _sheet.InitialOffsetX;
            int y = _sheet.InitialOffsetY;

            using (var graphics = Graphics.FromImage(picSource.Image))
            {
                using (var pen = new Pen(penColor))
                using (var dashPen = new Pen(penColor))
                {
                    dashPen.DashStyle = DashStyle.Dot;

                    while (x <= w)
                    {
                        // draw left border
                        graphics.DrawLine(pen, x, 0, x, h);

                        // draw right border
                        x += _sheet.TileSize.Width;
                        graphics.DrawLine(pen, x, 0, x, h);

                        // shift to the right as necessary for next line
                        x += _sheet.XPixelsBetweenTiles;
                    }
                    
                    while (y <= h)
                    {
                        // draw top border
                        graphics.DrawLine(pen, 0, y, w, y);

                        // draw dotted line to mark "overlapping" pixels if necessary
                        if (_sheet.ExtraTopSpace > 0)
                            graphics.DrawLine(dashPen, 0, y + _sheet.ExtraTopSpace, w, y + _sheet.ExtraTopSpace);

                        // draw bottom border
                        y += _sheet.TileSize.Height;
                        graphics.DrawLine(pen, 0, y, w, y);


                        // shift down as necessary for next line
                        y += _sheet.YPixelsBetweenTiles;
                    }
                }
            }
        }

        private void txtName_Leave(object sender, EventArgs e)
        {
            if (txtName.Text == _sheet.Name)
                return;

            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                txtName.Text = _sheet.Name;
                return;
            }

            if (Gondwana.Common.Drawing.Tilesheet.AllTilesheets.Find(x => x.Name == txtName.Text) != null)
            {
                var msg = "Another Tilesheet with this Name already exists.  Changing the existing "
                        + "Tilesheet Name to this value will permanently replace the other Tilesheet.  "
                        + "Is this okay?";
                
                if (MessageBox.Show(msg, "Confirm Change",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.OK)
                {
                    txtName.Text = _sheet.Name;
                    return;
                }

                ((ScriptDesigner)this.ParentForm).treeSelect.Nodes["ndFile"].Nodes["ndTilesheets"].Nodes[txtName.Text].Remove();
            }

            string oldName = _sheet.Name;
            _sheet.Name = txtName.Text;

            var node = ((ScriptDesigner)this.ParentForm).treeSelect.Nodes["ndFile"].Nodes["ndTilesheets"].Nodes[oldName];
            node.Name = _sheet.Name;
            node.Text = _sheet.Name;

            Program.State.IsDirty = true;
        }

        private void BindValueBag()
        {
            dgValueBag.AutoGenerateColumns = true;
            var dt = DesignerState.GetDataTableFromValueBag(_sheet.ValueBag);
            dgValueBag.DataSource = dt;

            dt.RowChanged += dt_RowChanged;
        }

        private void dt_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            _sheet.ValueBag = DesignerState.GetValueBagFromDataTable((DataTable)dgValueBag.DataSource);
            Program.State.IsDirty = true;
        }

        private void dgValueBag_Leave(object sender, EventArgs e)
        {
            _sheet.ValueBag = DesignerState.GetValueBagFromDataTable((DataTable)dgValueBag.DataSource);
        }

        private void dgValueBag_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            MessageBox.Show(e.Exception.Message, "Duplicate Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void cboMask_SelectedIndexChanged(object sender, EventArgs e)
        {
            // if not changing, just return
            if (((_sheet.Mask == null) && (cboMask.Text == "<none>")) ||
                ((_sheet.Mask != null) && (_sheet.Mask.Name == cboMask.Text)))
                return;

            if (cboMask.Text == "<none>")
                _sheet.Mask = null;
            else
                _sheet.Mask = Gondwana.Common.Drawing.Tilesheet.GetTilesheet(cboMask.Text);

            Program.State.IsDirty = true;
        }

        private void txtTileSizeX_Leave(object sender, EventArgs e)
        {
            if (txtTileSizeX.Text == _sheet.TileSize.Width.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtTileSizeX.Text) ||
                (int.TryParse(txtTileSizeX.Text, out val) == false) ||
                val <= 0)
            {
                MessageBox.Show("Value must be a positive integer.", "Invalid Entry", MessageBoxButtons.OK);
                txtTileSizeX.Text = _sheet.TileSize.Width.ToString();
                return;
            }

            _sheet.TileSize = new Size(val, _sheet.TileSize.Height);
            DrawBmpWithGridLines();
            Program.State.IsDirty = true;
        }

        private void txtTileSizeY_Leave(object sender, EventArgs e)
        {
            if (txtTileSizeY.Text == _sheet.TileSize.Height.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtTileSizeY.Text) ||
                (int.TryParse(txtTileSizeY.Text, out val) == false) ||
                val <= 0)
            {
                MessageBox.Show("Value must be a positive integer.", "Invalid Entry", MessageBoxButtons.OK);
                txtTileSizeY.Text = _sheet.TileSize.Height.ToString();
                return;
            }

            _sheet.TileSize = new Size(_sheet.TileSize.Width, val);
            DrawBmpWithGridLines();
            Program.State.IsDirty = true;
        }

        private void txtExtraTopSpace_Leave(object sender, EventArgs e)
        {
            if (txtExtraTopSpace.Text == _sheet.ExtraTopSpace.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtExtraTopSpace.Text) ||
                (int.TryParse(txtExtraTopSpace.Text, out val) == false) ||
                val < 0)
            {
                MessageBox.Show("Value must be zero or a positive integer.", "Invalid Entry", MessageBoxButtons.OK);
                txtExtraTopSpace.Text = _sheet.ExtraTopSpace.ToString();
                return;
            }

            _sheet.ExtraTopSpace = val;
            DrawBmpWithGridLines();
            Program.State.IsDirty = true;
        }

        private void txtInitialOffsetX_Leave(object sender, EventArgs e)
        {
            if (txtInitialOffsetX.Text == _sheet.InitialOffsetX.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtInitialOffsetX.Text) ||
                (int.TryParse(txtInitialOffsetX.Text, out val) == false) ||
                val < 0)
            {
                MessageBox.Show("Value must be zero or a positive integer.", "Invalid Entry", MessageBoxButtons.OK);
                txtInitialOffsetX.Text = _sheet.InitialOffsetX.ToString();
                return;
            }

            _sheet.InitialOffsetX = val;
            DrawBmpWithGridLines();
            Program.State.IsDirty = true;
        }

        private void txtInitialOffsetY_Leave(object sender, EventArgs e)
        {
            if (txtInitialOffsetY.Text == _sheet.InitialOffsetY.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtInitialOffsetY.Text) ||
                (int.TryParse(txtInitialOffsetY.Text, out val) == false) ||
                val < 0)
            {
                MessageBox.Show("Value must be zero or a positive integer.", "Invalid Entry", MessageBoxButtons.OK);
                txtInitialOffsetY.Text = _sheet.InitialOffsetY.ToString();
                return;
            }

            _sheet.InitialOffsetY = val;
            DrawBmpWithGridLines();
            Program.State.IsDirty = true;
        }

        private void txtPixelsBetweenTilesX_Leave(object sender, EventArgs e)
        {
            if (txtPixelsBetweenTilesX.Text == _sheet.XPixelsBetweenTiles.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtPixelsBetweenTilesX.Text) ||
                (int.TryParse(txtPixelsBetweenTilesX.Text, out val) == false) ||
                val < 0)
            {
                MessageBox.Show("Value must be zero or a positive integer.", "Invalid Entry", MessageBoxButtons.OK);
                txtPixelsBetweenTilesX.Text = _sheet.XPixelsBetweenTiles.ToString();
                return;
            }

            _sheet.XPixelsBetweenTiles = val;
            DrawBmpWithGridLines();
            Program.State.IsDirty = true;
        }

        private void txtPixelsBetweenTilesY_Leave(object sender, EventArgs e)
        {
            if (txtPixelsBetweenTilesY.Text == _sheet.YPixelsBetweenTiles.ToString())
                return;

            int val;
            if (string.IsNullOrWhiteSpace(txtPixelsBetweenTilesY.Text) ||
                (int.TryParse(txtPixelsBetweenTilesY.Text, out val) == false) ||
                val < 0)
            {
                MessageBox.Show("Value must be zero or a positive integer.", "Invalid Entry", MessageBoxButtons.OK);
                txtPixelsBetweenTilesY.Text = _sheet.YPixelsBetweenTiles.ToString();
                return;
            }

            _sheet.YPixelsBetweenTiles = val;
            DrawBmpWithGridLines();
            Program.State.IsDirty = true;
        }

        private void cmdDelete_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("This will permanently delete this Tilesheet definition.  Are you sure you want to do this?",
                                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                _sheet.Dispose();
                Program.State.IsDirty = true;
                
                var parent = (ScriptDesigner)this.ParentForm;
                parent.PopulateTreeView();
                parent.treeSelect.SelectedNode = parent.treeSelect.Nodes["ndFile"].Nodes["ndTilesheets"];
                parent.treeSelect_AfterSelect(this, new TreeViewEventArgs(parent.treeSelect.SelectedNode));
            }
        }
    }
}
