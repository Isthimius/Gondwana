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
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
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
            winFormBitmapRenderSurfaceControl1 = new Gondwana.WinForms.Rendering.WinFormBitmapRenderSurfaceControl();
            SuspendLayout();
            // 
            // btnBmpOpen
            // 
            btnBmpOpen.Location = new System.Drawing.Point(20, 327);
            btnBmpOpen.Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            btnBmpOpen.Name = "btnBmpOpen";
            btnBmpOpen.Size = new System.Drawing.Size(227, 77);
            btnBmpOpen.TabIndex = 1;
            btnBmpOpen.TabStop = false;
            btnBmpOpen.Text = "Open Bitmap";
            btnBmpOpen.UseVisualStyleBackColor = true;
            btnBmpOpen.Click += btnBmpOpen_Click;
            // 
            // txtCol
            // 
            txtCol.Location = new System.Drawing.Point(113, 23);
            txtCol.Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            txtCol.Name = "txtCol";
            txtCol.Size = new System.Drawing.Size(131, 31);
            txtCol.TabIndex = 2;
            txtCol.Text = "3";
            txtCol.Leave += txtCol_Leave;
            // 
            // txtRow
            // 
            txtRow.Location = new System.Drawing.Point(113, 85);
            txtRow.Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            txtRow.Name = "txtRow";
            txtRow.Size = new System.Drawing.Size(131, 31);
            txtRow.TabIndex = 3;
            txtRow.Text = "3";
            txtRow.Leave += txtRow_Leave;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(20, 23);
            label1.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(82, 25);
            label1.TabIndex = 4;
            label1.Text = "Columns";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(20, 85);
            label2.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(54, 25);
            label2.TabIndex = 5;
            label2.Text = "Rows";
            // 
            // btnShuffle
            // 
            btnShuffle.Enabled = false;
            btnShuffle.Location = new System.Drawing.Point(20, 420);
            btnShuffle.Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            btnShuffle.Name = "btnShuffle";
            btnShuffle.Size = new System.Drawing.Size(227, 77);
            btnShuffle.TabIndex = 6;
            btnShuffle.TabStop = false;
            btnShuffle.Text = "Shuffle";
            btnShuffle.UseVisualStyleBackColor = true;
            btnShuffle.Click += btnShuffle_Click;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(20, 220);
            label3.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(69, 25);
            label3.TabIndex = 10;
            label3.Text = "Correct";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(20, 158);
            label4.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(60, 25);
            label4.TabIndex = 9;
            label4.Text = "Pieces";
            // 
            // txtCorrect
            // 
            txtCorrect.Enabled = false;
            txtCorrect.Location = new System.Drawing.Point(113, 220);
            txtCorrect.Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            txtCorrect.Name = "txtCorrect";
            txtCorrect.Size = new System.Drawing.Size(131, 31);
            txtCorrect.TabIndex = 8;
            txtCorrect.TabStop = false;
            txtCorrect.Text = "0";
            // 
            // txtPieces
            // 
            txtPieces.Enabled = false;
            txtPieces.Location = new System.Drawing.Point(113, 158);
            txtPieces.Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            txtPieces.Name = "txtPieces";
            txtPieces.Size = new System.Drawing.Size(131, 31);
            txtPieces.TabIndex = 7;
            txtPieces.TabStop = false;
            txtPieces.Text = "0";
            // 
            // chkGrid
            // 
            chkGrid.AutoSize = true;
            chkGrid.Enabled = false;
            chkGrid.Location = new System.Drawing.Point(113, 270);
            chkGrid.Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            chkGrid.Name = "chkGrid";
            chkGrid.Size = new System.Drawing.Size(115, 29);
            chkGrid.TabIndex = 11;
            chkGrid.Text = "Grid Lines";
            chkGrid.UseVisualStyleBackColor = true;
            chkGrid.CheckedChanged += chkGrid_CheckedChanged;
            // 
            // lblCoord
            // 
            lblCoord.AutoSize = true;
            lblCoord.Location = new System.Drawing.Point(14, 502);
            lblCoord.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            lblCoord.Name = "lblCoord";
            lblCoord.Size = new System.Drawing.Size(105, 25);
            lblCoord.TabIndex = 12;
            lblCoord.Text = "coordinates";
            // 
            // lblInfo
            // 
            lblInfo.AutoSize = true;
            lblInfo.Location = new System.Drawing.Point(14, 697);
            lblInfo.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new System.Drawing.Size(89, 25);
            lblInfo.TabIndex = 14;
            lblInfo.Text = "misc_disp";
            // 
            // winFormBitmapRenderSurfaceControl1
            // 
            winFormBitmapRenderSurfaceControl1.Location = new System.Drawing.Point(269, -2);
            winFormBitmapRenderSurfaceControl1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            winFormBitmapRenderSurfaceControl1.Name = "winFormBitmapRenderSurfaceControl1";
            winFormBitmapRenderSurfaceControl1.Size = new System.Drawing.Size(1376, 1375);
            winFormBitmapRenderSurfaceControl1.TabIndex = 15;
            winFormBitmapRenderSurfaceControl1.MouseDown += winFormBitmapRenderSurfaceControl1_MouseDown;
            winFormBitmapRenderSurfaceControl1.MouseMove += winFormBitmapRenderSurfaceControl1_MouseMove;
            // 
            // PuzzleForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1643, 1375);
            Controls.Add(winFormBitmapRenderSurfaceControl1);
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
            Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            Name = "PuzzleForm";
            Text = "PuzzleForm";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
            FormClosing += PuzzleForm_FormClosing;
            FormClosed += PuzzleForm_FormClosed;
            Load += PuzzleForm_Load;
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion
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
        private Gondwana.WinForms.Rendering.WinFormBitmapRenderSurfaceControl winFormBitmapRenderSurfaceControl1;
    }
}