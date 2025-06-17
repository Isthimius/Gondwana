namespace Gondwana.Design
{
    partial class ScriptDesigner
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.TreeNode treeNode1 = new System.Windows.Forms.TreeNode("Bitmaps", 1, 1);
            System.Windows.Forms.TreeNode treeNode2 = new System.Windows.Forms.TreeNode("Audio Files", 1, 1);
            System.Windows.Forms.TreeNode treeNode3 = new System.Windows.Forms.TreeNode("Video Files", 1, 1);
            System.Windows.Forms.TreeNode treeNode4 = new System.Windows.Forms.TreeNode("Cursors", 1, 1);
            System.Windows.Forms.TreeNode treeNode5 = new System.Windows.Forms.TreeNode("Misc Files", 1, 1);
            System.Windows.Forms.TreeNode treeNode6 = new System.Windows.Forms.TreeNode("Assets", 1, 1, new System.Windows.Forms.TreeNode[] {
            treeNode1,
            treeNode2,
            treeNode3,
            treeNode4,
            treeNode5});
            System.Windows.Forms.TreeNode treeNode7 = new System.Windows.Forms.TreeNode("Resource Files", 1, 1);
            System.Windows.Forms.TreeNode treeNode8 = new System.Windows.Forms.TreeNode("Tilesheets", 1, 1);
            System.Windows.Forms.TreeNode treeNode9 = new System.Windows.Forms.TreeNode("Cycles", 1, 1);
            System.Windows.Forms.TreeNode treeNode10 = new System.Windows.Forms.TreeNode("Grids", 1, 1);
            System.Windows.Forms.TreeNode treeNode11 = new System.Windows.Forms.TreeNode("Displays", 1, 1);
            System.Windows.Forms.TreeNode treeNode12 = new System.Windows.Forms.TreeNode("Sprites", 1, 1);
            System.Windows.Forms.TreeNode treeNode13 = new System.Windows.Forms.TreeNode("Audio", 1, 1);
            System.Windows.Forms.TreeNode treeNode14 = new System.Windows.Forms.TreeNode("Video", 1, 1);
            System.Windows.Forms.TreeNode treeNode15 = new System.Windows.Forms.TreeNode("Media", 1, 1, new System.Windows.Forms.TreeNode[] {
            treeNode13,
            treeNode14});
            System.Windows.Forms.TreeNode treeNode16 = new System.Windows.Forms.TreeNode("File", new System.Windows.Forms.TreeNode[] {
            treeNode6,
            treeNode7,
            treeNode8,
            treeNode9,
            treeNode10,
            treeNode11,
            treeNode12,
            treeNode15});
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ScriptDesigner));
            this.splitLR = new System.Windows.Forms.SplitContainer();
            this.treeSelect = new System.Windows.Forms.TreeView();
            this.imgListIcons = new System.Windows.Forms.ImageList(this.components);
            this.splitUD = new System.Windows.Forms.SplitContainer();
            this.fswAssets = new System.IO.FileSystemWatcher();
            // TODO MainMenu is no longer supported. Use MenuStrip instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
            this.menu = new System.Windows.Forms.MainMenu(this.components);
            // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
            this.menuFile = new System.Windows.Forms.MenuItem();
            // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
            this.menuSave = new System.Windows.Forms.MenuItem();
            // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
            this.menuSaveAs = new System.Windows.Forms.MenuItem();
            // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
            this.menuOpen = new System.Windows.Forms.MenuItem();
            // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
            this.menuNew = new System.Windows.Forms.MenuItem();
            // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
            this.menuItem6 = new System.Windows.Forms.MenuItem();
            // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
            this.menuExit = new System.Windows.Forms.MenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitLR)).BeginInit();
            this.splitLR.Panel1.SuspendLayout();
            this.splitLR.Panel2.SuspendLayout();
            this.splitLR.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitUD)).BeginInit();
            this.splitUD.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.fswAssets)).BeginInit();
            this.SuspendLayout();
            // 
            // splitLR
            // 
            this.splitLR.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitLR.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitLR.Location = new System.Drawing.Point(0, 0);
            this.splitLR.Margin = new System.Windows.Forms.Padding(0);
            this.splitLR.Name = "splitLR";
            // 
            // splitLR.Panel1
            // 
            this.splitLR.Panel1.Controls.Add(this.treeSelect);
            // 
            // splitLR.Panel2
            // 
            this.splitLR.Panel2.Controls.Add(this.splitUD);
            this.splitLR.Size = new System.Drawing.Size(1045, 449);
            this.splitLR.SplitterDistance = 247;
            this.splitLR.TabIndex = 1;
            // 
            // treeSelect
            // 
            this.treeSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.treeSelect.ImageIndex = 1;
            this.treeSelect.ImageList = this.imgListIcons;
            this.treeSelect.Location = new System.Drawing.Point(-2, -2);
            this.treeSelect.Name = "treeSelect";
            treeNode1.ImageIndex = 1;
            treeNode1.Name = "ndBitmaps";
            treeNode1.SelectedImageIndex = 1;
            treeNode1.Text = "Bitmaps";
            treeNode2.ImageIndex = 1;
            treeNode2.Name = "ndAudioFiles";
            treeNode2.SelectedImageIndex = 1;
            treeNode2.Text = "Audio Files";
            treeNode3.ImageIndex = 1;
            treeNode3.Name = "ndVideoFiles";
            treeNode3.SelectedImageIndex = 1;
            treeNode3.Text = "Video Files";
            treeNode4.ImageIndex = 1;
            treeNode4.Name = "ndCursors";
            treeNode4.SelectedImageIndex = 1;
            treeNode4.Text = "Cursors";
            treeNode5.ImageIndex = 1;
            treeNode5.Name = "ndMiscFiles";
            treeNode5.SelectedImageIndex = 1;
            treeNode5.Text = "Misc Files";
            treeNode6.ImageIndex = 1;
            treeNode6.Name = "ndAssets";
            treeNode6.SelectedImageIndex = 1;
            treeNode6.Text = "Assets";
            treeNode7.ImageIndex = 1;
            treeNode7.Name = "ndResourceFiles";
            treeNode7.SelectedImageIndex = 1;
            treeNode7.Text = "Resource Files";
            treeNode8.ImageIndex = 1;
            treeNode8.Name = "ndTilesheets";
            treeNode8.NodeFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            treeNode8.SelectedImageIndex = 1;
            treeNode8.Text = "Tilesheets";
            treeNode9.ImageIndex = 1;
            treeNode9.Name = "ndCycles";
            treeNode9.NodeFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            treeNode9.SelectedImageIndex = 1;
            treeNode9.Text = "Cycles";
            treeNode10.ImageIndex = 1;
            treeNode10.Name = "ndGrids";
            treeNode10.NodeFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            treeNode10.SelectedImageIndex = 1;
            treeNode10.Text = "Grids";
            treeNode11.ImageIndex = 1;
            treeNode11.Name = "ndGridsDisplays";
            treeNode11.NodeFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            treeNode11.SelectedImageIndex = 1;
            treeNode11.Text = "Displays";
            treeNode12.ImageIndex = 1;
            treeNode12.Name = "ndSprites";
            treeNode12.NodeFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            treeNode12.SelectedImageIndex = 1;
            treeNode12.Text = "Sprites";
            treeNode13.ImageIndex = 1;
            treeNode13.Name = "ndAudio";
            treeNode13.SelectedImageIndex = 1;
            treeNode13.Text = "Audio";
            treeNode14.ImageIndex = 1;
            treeNode14.Name = "ndVideo";
            treeNode14.SelectedImageIndex = 1;
            treeNode14.Text = "Video";
            treeNode15.ImageIndex = 1;
            treeNode15.Name = "ndMediaFiles";
            treeNode15.SelectedImageIndex = 1;
            treeNode15.Text = "Media";
            treeNode16.ImageIndex = 5;
            treeNode16.Name = "ndFile";
            treeNode16.SelectedImageKey = "project.ico";
            treeNode16.Text = "File";
            this.treeSelect.Nodes.AddRange(new System.Windows.Forms.TreeNode[] {
            treeNode16});
            this.treeSelect.SelectedImageIndex = 0;
            this.treeSelect.Size = new System.Drawing.Size(252, 451);
            this.treeSelect.TabIndex = 0;
            this.treeSelect.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeSelect_AfterSelect);
            // 
            // imgListIcons
            // 
            this.imgListIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imgListIcons.ImageStream")));
            this.imgListIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.imgListIcons.Images.SetKeyName(0, "cycle.ico");
            this.imgListIcons.Images.SetKeyName(1, "folder.ico");
            this.imgListIcons.Images.SetKeyName(2, "grid.ico");
            this.imgListIcons.Images.SetKeyName(3, "grid2.ico");
            this.imgListIcons.Images.SetKeyName(4, "layers.ico");
            this.imgListIcons.Images.SetKeyName(5, "project.ico");
            this.imgListIcons.Images.SetKeyName(6, "project2.ico");
            this.imgListIcons.Images.SetKeyName(7, "resource.ico");
            this.imgListIcons.Images.SetKeyName(8, "resource2.ico");
            this.imgListIcons.Images.SetKeyName(9, "sprite.ico");
            this.imgListIcons.Images.SetKeyName(10, "sprite2.ico");
            this.imgListIcons.Images.SetKeyName(11, "tilesheet.ico");
            this.imgListIcons.Images.SetKeyName(12, "blue_close.png");
            this.imgListIcons.Images.SetKeyName(13, "blue_open.png");
            this.imgListIcons.Images.SetKeyName(14, "bitmap.ico");
            this.imgListIcons.Images.SetKeyName(15, "cursor.ico");
            this.imgListIcons.Images.SetKeyName(16, "media.ico");
            this.imgListIcons.Images.SetKeyName(17, "wav.ico");
            this.imgListIcons.Images.SetKeyName(18, "file.ico");
            this.imgListIcons.Images.SetKeyName(19, "appfile.ico");
            this.imgListIcons.Images.SetKeyName(20, "multimedia.ico");
            // 
            // splitUD
            // 
            this.splitUD.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitUD.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitUD.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitUD.Location = new System.Drawing.Point(0, 0);
            this.splitUD.Margin = new System.Windows.Forms.Padding(0);
            this.splitUD.Name = "splitUD";
            this.splitUD.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitUD.Panel2
            // 
            this.splitUD.Panel2.BackColor = System.Drawing.SystemColors.Control;
            this.splitUD.Size = new System.Drawing.Size(794, 446);
            this.splitUD.SplitterDistance = 143;
            this.splitUD.TabIndex = 0;
            // 
            // fswAssets
            // 
            this.fswAssets.EnableRaisingEvents = true;
            this.fswAssets.Path = "Assets/";
            this.fswAssets.SynchronizingObject = this;
            this.fswAssets.Changed += new System.IO.FileSystemEventHandler(this.fswAssets_Changed);
            this.fswAssets.Created += new System.IO.FileSystemEventHandler(this.fswAssets_Changed);
            this.fswAssets.Deleted += new System.IO.FileSystemEventHandler(this.fswAssets_Changed);
            this.fswAssets.Renamed += new System.IO.RenamedEventHandler(this.fswAssets_Renamed);
            // 
            // menu
            // 
            // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
                                                this.menu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuFile});
            // 
            // menuFile
            // 
            this.menuFile.Index = 0;
            // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
            this.menuFile.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuSave,
            this.menuSaveAs,
            this.menuOpen,
            this.menuNew,
            this.menuItem6,
            this.menuExit});
            this.menuFile.Text = "&File";
            // 
            // menuSave
            // 
            this.menuSave.Index = 0;
            this.menuSave.Text = "&Save";
            this.menuSave.Click += new System.EventHandler(this.menuSave_Click);
            // 
            // menuSaveAs
            // 
            this.menuSaveAs.Index = 1;
            this.menuSaveAs.Text = "Save &As";
            this.menuSaveAs.Click += new System.EventHandler(this.menuSaveAs_Click);
            // 
            // menuOpen
            // 
            this.menuOpen.Index = 2;
            this.menuOpen.Text = "&Open";
            this.menuOpen.Click += new System.EventHandler(this.menuOpen_Click);
            // 
            // menuNew
            // 
            this.menuNew.Index = 3;
            this.menuNew.Text = "&New";
            this.menuNew.Click += new System.EventHandler(this.menuNew_Click);
            // 
            // menuItem6
            // 
            this.menuItem6.Index = 4;
            this.menuItem6.Text = "-";
            // 
            // menuExit
            // 
            this.menuExit.Index = 5;
            this.menuExit.Text = "E&xit";
            this.menuExit.Click += new System.EventHandler(this.menuExit_Click);
            // 
            // ScriptDesigner
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1046, 451);
            this.Controls.Add(this.splitLR);
            this.Menu = this.menu;
            this.Name = "ScriptDesigner";
            this.Text = "Form1";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.splitLR.Panel1.ResumeLayout(false);
            this.splitLR.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitLR)).EndInit();
            this.splitLR.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitUD)).EndInit();
            this.splitUD.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.fswAssets)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitLR;
        private System.Windows.Forms.SplitContainer splitUD;
        private System.Windows.Forms.ImageList imgListIcons;
        private System.IO.FileSystemWatcher fswAssets;
        internal System.Windows.Forms.TreeView treeSelect;
        // TODO MainMenu is no longer supported. Use MenuStrip instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
        private System.Windows.Forms.MainMenu menu;
        // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
        private System.Windows.Forms.MenuItem menuFile;
        // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
        private System.Windows.Forms.MenuItem menuSave;
        // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
        private System.Windows.Forms.MenuItem menuSaveAs;
        // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
        private System.Windows.Forms.MenuItem menuOpen;
        // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
        private System.Windows.Forms.MenuItem menuNew;
        // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
        private System.Windows.Forms.MenuItem menuItem6;
        // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
        private System.Windows.Forms.MenuItem menuExit;

    }
}

