namespace Gondwana.Design.Controls
{
    partial class ResourceFile
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.TreeNode treeNode1 = new System.Windows.Forms.TreeNode("Bitmaps", 0, 0);
            System.Windows.Forms.TreeNode treeNode2 = new System.Windows.Forms.TreeNode("Wav Files", 3, 3);
            System.Windows.Forms.TreeNode treeNode3 = new System.Windows.Forms.TreeNode("Media Files", 2, 2);
            System.Windows.Forms.TreeNode treeNode4 = new System.Windows.Forms.TreeNode("Cursors", 1, 1);
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ResourceFile));
            this.splitResourceLR = new System.Windows.Forms.SplitContainer();
            this.cmdNew = new System.Windows.Forms.Button();
            this.cmdDelete = new System.Windows.Forms.Button();
            this.tvEntries = new System.Windows.Forms.TreeView();
            this.lblIsEncrypted = new System.Windows.Forms.Label();
            this.lblPassword = new System.Windows.Forms.Label();
            this.lblName = new System.Windows.Forms.Label();
            this.imgListIcons = new System.Windows.Forms.ImageList(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitResourceLR)).BeginInit();
            this.splitResourceLR.Panel1.SuspendLayout();
            this.splitResourceLR.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitResourceLR
            // 
            this.splitResourceLR.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitResourceLR.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitResourceLR.Location = new System.Drawing.Point(0, 0);
            this.splitResourceLR.Name = "splitResourceLR";
            // 
            // splitResourceLR.Panel1
            // 
            this.splitResourceLR.Panel1.Controls.Add(this.cmdNew);
            this.splitResourceLR.Panel1.Controls.Add(this.cmdDelete);
            this.splitResourceLR.Panel1.Controls.Add(this.tvEntries);
            this.splitResourceLR.Panel1.Controls.Add(this.lblIsEncrypted);
            this.splitResourceLR.Panel1.Controls.Add(this.lblPassword);
            this.splitResourceLR.Panel1.Controls.Add(this.lblName);
            this.splitResourceLR.Size = new System.Drawing.Size(918, 447);
            this.splitResourceLR.SplitterDistance = 306;
            this.splitResourceLR.SplitterWidth = 1;
            this.splitResourceLR.TabIndex = 22;
            // 
            // cmdNew
            // 
            this.cmdNew.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdNew.Location = new System.Drawing.Point(143, 419);
            this.cmdNew.Name = "cmdNew";
            this.cmdNew.Size = new System.Drawing.Size(75, 23);
            this.cmdNew.TabIndex = 27;
            this.cmdNew.Text = "New";
            this.cmdNew.UseVisualStyleBackColor = true;
            // 
            // cmdDelete
            // 
            this.cmdDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdDelete.Location = new System.Drawing.Point(224, 419);
            this.cmdDelete.Name = "cmdDelete";
            this.cmdDelete.Size = new System.Drawing.Size(75, 23);
            this.cmdDelete.TabIndex = 26;
            this.cmdDelete.Text = "Delete";
            this.cmdDelete.UseVisualStyleBackColor = true;
            // 
            // tvEntries
            // 
            this.tvEntries.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tvEntries.ImageIndex = 0;
            this.tvEntries.ImageList = this.imgListIcons;
            this.tvEntries.Location = new System.Drawing.Point(3, 82);
            this.tvEntries.Name = "tvEntries";
            treeNode1.ImageIndex = 0;
            treeNode1.Name = "bmBitmaps";
            treeNode1.SelectedImageIndex = 0;
            treeNode1.Text = "Bitmaps";
            treeNode2.ImageIndex = 3;
            treeNode2.Name = "ndWav";
            treeNode2.SelectedImageIndex = 3;
            treeNode2.Text = "Wav Files";
            treeNode3.ImageIndex = 2;
            treeNode3.Name = "ndMedia";
            treeNode3.SelectedImageIndex = 2;
            treeNode3.Text = "Media Files";
            treeNode4.ImageIndex = 1;
            treeNode4.Name = "ndCursors";
            treeNode4.SelectedImageIndex = 1;
            treeNode4.Text = "Cursors";
            this.tvEntries.Nodes.AddRange(new System.Windows.Forms.TreeNode[] {
            treeNode1,
            treeNode2,
            treeNode3,
            treeNode4});
            this.tvEntries.SelectedImageIndex = 0;
            this.tvEntries.Size = new System.Drawing.Size(296, 331);
            this.tvEntries.TabIndex = 25;
            this.tvEntries.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tvEntries_AfterSelect);
            // 
            // lblIsEncrypted
            // 
            this.lblIsEncrypted.AutoSize = true;
            this.lblIsEncrypted.Location = new System.Drawing.Point(5, 55);
            this.lblIsEncrypted.Name = "lblIsEncrypted";
            this.lblIsEncrypted.Size = new System.Drawing.Size(35, 13);
            this.lblIsEncrypted.TabIndex = 24;
            this.lblIsEncrypted.Text = "label1";
            // 
            // lblPassword
            // 
            this.lblPassword.AutoSize = true;
            this.lblPassword.Location = new System.Drawing.Point(5, 32);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(35, 13);
            this.lblPassword.TabIndex = 23;
            this.lblPassword.Text = "label1";
            // 
            // lblName
            // 
            this.lblName.AutoSize = true;
            this.lblName.Location = new System.Drawing.Point(5, 9);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(35, 13);
            this.lblName.TabIndex = 22;
            this.lblName.Text = "label1";
            // 
            // imgListIcons
            // 
            this.imgListIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imgListIcons.ImageStream")));
            this.imgListIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.imgListIcons.Images.SetKeyName(0, "bitmap.ico");
            this.imgListIcons.Images.SetKeyName(1, "cursor.ico");
            this.imgListIcons.Images.SetKeyName(2, "media.ico");
            this.imgListIcons.Images.SetKeyName(3, "wav.ico");
            // 
            // ResourceFile
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitResourceLR);
            this.Name = "ResourceFile";
            this.Size = new System.Drawing.Size(918, 447);
            this.splitResourceLR.Panel1.ResumeLayout(false);
            this.splitResourceLR.Panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitResourceLR)).EndInit();
            this.splitResourceLR.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitResourceLR;
        private System.Windows.Forms.Label lblIsEncrypted;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TreeView tvEntries;
        private System.Windows.Forms.Button cmdNew;
        private System.Windows.Forms.Button cmdDelete;
        private System.Windows.Forms.ImageList imgListIcons;




    }
}
