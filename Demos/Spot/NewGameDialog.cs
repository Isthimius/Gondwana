using SkiaSharp;
using Gondwana.Demos.Spot.Game;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Gondwana.Demos.Spot;

internal partial class NewGameDialog : Form
{
    internal NewGameOptions Options { get; private set; } = null!;

    internal NewGameDialog(NewGameOptions? initialOptions = null)
    {
        InitializeComponent();

        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;

        this.MinimizeBox = false;
        this.MaximizeBox = false;

        this.ShowInTaskbar = false;

        cboPlayerCount.SelectedIndex = 2; // default to 4 players
        cboWidth.SelectedIndex = 5;       // default to 8
        cboHeight.SelectedIndex = 5;      // default to 8

        cboPlayerType1.SelectedIndex = 0; // default to human
        cboPlayerType2.SelectedIndex = 1; // default to AI
        cboPlayerType3.SelectedIndex = 1; // default to AI
        cboPlayerType4.SelectedIndex = 1; // default to AI

        BuildColorComboBox(cboColor1);
        cboColor1.SelectedIndex = 0;

        BuildColorComboBox(cboColor2);
        cboColor2.SelectedIndex = 1;

        BuildColorComboBox(cboColor3);
        cboColor3.SelectedIndex = 2;

        BuildColorComboBox(cboColor4);
        cboColor4.SelectedIndex = 3;

        this.AcceptButton = cmdStart;
        this.CancelButton = cmdCancel;

        if (initialOptions != null)
            ApplyInitialOptions(initialOptions);
    }

    private void ApplyInitialOptions(NewGameOptions options)
    {
        int playerCountIndex = options.PlayerCount - 2; // combo items start at "2"
        if (playerCountIndex >= 0 && playerCountIndex < cboPlayerCount.Items.Count)
            cboPlayerCount.SelectedIndex = playerCountIndex;

        int widthIndex = options.BoardWidth - 3;   // combo items start at "3"
        if (widthIndex >= 0 && widthIndex < cboWidth.Items.Count)
            cboWidth.SelectedIndex = widthIndex;

        int heightIndex = options.BoardHeight - 3; // combo items start at "3"
        if (heightIndex >= 0 && heightIndex < cboHeight.Items.Count)
            cboHeight.SelectedIndex = heightIndex;

        var nameBoxes = new[] { textBox1, textBox2, textBox3, textBox4 };
        var typeSelects = new[] { cboPlayerType1, cboPlayerType2, cboPlayerType3, cboPlayerType4 };
        var colorSelects = new[] { cboColor1, cboColor2, cboColor3, cboColor4 };

        for (int i = 0; i < options.Players.Count && i < 4; i++)
        {
            var player = options.Players[i];
            nameBoxes[i].Text = player.Name;
            typeSelects[i].SelectedIndex = player.Type == PlayerType.Human ? 0 : 1;
            SetColorCombo(colorSelects[i], player.ColorItem.Color);
        }
    }

    private static void SetColorCombo(ComboBox cbo, SKColor color)
    {
        for (int i = 0; i < cbo.Items.Count; i++)
        {
            if (cbo.Items[i] is ColorItem ci && ci.Color == color)
            {
                cbo.SelectedIndex = i;
                return;
            }
        }
    }

    private void BuildColorComboBox(ComboBox cboColor)
    {
        var colors = new[]
        {
            new ColorItem("Red", SKColors.Red, SKColors.White),
            new ColorItem("Blue", SKColors.Blue, SKColors.White),
            new ColorItem("Yellow", SKColors.Yellow, SKColors.Blue),
            new ColorItem("Violet", SKColors.Violet, SKColors.White),
            new ColorItem("Green", SKColors.Green, SKColors.Black)
        };

        cboColor.DrawMode = DrawMode.OwnerDrawFixed;
        cboColor.DropDownStyle = ComboBoxStyle.DropDownList;
        cboColor.Items.Clear();

        foreach (var color in colors)
            cboColor.Items.Add(color);

        // Ensure handler only attached once
        cboColor.DrawItem -= ColorCombo_DrawItem;
        cboColor.DrawItem += ColorCombo_DrawItem;

        cboColor.SelectedIndexChanged -= ColorCombo_SelectedIndexChanged;
        cboColor.SelectedIndexChanged += ColorCombo_SelectedIndexChanged;
    }

