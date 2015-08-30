namespace Gondwana.Design.Controls
{
    partial class MediaFile
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
            this.lblAlias = new System.Windows.Forms.Label();
            this.lblFileType = new System.Windows.Forms.Label();
            this.lblFileName = new System.Windows.Forms.Label();
            this.lblResourceId = new System.Windows.Forms.Label();
            this.chkMuteAll = new System.Windows.Forms.CheckBox();
            this.chkMuteLeft = new System.Windows.Forms.CheckBox();
            this.chkMuteRight = new System.Windows.Forms.CheckBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.trackBalance = new System.Windows.Forms.TrackBar();
            this.label4 = new System.Windows.Forms.Label();
            this.txtBalance = new System.Windows.Forms.TextBox();
            this.txtVolAll = new System.Windows.Forms.TextBox();
            this.txtVolLeft = new System.Windows.Forms.TextBox();
            this.txtVolRight = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.txtBass = new System.Windows.Forms.TextBox();
            this.lblBass = new System.Windows.Forms.Label();
            this.txtTreble = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.chkLoop = new System.Windows.Forms.CheckBox();
            this.btnPlay = new System.Windows.Forms.Button();
            this.btnPause = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.picVideo = new System.Windows.Forms.PictureBox();
            this.txtAlias = new System.Windows.Forms.TextBox();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBalance)).BeginInit();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picVideo)).BeginInit();
            this.SuspendLayout();
            // 
            // lblAlias
            // 
            this.lblAlias.AutoSize = true;
            this.lblAlias.Location = new System.Drawing.Point(13, 13);
            this.lblAlias.Name = "lblAlias";
            this.lblAlias.Size = new System.Drawing.Size(32, 13);
            this.lblAlias.TabIndex = 0;
            this.lblAlias.Text = "Alias:";
            // 
            // lblFileType
            // 
            this.lblFileType.AutoSize = true;
            this.lblFileType.Location = new System.Drawing.Point(13, 35);
            this.lblFileType.Name = "lblFileType";
            this.lblFileType.Size = new System.Drawing.Size(35, 13);
            this.lblFileType.TabIndex = 1;
            this.lblFileType.Text = "label2";
            // 
            // lblFileName
            // 
            this.lblFileName.AutoSize = true;
            this.lblFileName.Location = new System.Drawing.Point(13, 57);
            this.lblFileName.Name = "lblFileName";
            this.lblFileName.Size = new System.Drawing.Size(35, 13);
            this.lblFileName.TabIndex = 2;
            this.lblFileName.Text = "label3";
            // 
            // lblResourceId
            // 
            this.lblResourceId.AutoSize = true;
            this.lblResourceId.Location = new System.Drawing.Point(13, 79);
            this.lblResourceId.Name = "lblResourceId";
            this.lblResourceId.Size = new System.Drawing.Size(35, 13);
            this.lblResourceId.TabIndex = 3;
            this.lblResourceId.Text = "label4";
            // 
            // chkMuteAll
            // 
            this.chkMuteAll.AutoSize = true;
            this.chkMuteAll.Location = new System.Drawing.Point(204, 19);
            this.chkMuteAll.Name = "chkMuteAll";
            this.chkMuteAll.Size = new System.Drawing.Size(49, 17);
            this.chkMuteAll.TabIndex = 4;
            this.chkMuteAll.Text = "mute";
            this.chkMuteAll.UseVisualStyleBackColor = true;
            this.chkMuteAll.CheckStateChanged += new System.EventHandler(this.chkMuteAll_CheckStateChanged);
            // 
            // chkMuteLeft
            // 
            this.chkMuteLeft.AutoSize = true;
            this.chkMuteLeft.Location = new System.Drawing.Point(204, 43);
            this.chkMuteLeft.Name = "chkMuteLeft";
            this.chkMuteLeft.Size = new System.Drawing.Size(49, 17);
            this.chkMuteLeft.TabIndex = 5;
            this.chkMuteLeft.Text = "mute";
            this.chkMuteLeft.UseVisualStyleBackColor = true;
            this.chkMuteLeft.CheckStateChanged += new System.EventHandler(this.chkMuteLeft_CheckStateChanged);
            // 
            // chkMuteRight
            // 
            this.chkMuteRight.AutoSize = true;
            this.chkMuteRight.Location = new System.Drawing.Point(204, 67);
            this.chkMuteRight.Name = "chkMuteRight";
            this.chkMuteRight.Size = new System.Drawing.Size(49, 17);
            this.chkMuteRight.TabIndex = 6;
            this.chkMuteRight.Text = "mute";
            this.chkMuteRight.UseVisualStyleBackColor = true;
            this.chkMuteRight.CheckStateChanged += new System.EventHandler(this.chkMuteRight_CheckStateChanged);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.trackBalance);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.txtBalance);
            this.groupBox2.Controls.Add(this.txtVolAll);
            this.groupBox2.Controls.Add(this.txtVolLeft);
            this.groupBox2.Controls.Add(this.chkMuteRight);
            this.groupBox2.Controls.Add(this.txtVolRight);
            this.groupBox2.Controls.Add(this.chkMuteAll);
            this.groupBox2.Controls.Add(this.chkMuteLeft);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Location = new System.Drawing.Point(16, 104);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(264, 174);
            this.groupBox2.TabIndex = 8;
            this.groupBox2.TabStop = false;
            // 
            // trackBalance
            // 
            this.trackBalance.LargeChange = 100;
            this.trackBalance.Location = new System.Drawing.Point(6, 119);
            this.trackBalance.Maximum = 1000;
            this.trackBalance.Minimum = -1000;
            this.trackBalance.Name = "trackBalance";
            this.trackBalance.Size = new System.Drawing.Size(247, 45);
            this.trackBalance.SmallChange = 10;
            this.trackBalance.TabIndex = 9;
            this.trackBalance.ValueChanged += new System.EventHandler(this.trackBalance_ValueChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(35, 93);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(49, 13);
            this.label4.TabIndex = 10;
            this.label4.Text = "Balance:";
            // 
            // txtBalance
            // 
            this.txtBalance.Location = new System.Drawing.Point(90, 93);
            this.txtBalance.Name = "txtBalance";
            this.txtBalance.Size = new System.Drawing.Size(100, 20);
            this.txtBalance.TabIndex = 11;
            this.txtBalance.Leave += new System.EventHandler(this.txtBalance_Leave);
            // 
            // txtVolAll
            // 
            this.txtVolAll.Location = new System.Drawing.Point(90, 19);
            this.txtVolAll.Name = "txtVolAll";
            this.txtVolAll.Size = new System.Drawing.Size(100, 20);
            this.txtVolAll.TabIndex = 8;
            this.txtVolAll.Leave += new System.EventHandler(this.txtVolAll_Leave);
            // 
            // txtVolLeft
            // 
            this.txtVolLeft.Location = new System.Drawing.Point(90, 43);
            this.txtVolLeft.Name = "txtVolLeft";
            this.txtVolLeft.Size = new System.Drawing.Size(100, 20);
            this.txtVolLeft.TabIndex = 7;
            this.txtVolLeft.Leave += new System.EventHandler(this.txtVolLeft_Leave);
            // 
            // txtVolRight
            // 
            this.txtVolRight.Location = new System.Drawing.Point(90, 67);
            this.txtVolRight.Name = "txtVolRight";
            this.txtVolRight.Size = new System.Drawing.Size(100, 20);
            this.txtVolRight.TabIndex = 3;
            this.txtVolRight.Leave += new System.EventHandler(this.txtVolRight_Leave);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(11, 67);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(73, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Volume Right:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(18, 43);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(66, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Volume Left:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(25, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(59, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Volume All:";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.txtBass);
            this.groupBox1.Controls.Add(this.lblBass);
            this.groupBox1.Controls.Add(this.txtTreble);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Location = new System.Drawing.Point(286, 104);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(166, 80);
            this.groupBox1.TabIndex = 9;
            this.groupBox1.TabStop = false;
            // 
            // txtBass
            // 
            this.txtBass.Location = new System.Drawing.Point(52, 43);
            this.txtBass.Name = "txtBass";
            this.txtBass.Size = new System.Drawing.Size(100, 20);
            this.txtBass.TabIndex = 15;
            this.txtBass.Leave += new System.EventHandler(this.txtBass_Leave);
            // 
            // lblBass
            // 
            this.lblBass.AutoSize = true;
            this.lblBass.Location = new System.Drawing.Point(13, 43);
            this.lblBass.Name = "lblBass";
            this.lblBass.Size = new System.Drawing.Size(33, 13);
            this.lblBass.TabIndex = 14;
            this.lblBass.Text = "Bass:";
            // 
            // txtTreble
            // 
            this.txtTreble.Location = new System.Drawing.Point(52, 17);
            this.txtTreble.Name = "txtTreble";
            this.txtTreble.Size = new System.Drawing.Size(100, 20);
            this.txtTreble.TabIndex = 13;
            this.txtTreble.Leave += new System.EventHandler(this.txtTreble_Leave);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 19);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(40, 13);
            this.label5.TabIndex = 12;
            this.label5.Text = "Treble:";
            // 
            // chkLoop
            // 
            this.chkLoop.AutoSize = true;
            this.chkLoop.Location = new System.Drawing.Point(286, 190);
            this.chkLoop.Name = "chkLoop";
            this.chkLoop.Size = new System.Drawing.Size(97, 17);
            this.chkLoop.TabIndex = 10;
            this.chkLoop.Text = "Loop Playback";
            this.chkLoop.UseVisualStyleBackColor = true;
            this.chkLoop.CheckStateChanged += new System.EventHandler(this.chkLoop_CheckStateChanged);
            // 
            // btnPlay
            // 
            this.btnPlay.Location = new System.Drawing.Point(286, 226);
            this.btnPlay.Name = "btnPlay";
            this.btnPlay.Size = new System.Drawing.Size(75, 23);
            this.btnPlay.TabIndex = 11;
            this.btnPlay.Text = "Play";
            this.btnPlay.UseVisualStyleBackColor = true;
            this.btnPlay.Click += new System.EventHandler(this.btnPlay_Click);
            // 
            // btnPause
            // 
            this.btnPause.Location = new System.Drawing.Point(377, 226);
            this.btnPause.Name = "btnPause";
            this.btnPause.Size = new System.Drawing.Size(75, 23);
            this.btnPause.TabIndex = 12;
            this.btnPause.Text = "Pause";
            this.btnPause.UseVisualStyleBackColor = true;
            this.btnPause.Click += new System.EventHandler(this.btnPause_Click);
            // 
            // btnStop
            // 
            this.btnStop.Location = new System.Drawing.Point(286, 255);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(75, 23);
            this.btnStop.TabIndex = 13;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.Location = new System.Drawing.Point(377, 255);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(75, 23);
            this.btnDelete.TabIndex = 14;
            this.btnDelete.Text = "Delete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // picVideo
            // 
            this.picVideo.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.picVideo.Location = new System.Drawing.Point(16, 284);
            this.picVideo.Name = "picVideo";
            this.picVideo.Size = new System.Drawing.Size(437, 188);
            this.picVideo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.picVideo.TabIndex = 15;
            this.picVideo.TabStop = false;
            // 
            // txtAlias
            // 
            this.txtAlias.Location = new System.Drawing.Point(51, 10);
            this.txtAlias.Name = "txtAlias";
            this.txtAlias.Size = new System.Drawing.Size(218, 20);
            this.txtAlias.TabIndex = 16;
            this.txtAlias.Leave += new System.EventHandler(this.txtAlias_Leave);
            // 
            // MediaFile
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.txtAlias);
            this.Controls.Add(this.picVideo);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnPause);
            this.Controls.Add(this.btnPlay);
            this.Controls.Add(this.chkLoop);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.lblResourceId);
            this.Controls.Add(this.lblFileName);
            this.Controls.Add(this.lblFileType);
            this.Controls.Add(this.lblAlias);
            this.Name = "MediaFile";
            this.Size = new System.Drawing.Size(466, 485);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBalance)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picVideo)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblAlias;
        private System.Windows.Forms.Label lblFileType;
        private System.Windows.Forms.Label lblFileName;
        private System.Windows.Forms.Label lblResourceId;
        private System.Windows.Forms.CheckBox chkMuteAll;
        private System.Windows.Forms.CheckBox chkMuteLeft;
        private System.Windows.Forms.CheckBox chkMuteRight;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox txtVolAll;
        private System.Windows.Forms.TextBox txtVolLeft;
        private System.Windows.Forms.TextBox txtVolRight;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtBalance;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TrackBar trackBalance;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox txtBass;
        private System.Windows.Forms.Label lblBass;
        private System.Windows.Forms.TextBox txtTreble;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox chkLoop;
        private System.Windows.Forms.Button btnPlay;
        private System.Windows.Forms.Button btnPause;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.PictureBox picVideo;
        private System.Windows.Forms.TextBox txtAlias;
    }
}
