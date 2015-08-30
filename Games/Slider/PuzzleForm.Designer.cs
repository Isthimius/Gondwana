namespace Slider
{
    partial class PuzzleForm
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
            picBoxDC.Dispose();
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.picBox = new System.Windows.Forms.PictureBox();
            this.openFileBox = new System.Windows.Forms.OpenFileDialog();
            this.btnBmpOpen = new System.Windows.Forms.Button();
            this.txtCol = new System.Windows.Forms.TextBox();
            this.txtRow = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnShuffle = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.txtCorrect = new System.Windows.Forms.TextBox();
            this.txtPieces = new System.Windows.Forms.TextBox();
            this.chkGrid = new System.Windows.Forms.CheckBox();
            this.lblCoord = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.picBox)).BeginInit();
            this.SuspendLayout();
            // 
            // picBox
            // 
            this.picBox.Location = new System.Drawing.Point(163, 0);
            this.picBox.Name = "picBox";
            this.picBox.Size = new System.Drawing.Size(824, 712);
            this.picBox.TabIndex = 0;
            this.picBox.TabStop = false;
            this.picBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.picBox_MouseClick);
            this.picBox.MouseMove += new System.Windows.Forms.MouseEventHandler(this.picBox_MouseMove);
            // 
            // btnBmpOpen
            // 
            this.btnBmpOpen.Location = new System.Drawing.Point(12, 170);
            this.btnBmpOpen.Name = "btnBmpOpen";
            this.btnBmpOpen.Size = new System.Drawing.Size(136, 40);
            this.btnBmpOpen.TabIndex = 1;
            this.btnBmpOpen.TabStop = false;
            this.btnBmpOpen.Text = "Open Bitmap";
            this.btnBmpOpen.UseVisualStyleBackColor = true;
            this.btnBmpOpen.Click += new System.EventHandler(this.btnBmpOpen_Click);
            // 
            // txtCol
            // 
            this.txtCol.Location = new System.Drawing.Point(68, 12);
            this.txtCol.Name = "txtCol";
            this.txtCol.Size = new System.Drawing.Size(80, 20);
            this.txtCol.TabIndex = 2;
            this.txtCol.Text = "3";
            this.txtCol.Leave += new System.EventHandler(this.txtCol_Leave);
            // 
            // txtRow
            // 
            this.txtRow.Location = new System.Drawing.Point(68, 44);
            this.txtRow.Name = "txtRow";
            this.txtRow.Size = new System.Drawing.Size(80, 20);
            this.txtRow.TabIndex = 3;
            this.txtRow.Text = "3";
            this.txtRow.Leave += new System.EventHandler(this.txtRow_Leave);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(47, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "Columns";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 44);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(34, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Rows";
            // 
            // btnShuffle
            // 
            this.btnShuffle.Enabled = false;
            this.btnShuffle.Location = new System.Drawing.Point(12, 218);
            this.btnShuffle.Name = "btnShuffle";
            this.btnShuffle.Size = new System.Drawing.Size(136, 40);
            this.btnShuffle.TabIndex = 6;
            this.btnShuffle.TabStop = false;
            this.btnShuffle.Text = "Shuffle";
            this.btnShuffle.UseVisualStyleBackColor = true;
            this.btnShuffle.Click += new System.EventHandler(this.btnShuffle_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 114);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(41, 13);
            this.label3.TabIndex = 10;
            this.label3.Text = "Correct";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(12, 82);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(39, 13);
            this.label4.TabIndex = 9;
            this.label4.Text = "Pieces";
            // 
            // txtCorrect
            // 
            this.txtCorrect.Enabled = false;
            this.txtCorrect.Location = new System.Drawing.Point(68, 114);
            this.txtCorrect.Name = "txtCorrect";
            this.txtCorrect.Size = new System.Drawing.Size(80, 20);
            this.txtCorrect.TabIndex = 8;
            this.txtCorrect.TabStop = false;
            this.txtCorrect.Text = "0";
            // 
            // txtPieces
            // 
            this.txtPieces.Enabled = false;
            this.txtPieces.Location = new System.Drawing.Point(68, 82);
            this.txtPieces.Name = "txtPieces";
            this.txtPieces.Size = new System.Drawing.Size(80, 20);
            this.txtPieces.TabIndex = 7;
            this.txtPieces.TabStop = false;
            this.txtPieces.Text = "0";
            // 
            // chkGrid
            // 
            this.chkGrid.AutoSize = true;
            this.chkGrid.Enabled = false;
            this.chkGrid.Location = new System.Drawing.Point(68, 140);
            this.chkGrid.Name = "chkGrid";
            this.chkGrid.Size = new System.Drawing.Size(73, 17);
            this.chkGrid.TabIndex = 11;
            this.chkGrid.Text = "Grid Lines";
            this.chkGrid.UseVisualStyleBackColor = true;
            this.chkGrid.CheckedChanged += new System.EventHandler(this.chkGrid_CheckedChanged);
            // 
            // lblCoord
            // 
            this.lblCoord.AutoSize = true;
            this.lblCoord.Location = new System.Drawing.Point(9, 261);
            this.lblCoord.Name = "lblCoord";
            this.lblCoord.Size = new System.Drawing.Size(62, 13);
            this.lblCoord.TabIndex = 12;
            this.lblCoord.Text = "coordinates";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(15, 322);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 13;
            this.button1.Text = "button1";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // PuzzleForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(986, 715);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.lblCoord);
            this.Controls.Add(this.chkGrid);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.txtCorrect);
            this.Controls.Add(this.txtPieces);
            this.Controls.Add(this.btnShuffle);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtRow);
            this.Controls.Add(this.txtCol);
            this.Controls.Add(this.btnBmpOpen);
            this.Controls.Add(this.picBox);
            this.Name = "PuzzleForm";
            this.Text = "PuzzleForm";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PuzzleForm_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.PuzzleForm_FormClosed);
            this.Load += new System.EventHandler(this.PuzzleForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.picBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox picBox;
        private System.Windows.Forms.OpenFileDialog openFileBox;
        private System.Windows.Forms.Button btnBmpOpen;
        private System.Windows.Forms.TextBox txtCol;
        private System.Windows.Forms.TextBox txtRow;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnShuffle;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtCorrect;
        private System.Windows.Forms.TextBox txtPieces;
        private System.Windows.Forms.CheckBox chkGrid;
        private System.Windows.Forms.Label lblCoord;
        private System.Windows.Forms.Button button1;
    }
}