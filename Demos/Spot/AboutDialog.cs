using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace Gondwana.Demos.Spot;

public sealed class AboutDialog : Form
{
    private const string REPO_URL = "https://github.com/isthimius/gondwana";

    private readonly PrivateFontCollection _privateFonts = new();
    private Font? _madeWithFont;
    private Font? _linkFont;

    public AboutDialog()
    {
        Text = "About Spot!";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 570);
        BackColor = Color.Black;
        ForeColor = Color.White;

        var logoPath1 = Path.Combine(AppContext.BaseDirectory, "assets", "spot.png");
        var logoPath2 = Path.Combine(AppContext.BaseDirectory, "assets", "gondwana-logo-text.png");
        var fontPath = Path.Combine(AppContext.BaseDirectory, "assets", "ArchitectsDaughter-Regular.ttf");

        _privateFonts.AddFontFile(fontPath);
        _madeWithFont = new Font(_privateFonts.Families[0], 16f, FontStyle.Bold);
        _linkFont = new Font(Font.FontFamily, 14f, FontStyle.Underline);

        var logo1 = new PictureBox
        {
            Image = Image.FromFile(logoPath1),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(30, 0),
            Size = new Size(360, 240)
        };

        var logo2 = new PictureBox
        {
            Image = Image.FromFile(logoPath2),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(110, 195),
            Size = new Size(200, 200)
        };

        var description = new Label
        {
            Text = "Built with Gondwana Game Engine",
            Font = _madeWithFont,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, 405),
            Size = new Size(420, 30)
        };

        var repoLink = new LinkLabel
        {
            Text = "View Gondwana on GitHub",
            Font = _linkFont,
            LinkColor = Color.LightSkyBlue,
            ActiveLinkColor = Color.White,
            VisitedLinkColor = Color.LightSkyBlue,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, 450),
            Size = new Size(420, 25)
        };

        repoLink.LinkClicked += (_, _) =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = REPO_URL,
                UseShellExecute = true
            });
        };

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(160, 505),
            Size = new Size(100, 32)
        };

        AcceptButton = okButton;
        CancelButton = okButton;

        Controls.Add(logo1);
        Controls.Add(logo2);
        Controls.Add(description);
        Controls.Add(repoLink);
        Controls.Add(okButton);

        logo2.BringToFront();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _linkFont?.Dispose();
            _privateFonts.Dispose();
        }

        base.Dispose(disposing);
    }
}