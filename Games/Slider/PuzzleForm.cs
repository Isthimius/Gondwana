using Gondwana.Common.Drawing.Sprites;
using Gondwana.Media;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Slider
{
    public partial class PuzzleForm : Form
    {
        public static string AssetDir;

        public Bitmap picBoxBmp;
        public Graphics picBoxDC;

        static PuzzleForm()
        {
            AssetDir = Application.ExecutablePath;
            AssetDir = string.Format("{0}\\assets\\", Path.GetDirectoryName(AssetDir));
        }

        public PuzzleForm()
        {
            InitializeComponent();
            picBoxDC = picBox.CreateGraphics();
            Program.slideSound = new MediaFile("slide", AssetDir + "75143__willc2-45220__slide-cup-16b-44k-0-747s.wav", MediaFileType.wav);
            Program.tadaSound = new MediaFile("tada", AssetDir + "177120__rdholder__2dogsound-tadaa1-3s-2013jan31-cc-by-30-us.wav", MediaFileType.wav);
            Sprites.SpriteMovementStarted += Sprites_SpriteMovementStarted;
            Sprites.SpriteMovementStopped += Sprites_SpriteMovementStopped;
        }

        void Sprites_SpriteMovementStarted(Gondwana.Common.EventArgs.SpriteMovementEventArgs e)
        {
#if DEBUG
            Console.WriteLine(string.Format("{3}   start move '{0}' from {1}:{2}", e.sprite.ID, e.sprite.GridCoordinates.X, e.sprite.GridCoordinates.Y, Environment.TickCount));
#endif
            Program.slideSound.Play();
        }

        void Sprites_SpriteMovementStopped(Gondwana.Common.EventArgs.SpriteMovementEventArgs e)
        {
#if DEBUG
            Console.WriteLine(string.Format("{3}   end move '{0}' at {1}:{2}", e.sprite.ID, e.sprite.GridCoordinates.X, e.sprite.GridCoordinates.Y, Environment.TickCount));
#endif

            //if (!Program.puzzle._isShuffling)
                Program.slideSound.Stop();

            if (Program.puzzle.TotalPieces == Program.puzzle.TotalPiecesCorrect)
                Program.tadaSound.Play();
        }

        private void btnBmpOpen_Click(object sender, EventArgs e)
        {
            Gondwana.Common.Timers.Timers.Clear();

            if (picBoxBmp != null)
            {
                picBoxBmp.Dispose();
                picBoxBmp = null;
            }

            openFileBox.Filter = "Image Files(*.jpg; *.jpeg; *.gif; *.bmp)|*.jpg; *.jpeg; *.gif; *.bmp";
            openFileBox.Multiselect = false;

            if (openFileBox.ShowDialog(this) == DialogResult.OK)
            {
                if (Program.puzzle != null)
                    Program.puzzle.Dispose();

                Program.puzzle = new Puzzle(picBoxDC, openFileBox.FileName, int.Parse(txtCol.Text), int.Parse(txtRow.Text), picBox.Size);
                Sprites_SpriteMovePointFinished(null);

                if (!Gondwana.Engine.IsRunning)
                {
                    this.chkGrid.Enabled = true;
                    this.btnShuffle.Enabled = true;
                    Gondwana.Engine.Start();
                }
            }
        }

        void Sprites_SpriteMovePointFinished(Gondwana.Common.EventArgs.SpriteMovePointFinishedEventArgs e)
        {
            txtPieces.Text = Program.puzzle.TotalPieces.ToString();
            txtCorrect.Text = Program.puzzle.TotalPiecesCorrect.ToString();
        }

        private void txtRow_Leave(object sender, EventArgs e)
        {
            int val;
            int.TryParse(txtRow.Text, out val);
            if (val < 3 || val > 20)
            {
                MessageBox.Show("Please enter a numeric value between 3 and 20.");
                txtRow.Text = "3";
            }
        }

        private void btnShuffle_Click(object sender, EventArgs e)
        {
            if (!Program.puzzle._isShuffling)
            {
                int numberOfSlides = Program.puzzle.Rows * Program.puzzle.Columns * 3;
                double slideTime = (double)15 / (double)numberOfSlides;
                Program.puzzle.Shuffle(numberOfSlides, slideTime);
            }
        }

        private void txtCol_Leave(object sender, EventArgs e)
        {
            int val;
            int.TryParse(txtCol.Text, out val);
            if (val < 3 || val > 20)
            {
                MessageBox.Show("Please enter a numeric value between 3 and 20.");
                txtCol.Text = "3";
            }
        }

        private void picBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (!Program.puzzle._isShuffling)
            {
                List<Sprite> sprites = Sprites.GetSpritesAtPoint(new Point(e.X, e.Y));
                if (sprites.Count != 0)
                    Program.puzzle.SlidePiece(sprites[0], 0.15);
            }
        }

        private void chkGrid_CheckedChanged(object sender, EventArgs e)
        {
            Program.puzzle.ShowGridLines = chkGrid.Checked;
        }

        private void PuzzleForm_Load(object sender, EventArgs e)
        {
            picBox.Size = new Size(this.Width - picBox.Left, this.Height);
        }

        private void PuzzleForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Program.puzzle != null)
            {
                if (Program.puzzle._spriteMoving == true)
                {
                    e.Cancel = true;
                    return;
                }
            }

            Gondwana.Engine.Stop();
        }

        private void PuzzleForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Program.puzzle != null)
                Program.puzzle.Dispose();
        }

        private void picBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (Program.puzzle != null)
            {
                var coords = Program.puzzle.GetGridCoordinates(e.X, e.Y);
                lblCoord.Text = "x: " + coords.X.ToString() + "   y: " + coords.Y.ToString();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 3; i++)
            {
                Program.slideSound.Play();
                //Thread.Sleep(750);
            }
        }
    }
}
