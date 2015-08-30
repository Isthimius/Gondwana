using Gondwana;
using Gondwana.Common;
using Gondwana.Common.Drawing.Direct;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GondwanaWinTest
{
    public partial class Form1 : Form
    {
        private Point start;
        private Point end;
        private bool firstRun = true;
        private bool dragging = false;
        
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (firstRun)
            {
                //LoadBitmaps();
                //Bitmaps.ClearAllTilesheets();

                //Parser.ReadFromFile(Program.path + "bmp.txt");
                //Program.layers = new GridPointMatrixLayers(Parser.Matrixes);

                Program.LoadBitmaps();
                Program.LoadMatrixLayers();
                Program.LoadVisibleSurfaces(this);
                Program.LoadSprite();
                Program.InitializeKeyboardEvents();
                Program.LoadSounds();

                firstRun = false;
            }

            Program.TestEngine(this);
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            start = new Point(e.X, e.Y);
            //Program.AddMovePt(e.X, e.Y);
            //Program.AddScrollPt(e.X, e.Y);
            dragging = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Program.visSurf.RenderBackbuffer(false);
        }

        private void Form1_Deactivate(object sender, EventArgs e)
        {
            //Gondwana.Input.Keyboard.PauseAllKeyEvents = true;
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            Gondwana.Input.Keyboard.Keyboard.PauseAllKeyEvents = false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Program.visSurf.Buffer.SaveToFile(Program.path + "backbuffer.bmp");
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            end = new Point(e.X, e.Y);
            Rectangle rect = new Rectangle(start, new Size(end.X - start.X, end.Y - start.Y));
            //Program.ResizePt(rect);
            dragging = false;
            //DirectDrawing.ClearAll();
            //DirectRectangle directRect = new DirectRectangle(Program.visSurf, rect, 10, Color.Purple);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            //if (Program.sprite.ParentGrid == Program.matrix)
            //    Program.sprite.MoveSprite(Program.matrix2);
            //else
            //    Program.sprite.MoveSprite(Program.matrix);
            Program.matrix.SetSourceGridPoint(0, 0);
            Program.sprite.MoveSprite(0, 0);
            Program.sprite.SpriteMovement.Stop();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Program.PlayMedia(this);
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                end = new Point(e.X, e.Y);
                Rectangle rect = new Rectangle(start, new Size(end.X - start.X, end.Y - start.Y));

                DirectRectangle directRect = (DirectRectangle)DirectDrawing.GetDirectDrawing("purple");

                if (directRect == null)
                {
                    directRect = new DirectRectangle(Program.visSurf, rect, Color.Purple, true, 128);
                    directRect.Name = "purple";
                }
                else
                    directRect.Bounds = rect;

                //DirectImage directImg = new DirectImage(Program.visSurf, rect, Program.sprtBmp);
                directRect.ZOrder = 2;
                //directImg.ZOrder = 1;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Program.visSurf != null)
                Program.visSurf.Dispose();
        }

        private void Form1_MouseClick(object sender, MouseEventArgs e)
        {
            MessageBox.Show(Program.matrix.CoordinateSystem.GetGridPtAtPxl(Program.matrix, new Point(e.X, e.Y)).ToString());
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button6_Click(object sender, EventArgs e)
        {
            Program.matrix.ScrollSourceGridPoint(5, new PointF(8, 8));
        }

        private void button7_Click(object sender, EventArgs e)
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath) + @"\";
            Engine.State.Save(dir + "text.xml", false);
            Engine.State.Save(dir + "binary.txt", true);

            //var xmlState = EngineState.GetEngineState(dir + "text.xml", false);
            var binState = EngineState.GetEngineState(dir + "binary.txt", true);

            //xmlState.Save(dir + "in_out_xml.xml", false);
            binState.Save(dir + "in_out_bin.xml", true);
        }
    }
}