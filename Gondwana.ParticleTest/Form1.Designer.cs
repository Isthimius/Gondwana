namespace Gondwana.ParticleTest
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
            winFormBitmapRenderSurfaceControl1 = new Gondwana.WinForms.Rendering.WinFormBitmapRenderSurfaceControl();
            SuspendLayout();
            // 
            // winFormBitmapRenderSurfaceControl1
            // 
            winFormBitmapRenderSurfaceControl1.Location = new Point(16, 23);
            winFormBitmapRenderSurfaceControl1.Name = "winFormBitmapRenderSurfaceControl1";
            winFormBitmapRenderSurfaceControl1.Size = new Size(2118, 925);
            winFormBitmapRenderSurfaceControl1.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(2146, 960);
            Controls.Add(winFormBitmapRenderSurfaceControl1);
            Name = "Form1";
            Text = "Form1";
            FormClosing += Form1_FormClosing;
            ResumeLayout(false);
        }

        #endregion

        private WinForms.Rendering.WinFormBitmapRenderSurfaceControl winFormBitmapRenderSurfaceControl1;
    }
}
