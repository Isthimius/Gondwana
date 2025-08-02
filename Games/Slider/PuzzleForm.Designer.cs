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
            picBox = new System.Windows.Forms.PictureBox();
            openFileBox = new System.Windows.Forms.OpenFileDialog();
            btnBmpOpen = new System.Windows.Forms.Button();
            txtCol = new System.Windows.Forms.TextBox();
            txtRow = new System.Windows.Forms.TextBox();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            btnShuffle = new System.Windows.Forms.Button();
            label3 = new System.Windows.Forms.Label();
            label4 = new System.Windows.Forms.Label();
            txtCorrect = new System.Windows.Forms.TextBox();
            txtPieces = new System.Windows.Forms.TextBox();
            chkGrid = new System.Windows.Forms.CheckBox();
            lblCoord = new System.Windows.Forms.Label();
            lblInfo = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)picBox).BeginInit();
            SuspendLayout();
            // 
            // picBox
            // 
            picBox.Location = new System.Drawing.Point(190, 0);
            picBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            picBox.Name = "picBox";
            picBox.Size = new System.Drawing.Size(961, 822);
            picBox.TabIndex = 0;
            picBox.TabStop = false;
            picBox.MouseClick += picBox_MouseClick;
            picBox.MouseMove += picBox_MouseMove;
            // 
            // btnBmpOpen
            // 
            btnBmpOpen.Location = new System.Drawing.Point(14, 196);
            btnBmpOpen.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnBmpOpen.Name = "btnBmpOpen";
            btnBmpOpen.Size = new System.Drawing.Size(159, 46);
            btnBmpOpen.TabIndex = 1;
            btnBmpOpen.TabStop = false;
            btnBmpOpen.Text = "Open Bitmap";
            btnBmpOpen.UseVisualStyleBackColor = true;
            btnBmpOpen.Click += btnBmpOpen_Click;
            // 
            // txtCol
            // 
            txtCol.Location = new System.Drawing.Point(79, 14);
            txtCol.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtCol.Name = "txtCol";
            txtCol.Size = new System.Drawing.Size(93, 23);
            txtCol.TabIndex = 2;
            txtCol.Text = "3";
            txtCol.Leave += txtCol_Leave;
            // 
            // txtRow
            // 
            txtRow.Location = new System.Drawing.Point(79, 51);
            txtRow.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtRow.Name = "txtRow";
            txtRow.Size = new System.Drawing.Size(93, 23);
            txtRow.TabIndex = 3;
            txtRow.Text = "3";
            txtRow.Leave += txtRow_Leave;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(14, 14);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(55, 15);
            label1.TabIndex = 4;
            label1.Text = "Columns";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(14, 51);
            label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(35, 15);
            label2.TabIndex = 5;
            label2.Text = "Rows";
            // 
            // btnShuffle
            // 
            btnShuffle.Enabled = false;
            btnShuffle.Location = new System.Drawing.Point(14, 252);
            btnShuffle.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnShuffle.Name = "btnShuffle";
            btnShuffle.Size = new System.Drawing.Size(159, 46);
            btnShuffle.TabIndex = 6;
            btnShuffle.TabStop = false;
            btnShuffle.Text = "Shuffle";
            btnShuffle.UseVisualStyleBackColor = true;
            btnShuffle.Click += btnShuffle_Click;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(14, 132);
            label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(46, 15);
            label3.TabIndex = 10;
            label3.Text = "Correct";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(14, 95);
            label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(40, 15);
            label4.TabIndex = 9;
            label4.Text = "Pieces";
            // 
            // txtCorrect
            // 
            txtCorrect.Enabled = false;
            txtCorrect.Location = new System.Drawing.Point(79, 132);
            txtCorrect.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtCorrect.Name = "txtCorrect";
            txtCorrect.Size = new System.Drawing.Size(93, 23);
            txtCorrect.TabIndex = 8;
            txtCorrect.TabStop = false;
            txtCorrect.Text = "0";
            // 
            // txtPieces
            // 
            txtPieces.Enabled = false;
            txtPieces.Location = new System.Drawing.Point(79, 95);
            txtPieces.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            txtPieces.Name = "txtPieces";
            txtPieces.Size = new System.Drawing.Size(93, 23);
            txtPieces.TabIndex = 7;
            txtPieces.TabStop = false;
            txtPieces.Text = "0";
            // 
            // chkGrid
            // 
            chkGrid.AutoSize = true;
            chkGrid.Enabled = false;
            chkGrid.Location = new System.Drawing.Point(79, 162);
            chkGrid.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            chkGrid.Name = "chkGrid";
            chkGrid.Size = new System.Drawing.Size(78, 19);
            chkGrid.TabIndex = 11;
            chkGrid.Text = "Grid Lines";
            chkGrid.UseVisualStyleBackColor = true;
            chkGrid.CheckedChanged += chkGrid_CheckedChanged;
            // 
            // lblCoord
            // 
            lblCoord.AutoSize = true;
            lblCoord.Location = new System.Drawing.Point(10, 301);
            lblCoord.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblCoord.Name = "lblCoord";
            lblCoord.Size = new System.Drawing.Size(69, 15);
            lblCoord.TabIndex = 12;
            lblCoord.Text = "coordinates";
            // 
            // lblInfo
            // 
            lblInfo.AutoSize = true;
            lblInfo.Location = new System.Drawing.Point(10, 418);
            lblInfo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new System.Drawing.Size(59, 15);
            lblInfo.TabIndex = 14;
            lblInfo.Text = "misc_disp";
            // 
            // PuzzleForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1150, 825);
            Controls.Add(lblInfo);
            Controls.Add(lblCoord);
            Controls.Add(chkGrid);
            Controls.Add(label3);
            Controls.Add(label4);
            Controls.Add(txtCorrect);
            Controls.Add(txtPieces);
            Controls.Add(btnShuffle);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(txtRow);
            Controls.Add(txtCol);
            Controls.Add(btnBmpOpen);
            Controls.Add(picBox);
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Name = "PuzzleForm";
            Text = "PuzzleForm";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
            FormClosing += PuzzleForm_FormClosing;
            FormClosed += PuzzleForm_FormClosed;
            Load += PuzzleForm_Load;
            ((System.ComponentModel.ISupportInitialize)picBox).EndInit();
            ResumeLayout(false);
            PerformLayout();

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
        private System.Windows.Forms.Label lblInfo;
    }
}