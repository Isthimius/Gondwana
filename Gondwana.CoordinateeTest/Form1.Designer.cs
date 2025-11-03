namespace Gondwana.CoordinateeTest
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            renderSurface = new Gondwana.WinForms.Rendering.WinFormBitmapRenderSurfaceControl();
            listBox1 = new ListBox();
            SuspendLayout();
            // 
            // renderSurface
            // 
            renderSurface.Location = new Point(4, 3);
            renderSurface.Name = "renderSurface";
            renderSurface.Size = new Size(1164, 654);
            renderSurface.TabIndex = 0;
            // 
            // listBox1
            // 
            listBox1.FormattingEnabled = true;
            listBox1.ItemHeight = 15;
            listBox1.Location = new Point(1201, 12);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(212, 334);
            listBox1.TabIndex = 1;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1438, 656);
            Controls.Add(listBox1);
            Controls.Add(renderSurface);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
        }

        #endregion

        private WinForms.Rendering.WinFormBitmapRenderSurfaceControl renderSurface;
        private ListBox listBox1;
    }
}