    private void ColorCombo_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
            return;

        ComboBox combo = (ComboBox)sender;
        var item = (ColorItem)combo.Items[e.Index];

        var color = Color.FromArgb(item.Color.Alpha, item.Color.Red, item.Color.Green, item.Color.Blue);

        e.DrawBackground();

        Rectangle rect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, 20, e.Bounds.Height - 4);

        using (Brush brush = new SolidBrush(color))
            e.Graphics.FillRectangle(brush, rect);

        e.Graphics.DrawRectangle(Pens.Black, rect);

        e.Graphics.DrawString(item.Name, e.Font, Brushes.Black, e.Bounds.X + 26, e.Bounds.Y + 2);

        e.DrawFocusRectangle();
    }

    private void ColorCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        var changedCombo = (ComboBox)sender;
        var selected = changedCombo.SelectedItem as ColorItem;

        if (selected == null)
            return;

        foreach (var combo in new[] { cboColor1, cboColor2, cboColor3, cboColor4 })
        {
            if (combo == changedCombo)
                continue;

            var comboSelected = combo.SelectedItem as ColorItem;
            if (comboSelected == null)
                continue;

            // Compare by color value, not object reference
            if (comboSelected.Color == selected.Color)
            {
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    var candidate = combo.Items[i] as ColorItem;
                    if (candidate == null)
                        continue;

                    bool alreadyUsed = false;

                    foreach (var other in new[] { cboColor1, cboColor2, cboColor3, cboColor4 })
                    {
                        if (other == combo)
                            continue;

                        var otherSelected = other.SelectedItem as ColorItem;
                        if (otherSelected != null && otherSelected.Color == candidate.Color)
                        {
                            alreadyUsed = true;
                            break;
                        }
                    }

                    if (!alreadyUsed)
                    {
                        combo.SelectedIndex = i;
                        break;
                    }
                }
            }
        }
    }

    private void textBox1_TextChanged(object sender, System.EventArgs e)
    {
        groupBox1.Text = textBox1.Text;
    }

    private void textBox2_TextChanged(object sender, System.EventArgs e)
    {
        groupBox2.Text = textBox2.Text;
    }

    private void textBox3_TextChanged(object sender, System.EventArgs e)
    {
        groupBox3.Text = textBox3.Text;
    }

    private void textBox4_TextChanged(object sender, System.EventArgs e)
    {
        groupBox4.Text = textBox4.Text;
    }

    private void cboPlayerCount_SelectedIndexChanged(object sender, System.EventArgs e)
    {
        switch (cboPlayerCount.Text)
        {
            case "2":
                groupBox3.Visible = false;
                groupBox4.Visible = false;
                break;
            case "3":
                groupBox3.Visible = true;
                groupBox4.Visible = false;
                break;
            case "4":
                groupBox3.Visible = true;
                groupBox4.Visible = true;
                break;
            default:
                break;
        }
    }

    private void cmdStart_Click(object sender, EventArgs e)
    {
        SetNewGameOptions();
        DialogResult = DialogResult.OK;
    }

    private void cmdCancel_Click(object sender, EventArgs e)
    {
        SetNewGameOptions();
        DialogResult = DialogResult.Cancel;
    }

    private void SetNewGameOptions()
    {
        Options = new NewGameOptions
        {
            PlayerCount = int.Parse(cboPlayerCount.Text),
            BoardWidth = int.Parse(cboWidth.SelectedItem.ToString()),
            BoardHeight = int.Parse(cboHeight.SelectedItem.ToString())
        };

        var playerNames = new[] { textBox1, textBox2, textBox3, textBox4 };
        var playerTypes = new[] { cboPlayerType1, cboPlayerType2, cboPlayerType3, cboPlayerType4 };
        var playerColors = new[] { cboColor1, cboColor2, cboColor3, cboColor4 };

        for (int i = 0; i < Options.PlayerCount; i++)
        {
            var colorItem = (ColorItem)playerColors[i].SelectedItem;

            Options.Players.Add(new Player
            {
                Name = playerNames[i].Text,
                Type = playerTypes[i].SelectedIndex == 0 ? PlayerType.Human : PlayerType.Computer,
                ColorItem = colorItem
            });
        }
    }
}
