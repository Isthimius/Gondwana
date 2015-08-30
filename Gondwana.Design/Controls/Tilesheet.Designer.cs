namespace Gondwana.Design.Controls
{
    partial class Tilesheet
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
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.txtName = new System.Windows.Forms.TextBox();
            this.txtTileSizeX = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.txtTileSizeY = new System.Windows.Forms.TextBox();
            this.txtInitialOffsetY = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.txtInitialOffsetX = new System.Windows.Forms.TextBox();
            this.txtPixelsBetweenTilesY = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.txtPixelsBetweenTilesX = new System.Windows.Forms.TextBox();
            this.cboMask = new System.Windows.Forms.ComboBox();
            this.dgValueBag = new System.Windows.Forms.DataGridView();
            this.panel1 = new System.Windows.Forms.Panel();
            this.picSource = new System.Windows.Forms.PictureBox();
            this.cmdDelete = new System.Windows.Forms.Button();
            this.txtExtraTopSpace = new System.Windows.Forms.TextBox();
            this.lblImageSource = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgValueBag)).BeginInit();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picSource)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(20, 20);
            this.label1.Margin = new System.Windows.Forms.Padding(0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(35, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Name";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(20, 43);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(47, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Tile Size";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(20, 66);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(116, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "Overlapping Top Pixels";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(20, 89);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(62, 13);
            this.label4.TabIndex = 4;
            this.label4.Text = "Initial Offset";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(20, 112);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(104, 13);
            this.label5.TabIndex = 5;
            this.label5.Text = "Pixels Between Tiles";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(20, 135);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(33, 13);
            this.label6.TabIndex = 6;
            this.label6.Text = "Mask";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(418, 20);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(56, 13);
            this.label7.TabIndex = 7;
            this.label7.Text = "Value Bag";
            // 
            // txtName
            // 
            this.txtName.Location = new System.Drawing.Point(146, 20);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(211, 20);
            this.txtName.TabIndex = 8;
            this.txtName.Leave += new System.EventHandler(this.txtName_Leave);
            // 
            // txtTileSizeX
            // 
            this.txtTileSizeX.Location = new System.Drawing.Point(184, 43);
            this.txtTileSizeX.Name = "txtTileSizeX";
            this.txtTileSizeX.Size = new System.Drawing.Size(49, 20);
            this.txtTileSizeX.TabIndex = 9;
            this.txtTileSizeX.Leave += new System.EventHandler(this.txtTileSizeX_Leave);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(143, 43);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(35, 13);
            this.label8.TabIndex = 10;
            this.label8.Text = "Width";
            this.label8.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(264, 43);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(38, 13);
            this.label9.TabIndex = 11;
            this.label9.Text = "Height";
            this.label9.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // txtTileSizeY
            // 
            this.txtTileSizeY.Location = new System.Drawing.Point(308, 43);
            this.txtTileSizeY.Name = "txtTileSizeY";
            this.txtTileSizeY.Size = new System.Drawing.Size(49, 20);
            this.txtTileSizeY.TabIndex = 12;
            this.txtTileSizeY.Leave += new System.EventHandler(this.txtTileSizeY_Leave);
            // 
            // txtInitialOffsetY
            // 
            this.txtInitialOffsetY.Location = new System.Drawing.Point(308, 89);
            this.txtInitialOffsetY.Name = "txtInitialOffsetY";
            this.txtInitialOffsetY.Size = new System.Drawing.Size(49, 20);
            this.txtInitialOffsetY.TabIndex = 16;
            this.txtInitialOffsetY.Leave += new System.EventHandler(this.txtInitialOffsetY_Leave);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(288, 89);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(14, 13);
            this.label10.TabIndex = 15;
            this.label10.Text = "Y";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(164, 89);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(14, 13);
            this.label11.TabIndex = 14;
            this.label11.Text = "X";
            // 
            // txtInitialOffsetX
            // 
            this.txtInitialOffsetX.Location = new System.Drawing.Point(184, 89);
            this.txtInitialOffsetX.Name = "txtInitialOffsetX";
            this.txtInitialOffsetX.Size = new System.Drawing.Size(49, 20);
            this.txtInitialOffsetX.TabIndex = 13;
            this.txtInitialOffsetX.Leave += new System.EventHandler(this.txtInitialOffsetX_Leave);
            // 
            // txtPixelsBetweenTilesY
            // 
            this.txtPixelsBetweenTilesY.Location = new System.Drawing.Point(308, 112);
            this.txtPixelsBetweenTilesY.Name = "txtPixelsBetweenTilesY";
            this.txtPixelsBetweenTilesY.Size = new System.Drawing.Size(49, 20);
            this.txtPixelsBetweenTilesY.TabIndex = 20;
            this.txtPixelsBetweenTilesY.Leave += new System.EventHandler(this.txtPixelsBetweenTilesY_Leave);
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(288, 112);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(14, 13);
            this.label12.TabIndex = 19;
            this.label12.Text = "Y";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(164, 112);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(14, 13);
            this.label13.TabIndex = 18;
            this.label13.Text = "X";
            // 
            // txtPixelsBetweenTilesX
            // 
            this.txtPixelsBetweenTilesX.Location = new System.Drawing.Point(184, 112);
            this.txtPixelsBetweenTilesX.Name = "txtPixelsBetweenTilesX";
            this.txtPixelsBetweenTilesX.Size = new System.Drawing.Size(49, 20);
            this.txtPixelsBetweenTilesX.TabIndex = 17;
            this.txtPixelsBetweenTilesX.Leave += new System.EventHandler(this.txtPixelsBetweenTilesX_Leave);
            // 
            // cboMask
            // 
            this.cboMask.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.cboMask.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.cboMask.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboMask.FormattingEnabled = true;
            this.cboMask.Location = new System.Drawing.Point(146, 135);
            this.cboMask.Name = "cboMask";
            this.cboMask.Size = new System.Drawing.Size(211, 21);
            this.cboMask.TabIndex = 22;
            this.cboMask.SelectedIndexChanged += new System.EventHandler(this.cboMask_SelectedIndexChanged);
            // 
            // dgValueBag
            // 
            this.dgValueBag.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgValueBag.Location = new System.Drawing.Point(480, 20);
            this.dgValueBag.Name = "dgValueBag";
            this.dgValueBag.Size = new System.Drawing.Size(434, 136);
            this.dgValueBag.TabIndex = 23;
            this.dgValueBag.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dgValueBag_DataError);
            this.dgValueBag.Leave += new System.EventHandler(this.dgValueBag_Leave);
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.AutoScroll = true;
            this.panel1.Controls.Add(this.picSource);
            this.panel1.Location = new System.Drawing.Point(23, 186);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(891, 373);
            this.panel1.TabIndex = 24;
            // 
            // picSource
            // 
            this.picSource.InitialImage = null;
            this.picSource.Location = new System.Drawing.Point(0, 0);
            this.picSource.Name = "picSource";
            this.picSource.Size = new System.Drawing.Size(1600, 1000);
            this.picSource.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.picSource.TabIndex = 0;
            this.picSource.TabStop = false;
            // 
            // cmdDelete
            // 
            this.cmdDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdDelete.Location = new System.Drawing.Point(815, 565);
            this.cmdDelete.Name = "cmdDelete";
            this.cmdDelete.Size = new System.Drawing.Size(99, 30);
            this.cmdDelete.TabIndex = 25;
            this.cmdDelete.Text = "Delete";
            this.cmdDelete.UseVisualStyleBackColor = true;
            this.cmdDelete.Click += new System.EventHandler(this.cmdDelete_Click);
            // 
            // txtExtraTopSpace
            // 
            this.txtExtraTopSpace.Location = new System.Drawing.Point(184, 66);
            this.txtExtraTopSpace.Name = "txtExtraTopSpace";
            this.txtExtraTopSpace.Size = new System.Drawing.Size(49, 20);
            this.txtExtraTopSpace.TabIndex = 13;
            this.txtExtraTopSpace.Leave += new System.EventHandler(this.txtExtraTopSpace_Leave);
            // 
            // lblImageSource
            // 
            this.lblImageSource.AutoSize = true;
            this.lblImageSource.Location = new System.Drawing.Point(20, 170);
            this.lblImageSource.Name = "lblImageSource";
            this.lblImageSource.Size = new System.Drawing.Size(73, 13);
            this.lblImageSource.TabIndex = 29;
            this.lblImageSource.Text = "Image Source";
            // 
            // Tilesheet
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this.lblImageSource);
            this.Controls.Add(this.txtExtraTopSpace);
            this.Controls.Add(this.cmdDelete);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.dgValueBag);
            this.Controls.Add(this.cboMask);
            this.Controls.Add(this.txtPixelsBetweenTilesY);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.txtPixelsBetweenTilesX);
            this.Controls.Add(this.txtInitialOffsetY);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.txtInitialOffsetX);
            this.Controls.Add(this.txtTileSizeY);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.txtTileSizeX);
            this.Controls.Add(this.txtName);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Name = "Tilesheet";
            this.Size = new System.Drawing.Size(943, 610);
            ((System.ComponentModel.ISupportInitialize)(this.dgValueBag)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picSource)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.TextBox txtTileSizeX;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox txtTileSizeY;
        private System.Windows.Forms.TextBox txtInitialOffsetY;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox txtInitialOffsetX;
        private System.Windows.Forms.TextBox txtPixelsBetweenTilesY;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox txtPixelsBetweenTilesX;
        private System.Windows.Forms.ComboBox cboMask;
        private System.Windows.Forms.DataGridView dgValueBag;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.PictureBox picSource;
        private System.Windows.Forms.Button cmdDelete;
        private System.Windows.Forms.TextBox txtExtraTopSpace;
        private System.Windows.Forms.Label lblImageSource;
    }
}
