namespace Gondwana.Design.Controls
{
    partial class FramesBar
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
            this.label1 = new System.Windows.Forms.Label();
            this.cboTilesheet = new System.Windows.Forms.ComboBox();
            this.panelFrames = new System.Windows.Forms.FlowLayoutPanel();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(5, 7);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Tilesheet:";
            // 
            // cboTilesheet
            // 
            this.cboTilesheet.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboTilesheet.FormattingEnabled = true;
            this.cboTilesheet.Location = new System.Drawing.Point(64, 4);
            this.cboTilesheet.Name = "cboTilesheet";
            this.cboTilesheet.Size = new System.Drawing.Size(195, 21);
            this.cboTilesheet.TabIndex = 1;
            this.cboTilesheet.SelectedIndexChanged += new System.EventHandler(this.cboTilesheet_SelectedIndexChanged);
            // 
            // panelFrames
            // 
            this.panelFrames.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelFrames.AutoScroll = true;
            this.panelFrames.Location = new System.Drawing.Point(0, 31);
            this.panelFrames.Name = "panelFrames";
            this.panelFrames.Size = new System.Drawing.Size(415, 213);
            this.panelFrames.TabIndex = 2;
            // 
            // FramesBar
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelFrames);
            this.Controls.Add(this.cboTilesheet);
            this.Controls.Add(this.label1);
            this.DoubleBuffered = true;
            this.Name = "FramesBar";
            this.Size = new System.Drawing.Size(415, 244);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cboTilesheet;
        private System.Windows.Forms.FlowLayoutPanel panelFrames;
        private System.Windows.Forms.ToolTip toolTip;
    }
}
