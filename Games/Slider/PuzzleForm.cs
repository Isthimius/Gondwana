using Gondwana.Drawing.Sprites;
using Gondwana.Audio;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Gondwana.Audio.Midi;
using Gondwana.WinForms;
using Gondwana.WinForms.Input.Keyboard;
using Microsoft.Extensions.Logging;
using Gondwana;
using System.Linq;
using Gondwana.Input.Mouse;

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
            //picBoxDC = picBox.CreateGraphics();
            //Program.slideSound = new MediaFile("slide", AssetDir + "75143__willc2-45220__slide-cup-16b-44k-0-747s.wav", MediaFileType.wav);
            //Program.tadaSound = new MediaFile("tada", AssetDir + "177120__rdholder__2dogsound-tadaa1-3s-2013jan31-cc-by-30-us.wav", MediaFileType.wav);
            Sprites.SpriteMovementStarted += Sprites_SpriteMovementStarted;
            Sprites.SpriteMovementStopped += Sprites_SpriteMovementStopped;
        }

        void Sprites_SpriteMovementStarted(SpriteMovementEventArgs e)
        {
            Gondwana.Engine.Logger.LogDebug(string.Format("{3}   start move '{0}' from {1}:{2}", e.sprite.ID, e.sprite.GridCoordinates.X, e.sprite.GridCoordinates.Y, Environment.TickCount));
            //Program.slideSound.Play();
        }

        void Sprites_SpriteMovementStopped(SpriteMovementEventArgs e)
        {
            Engine.Logger.LogDebug(string.Format("{3}   stop move '{0}' at {1}:{2}", e.sprite.ID, e.sprite.GridCoordinates.X, e.sprite.GridCoordinates.Y, Environment.TickCount));

            //if (!Program.puzzle._isShuffling)
            //Program.slideSound.Stop();

            //if (Program.puzzle.TotalPieces == Program.puzzle.TotalPiecesCorrect)
            //    Program.tadaSound.Play();
        }

        private void btnBmpOpen_Click(object sender, EventArgs e)
        {
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

                Program.puzzle = new Puzzle(winFormBitmapRenderSurfaceControl1.RenderSurfaceHost, openFileBox.FileName, int.Parse(txtCol.Text), int.Parse(txtRow.Text), winFormBitmapRenderSurfaceControl1.Size);
                Sprites_SpriteMovePointFinished(null);

                if (!Gondwana.Engine.Instance.IsRunning)
                {
                    this.chkGrid.Enabled = true;
                    this.btnShuffle.Enabled = true;
                    Gondwana.Engine.Instance.PostInitialization += Instance_PostInitialization;

                    Gondwana.Engine.Instance.Configuration.TargetFPS = 120;
                    Gondwana.Engine.Instance.Start(SynchronizationContext.Current!);

                    Gondwana.Engine.Instance.CPSCalculated += Engine_CPSCalculated;
                }

                Engine.Instance.InitializeWinFormsKeyboardAdapter(winFormBitmapRenderSurfaceControl1);
                Engine.Instance.InitializeWinFormsMouseAdapter(winFormBitmapRenderSurfaceControl1);
                Engine.MouseEventPoller.MouseEvent += MouseEventPoller_MouseEvent;
            }
        }

        private void MouseEventPoller_MouseEvent(Gondwana.Input.Mouse.MouseEventArgs e)
        {
            if (Program.puzzle is null) return;

            var coords = Program.puzzle.GetGridCoordinates(e.CurrentPosition.X, e.CurrentPosition.Y);

            if (lblCoord.IsDisposed || !lblCoord.IsHandleCreated) return;
            if (lblCoord.InvokeRequired)
                lblCoord.BeginInvoke((Action)(() => lblCoord.Text = $"x: {coords.X}   y: {coords.Y}"));
            else
                lblCoord.Text = $"x: {coords.X}   y: {coords.Y}";

            if (e.ButtonStates.First(s => s.Key == Gondwana.Input.Mouse.MouseButton.Left).Value.JustPressed)
            {
                // TODO: also check if any sprites are moving
                if (!Program.puzzle._isShuffling)
                {
                    List<Sprite> sprites = Sprites.GetSpritesAtPoint(new Point(e.CurrentPosition.X, e.CurrentPosition.Y));
                    if (sprites.Count != 0)
                        Program.puzzle.SlidePiece(sprites[0], 0.15);
                }
            }
        }

        private void Instance_PostInitialization(object sender, EventArgs e)
        {
            //MidiFileReader.RegisterDefaultReaders();
        }

        private void Engine_CPSCalculated(object sender, Gondwana.CyclesPerSecondCalculatedEventArgs e)
        {
            lblInfo.Text = string.Format("FPS: {0}\r\nCPS: {1}\r\nSampling Time: {2}",
                e.NetCPS.ToString("N2"), e.GrossCPS.ToString("N2"), e.SamplingTime.ToString("N2"));
        }

        void Sprites_SpriteMovePointFinished(SpriteMovePointFinishedEventArgs e)
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
            if (Program.puzzle != null && !Program.puzzle._isShuffling)
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

        private void chkGrid_CheckedChanged(object sender, EventArgs e)
        {
            Program.puzzle.ShowGridLines = chkGrid.Checked;
        }

        private void PuzzleForm_Load(object sender, EventArgs e)
        {
            winFormBitmapRenderSurfaceControl1.Size = new Size(this.Width - winFormBitmapRenderSurfaceControl1.Left, this.Height);
        }

        private void PuzzleForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Program.puzzle != null)
            {
                if (Program.puzzle._spriteMoving)
                {
                    e.Cancel = true;
                    return;
                }
            }

            Gondwana.Engine.Instance.Stop();
        }

        private void PuzzleForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Program.puzzle != null)
                Program.puzzle.Dispose();
        }

        //private void winFormBitmapRenderSurfaceControl1_MouseMove(object sender, MouseEventArgs e)
        //{
        //    if (Program.puzzle != null)
        //    {
        //        var coords = Program.puzzle.GetGridCoordinates(e.X, e.Y);
        //        lblCoord.Text = "x: " + coords.X.ToString() + "   y: " + coords.Y.ToString();
        //    }
        //}

        //private void winFormBitmapRenderSurfaceControl1_MouseDown(object sender, MouseEventArgs e)
        //{
        //    if (Program.puzzle != null)
        //    {
        //        if (!Program.puzzle._isShuffling)
        //        {
        //            List<Sprite> sprites = Sprites.GetSpritesAtPoint(new Point(e.X, e.Y));
        //            if (sprites.Count != 0)
        //                Program.puzzle.SlidePiece(sprites[0], 0.15);
        //        }
        //    }
        //}

        //private void winFormBitmapRenderSurfaceControl1_Load(object sender, EventArgs e)
        //{

        //}

        //private void winFormBitmapRenderSurfaceControl1_Load_1(object sender, EventArgs e)
        //{

        //}

        //private void winFormBitmapRenderSurfaceControl1_MouseClick(object sender, MouseEventArgs e)
        //{
        //    if (Program.puzzle != null)
        //    {
        //        // TODO: also check if any sprites are moving
        //        if (!Program.puzzle._isShuffling)
        //        {
        //            List<Sprite> sprites = Sprites.GetSpritesAtPoint(new Point(e.X, e.Y));
        //            if (sprites.Count != 0)
        //                Program.puzzle.SlidePiece(sprites[0], 0.15);
        //        }
        //    }
        //}

        //private void winFormBitmapRenderSurfaceControl1_MouseCaptureChanged(object sender, EventArgs e)
        //{
        //    //if (Program.puzzle != null)
        //    //{
        //    //    var coords = Program.puzzle.GetGridCoordinates(e.X, e.Y);
        //    //    lblCoord.Text = "x: " + coords.X.ToString() + "   y: " + coords.Y.ToString();
        //    //}
        //}
    }
}
