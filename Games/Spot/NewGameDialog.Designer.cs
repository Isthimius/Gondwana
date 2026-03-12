namespace HWG.Spot
{
    partial class NewGameDialog
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
            cmdStart = new System.Windows.Forms.Button();
            cmdCancel = new System.Windows.Forms.Button();
            cboPlayerCount = new System.Windows.Forms.ComboBox();
            groupBox1 = new System.Windows.Forms.GroupBox();
            cboColor1 = new System.Windows.Forms.ComboBox();
            cboPlayerType1 = new System.Windows.Forms.ComboBox();
            textBox1 = new System.Windows.Forms.TextBox();
            groupBox2 = new System.Windows.Forms.GroupBox();
            cboColor2 = new System.Windows.Forms.ComboBox();
            cboPlayerType2 = new System.Windows.Forms.ComboBox();
            textBox2 = new System.Windows.Forms.TextBox();
            groupBox3 = new System.Windows.Forms.GroupBox();
            cboColor3 = new System.Windows.Forms.ComboBox();
            cboPlayerType3 = new System.Windows.Forms.ComboBox();
            textBox3 = new System.Windows.Forms.TextBox();
            groupBox4 = new System.Windows.Forms.GroupBox();
            cboColor4 = new System.Windows.Forms.ComboBox();
            cboPlayerType4 = new System.Windows.Forms.ComboBox();
            textBox4 = new System.Windows.Forms.TextBox();
            label1 = new System.Windows.Forms.Label();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            groupBox3.SuspendLayout();
            groupBox4.SuspendLayout();
            SuspendLayout();
            // 
            // cmdStart
            // 
            cmdStart.DialogResult = System.Windows.Forms.DialogResult.OK;
            cmdStart.Location = new System.Drawing.Point(12, 293);
            cmdStart.Name = "cmdStart";
            cmdStart.Size = new System.Drawing.Size(229, 23);
            cmdStart.TabIndex = 0;
            cmdStart.Text = "Start";
            cmdStart.UseVisualStyleBackColor = true;
            // 
            // cmdCancel
            // 
            cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            cmdCancel.Location = new System.Drawing.Point(244, 293);
            cmdCancel.Name = "cmdCancel";
            cmdCancel.Size = new System.Drawing.Size(229, 23);
            cmdCancel.TabIndex = 1;
            cmdCancel.Text = "Cancel";
            cmdCancel.UseVisualStyleBackColor = true;
            cmdCancel.Click += cmdCancel_Click;
            // 
            // cboPlayerCount
            // 
            cboPlayerCount.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cboPlayerCount.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cboPlayerCount.FormattingEnabled = true;
            cboPlayerCount.Items.AddRange(new object[] { "2", "3", "4" });
            cboPlayerCount.Location = new System.Drawing.Point(66, 12);
            cboPlayerCount.Name = "cboPlayerCount";
            cboPlayerCount.Size = new System.Drawing.Size(42, 23);
            cboPlayerCount.TabIndex = 2;
            cboPlayerCount.SelectedIndexChanged += cboPlayerCount_SelectedIndexChanged;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(cboColor1);
            groupBox1.Controls.Add(cboPlayerType1);
            groupBox1.Controls.Add(textBox1);
            groupBox1.Location = new System.Drawing.Point(12, 41);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new System.Drawing.Size(461, 57);
            groupBox1.TabIndex = 3;
            groupBox1.TabStop = false;
            groupBox1.Text = "Player 1";
            // 
            // cboColor1
            // 
            cboColor1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cboColor1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cboColor1.FormattingEnabled = true;
            cboColor1.Location = new System.Drawing.Point(330, 22);
            cboColor1.Name = "cboColor1";
            cboColor1.Size = new System.Drawing.Size(121, 23);
            cboColor1.TabIndex = 2;
            // 
            // cboPlayerType1
            // 
            cboPlayerType1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cboPlayerType1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cboPlayerType1.FormattingEnabled = true;
            cboPlayerType1.Items.AddRange(new object[] { "Human", "Computer" });
            cboPlayerType1.Location = new System.Drawing.Point(203, 22);
            cboPlayerType1.Name = "cboPlayerType1";
            cboPlayerType1.Size = new System.Drawing.Size(121, 23);
            cboPlayerType1.TabIndex = 1;
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(6, 22);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(191, 23);
            textBox1.TabIndex = 0;
            textBox1.Text = "Player 1";
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(cboColor2);
            groupBox2.Controls.Add(cboPlayerType2);
            groupBox2.Controls.Add(textBox2);
            groupBox2.Location = new System.Drawing.Point(12, 104);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new System.Drawing.Size(461, 57);
            groupBox2.TabIndex = 4;
            groupBox2.TabStop = false;
            groupBox2.Text = "Player 2";
            // 
            // cboColor2
            // 
            cboColor2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cboColor2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cboColor2.FormattingEnabled = true;
            cboColor2.Location = new System.Drawing.Point(330, 22);
            cboColor2.Name = "cboColor2";
            cboColor2.Size = new System.Drawing.Size(121, 23);
            cboColor2.TabIndex = 2;
            // 
            // cboPlayerType2
            // 
            cboPlayerType2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cboPlayerType2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cboPlayerType2.FormattingEnabled = true;
            cboPlayerType2.Items.AddRange(new object[] { "Human", "Computer" });
            cboPlayerType2.Location = new System.Drawing.Point(203, 22);
            cboPlayerType2.Name = "cboPlayerType2";
            cboPlayerType2.Size = new System.Drawing.Size(121, 23);
            cboPlayerType2.TabIndex = 1;
            // 
            // textBox2
            // 
            textBox2.Location = new System.Drawing.Point(6, 22);
            textBox2.Name = "textBox2";
            textBox2.Size = new System.Drawing.Size(191, 23);
            textBox2.TabIndex = 0;
            textBox2.Text = "Player 2";
            textBox2.TextChanged += textBox2_TextChanged;
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(cboColor3);
            groupBox3.Controls.Add(cboPlayerType3);
            groupBox3.Controls.Add(textBox3);
            groupBox3.Location = new System.Drawing.Point(12, 167);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new System.Drawing.Size(461, 57);
            groupBox3.TabIndex = 4;
            groupBox3.TabStop = false;
            groupBox3.Text = "Player 3";
            // 
            // cboColor3
            // 
            cboColor3.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cboColor3.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cboColor3.FormattingEnabled = true;
            cboColor3.Location = new System.Drawing.Point(330, 22);
            cboColor3.Name = "cboColor3";
            cboColor3.Size = new System.Drawing.Size(121, 23);
            cboColor3.TabIndex = 2;
            // 
            // cboPlayerType3
            // 
            cboPlayerType3.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cboPlayerType3.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cboPlayerType3.FormattingEnabled = true;
            cboPlayerType3.Items.AddRange(new object[] { "Human", "Computer" });
            cboPlayerType3.Location = new System.Drawing.Point(203, 22);
            cboPlayerType3.Name = "cboPlayerType3";
            cboPlayerType3.Size = new System.Drawing.Size(121, 23);
            cboPlayerType3.TabIndex = 1;
            // 
            // textBox3
            // 
            textBox3.Location = new System.Drawing.Point(6, 22);
            textBox3.Name = "textBox3";
            textBox3.Size = new System.Drawing.Size(191, 23);
            textBox3.TabIndex = 0;
            textBox3.Text = "Player 3";
            textBox3.TextChanged += textBox3_TextChanged;
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(cboColor4);
            groupBox4.Controls.Add(cboPlayerType4);
            groupBox4.Controls.Add(textBox4);
            groupBox4.Location = new System.Drawing.Point(12, 230);
            groupBox4.Name = "groupBox4";
            groupBox4.Size = new System.Drawing.Size(461, 57);
            groupBox4.TabIndex = 4;
            groupBox4.TabStop = false;
            groupBox4.Text = "Player 4";
            // 
            // cboColor4
            // 
            cboColor4.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cboColor4.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cboColor4.FormattingEnabled = true;
            cboColor4.Location = new System.Drawing.Point(330, 22);
            cboColor4.Name = "cboColor4";
            cboColor4.Size = new System.Drawing.Size(121, 23);
            cboColor4.TabIndex = 2;
            // 
            // cboPlayerType4
            // 
            cboPlayerType4.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cboPlayerType4.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cboPlayerType4.FormattingEnabled = true;
            cboPlayerType4.Items.AddRange(new object[] { "Human", "Computer" });
            cboPlayerType4.Location = new System.Drawing.Point(203, 22);
            cboPlayerType4.Name = "cboPlayerType4";
            cboPlayerType4.Size = new System.Drawing.Size(121, 23);
            cboPlayerType4.TabIndex = 1;
            // 
            // textBox4
            // 
            textBox4.Location = new System.Drawing.Point(6, 22);
            textBox4.Name = "textBox4";
            textBox4.Size = new System.Drawing.Size(191, 23);
            textBox4.TabIndex = 0;
            textBox4.Text = "Player 4";
            textBox4.TextChanged += textBox4_TextChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(12, 15);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(44, 15);
            label1.TabIndex = 5;
            label1.Text = "Players";
            // 
            // NewGameDialog
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.CornflowerBlue;
            ClientSize = new System.Drawing.Size(490, 331);
            Controls.Add(label1);
            Controls.Add(groupBox4);
            Controls.Add(groupBox3);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(cboPlayerCount);
            Controls.Add(cmdCancel);
            Controls.Add(cmdStart);
            Name = "NewGameDialog";
            Text = "New Game";
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button cmdStart;
        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.ComboBox cboPlayerCount;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.ComboBox cboColor1;
        private System.Windows.Forms.ComboBox cboPlayerType1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ComboBox cboColor2;
        private System.Windows.Forms.ComboBox cboPlayerType2;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.ComboBox cboColor3;
        private System.Windows.Forms.ComboBox cboPlayerType3;
        private System.Windows.Forms.TextBox textBox3;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.ComboBox cboColor4;
        private System.Windows.Forms.ComboBox cboPlayerType4;
        private System.Windows.Forms.TextBox textBox4;
        private System.Windows.Forms.Label label1;
    }
}