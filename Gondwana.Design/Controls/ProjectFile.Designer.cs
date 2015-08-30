namespace Gondwana.Design.Controls
{
    partial class ProjectFile
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
            this.chkIsBinary = new System.Windows.Forms.CheckBox();
            this.lblCurrentFile = new System.Windows.Forms.Label();
            this.cmdSave = new System.Windows.Forms.Button();
            this.cmdOpen = new System.Windows.Forms.Button();
            this.cmdNew = new System.Windows.Forms.Button();
            this.cmdSaveAs = new System.Windows.Forms.Button();
            this.dgValueBag = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgValueBag)).BeginInit();
            this.SuspendLayout();
            // 
            // chkIsBinary
            // 
            this.chkIsBinary.AutoSize = true;
            this.chkIsBinary.Location = new System.Drawing.Point(6, 44);
            this.chkIsBinary.Name = "chkIsBinary";
            this.chkIsBinary.Size = new System.Drawing.Size(66, 17);
            this.chkIsBinary.TabIndex = 3;
            this.chkIsBinary.Text = "Is Binary";
            this.chkIsBinary.UseVisualStyleBackColor = true;
            this.chkIsBinary.Click += new System.EventHandler(this.chkIsBinary_Click);
            // 
            // lblCurrentFile
            // 
            this.lblCurrentFile.AutoSize = true;
            this.lblCurrentFile.Location = new System.Drawing.Point(3, 18);
            this.lblCurrentFile.Name = "lblCurrentFile";
            this.lblCurrentFile.Size = new System.Drawing.Size(35, 13);
            this.lblCurrentFile.TabIndex = 2;
            this.lblCurrentFile.Text = "label1";
            // 
            // cmdSave
            // 
            this.cmdSave.Location = new System.Drawing.Point(449, 55);
            this.cmdSave.Name = "cmdSave";
            this.cmdSave.Size = new System.Drawing.Size(75, 23);
            this.cmdSave.TabIndex = 4;
            this.cmdSave.Text = "Save";
            this.cmdSave.UseVisualStyleBackColor = true;
            this.cmdSave.Click += new System.EventHandler(this.cmdSave_Click);
            // 
            // cmdOpen
            // 
            this.cmdOpen.Location = new System.Drawing.Point(611, 55);
            this.cmdOpen.Name = "cmdOpen";
            this.cmdOpen.Size = new System.Drawing.Size(75, 23);
            this.cmdOpen.TabIndex = 5;
            this.cmdOpen.Text = "Open";
            this.cmdOpen.UseVisualStyleBackColor = true;
            this.cmdOpen.Click += new System.EventHandler(this.cmdOpen_Click);
            // 
            // cmdNew
            // 
            this.cmdNew.Location = new System.Drawing.Point(692, 55);
            this.cmdNew.Name = "cmdNew";
            this.cmdNew.Size = new System.Drawing.Size(75, 23);
            this.cmdNew.TabIndex = 6;
            this.cmdNew.Text = "New File";
            this.cmdNew.UseVisualStyleBackColor = true;
            this.cmdNew.Click += new System.EventHandler(this.cmdNew_Click);
            // 
            // cmdSaveAs
            // 
            this.cmdSaveAs.Location = new System.Drawing.Point(530, 55);
            this.cmdSaveAs.Name = "cmdSaveAs";
            this.cmdSaveAs.Size = new System.Drawing.Size(75, 23);
            this.cmdSaveAs.TabIndex = 7;
            this.cmdSaveAs.Text = "Save As";
            this.cmdSaveAs.UseVisualStyleBackColor = true;
            this.cmdSaveAs.Click += new System.EventHandler(this.cmdSaveAs_Click);
            // 
            // dgValueBag
            // 
            this.dgValueBag.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.dgValueBag.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgValueBag.Location = new System.Drawing.Point(6, 84);
            this.dgValueBag.Name = "dgValueBag";
            this.dgValueBag.Size = new System.Drawing.Size(761, 276);
            this.dgValueBag.TabIndex = 8;
            this.dgValueBag.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dgValueBag_DataError);
            this.dgValueBag.Leave += new System.EventHandler(this.dgValueBag_Leave);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(6, 68);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(56, 13);
            this.label1.TabIndex = 9;
            this.label1.Text = "Value Bag";
            // 
            // ProjectFile
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.label1);
            this.Controls.Add(this.dgValueBag);
            this.Controls.Add(this.cmdSaveAs);
            this.Controls.Add(this.cmdNew);
            this.Controls.Add(this.cmdOpen);
            this.Controls.Add(this.cmdSave);
            this.Controls.Add(this.chkIsBinary);
            this.Controls.Add(this.lblCurrentFile);
            this.Name = "ProjectFile";
            this.Size = new System.Drawing.Size(777, 370);
            ((System.ComponentModel.ISupportInitialize)(this.dgValueBag)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox chkIsBinary;
        private System.Windows.Forms.Label lblCurrentFile;
        private System.Windows.Forms.Button cmdSave;
        private System.Windows.Forms.Button cmdOpen;
        private System.Windows.Forms.Button cmdNew;
        private System.Windows.Forms.Button cmdSaveAs;
        private System.Windows.Forms.DataGridView dgValueBag;
        private System.Windows.Forms.Label label1;
    }
}
