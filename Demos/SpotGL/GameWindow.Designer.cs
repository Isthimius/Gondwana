using System.Drawing;
using System.Windows.Forms;
using Gondwana.WinForms.Rendering;

namespace Gondwana.Demos.SpotGL;

partial class GameWindow
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
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GameWindow));
        renderSurface = new WinFormGpuRenderSurfaceControl();
        SuspendLayout();
        // 
        // renderSurface
        // 
        renderSurface.BackColor = SystemColors.Desktop;
        renderSurface.Location = new Point(4, 3);
        renderSurface.Name = "renderSurface";
        renderSurface.Size = new Size(1433, 654);
        renderSurface.TabIndex = 0;
        // 
        // GameWindow
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1438, 656);
        Controls.Add(renderSurface);
        Icon = (Icon)resources.GetObject("$this.Icon");
        Name = "GameWindow";
        Text = "Spot! (GL)";
        ResumeLayout(false);
    }

    #endregion

    private WinFormGpuRenderSurfaceControl renderSurface;
}
