using Gondwana;
using Gondwana.Common;
using Gondwana.Common.Collisions;
using Gondwana.Common.Drawing;
using Gondwana.Common.Drawing.Animation;
using Gondwana.Common.Drawing.Direct;
using Gondwana.Common.Drawing.Sprites;
using Gondwana.Common.Enums;
using Gondwana.Common.EventArgs;
using Gondwana.Common.Grid;
using Gondwana.Coordinates;
using Gondwana.Input.EventArgs;
using Gondwana.Input.Keyboard;
using Gondwana.Media;
using Gondwana.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Forms;

namespace GondwanaWinTest
{
    static class Program
    {
        //public const string path = "D:\\Milestone\\Gondwana\\";
        public const string path = @"E:\HiddenWorldsGames\Games\Gondwana\";

        private static long totalEngineCycles = 0;
        private static bool stopEngine = false;

        private static Tilesheet tilesheet;
        private static Tilesheet tilesheetMask;
        public static Tilesheet sprtBmp;
        private static Tilesheet sprtBmpMask;
        public static GridPointMatrixes layers;
        public static GridPointMatrix matrix;
        public static GridPointMatrix matrix2;
        //public static GridPointMatrix matrix3;
        public static VisibleSurface visSurf;
        public static Sprite sprite;
        public static Text fpsCounter;
        public static double fps;
        public static double cps;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Engine.AfterEngineCycle += new Gondwana.Common.EventArgs.EngineCycleEventHandler(Engine_AfterEngineCycle);
            Keyboard.KeyDown += new Gondwana.Input.Keyboard.Keyboard.KeyDownEventHandler(Keyboard_KeyDown);
            Engine.TileCollisions += new Gondwana.Common.EventArgs.CollisionEventHandler(Tile_SpriteCollision);
            Engine.Configuration.Settings.TargetFPS = 240;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            Engine.Configuration.Settings.ResizedFrameCacheLimit = 123;
            Engine.Configuration.StateFiles.LoadAtStartup = false;
            Engine.Configuration.StateFiles.Add(new Gondwana.Common.Configuration.EngineStateFile() { ID = "file1", Path = @"c:\test\file10.gscr" });
            Engine.Configuration.StateFiles.Add(new Gondwana.Common.Configuration.EngineStateFile() { ID = "file2", Path = @"c:\test\file20.gscr" });
            Engine.Configuration.Save();
            Engine.Configuration.Save(@"E:\TFS\Hidden Worlds Games\Gondwana\TestHarness\GondwanaWinTest\bin\Debug\hi.config");

            Engine.State.Save(@"c:\test\file10.gscr", false);
        }

        public static void TestEngine(Form1 frm)
        {
            //do
            //{
            //    Engine.Cycle();
            //} while (!stopEngine);

            Engine.Start();

            string msg = "ran for: " + Gondwana.Engine.TotalSecondsEngineRunning.ToString();
            msg += "\r\ntotal cycles: " + totalEngineCycles.ToString();
            msg += "\r\ncps: " + Gondwana.Engine.CyclesPerSecond.ToString();
            msg += "\r\nfps: " + Gondwana.Engine.FramesPerSecond.ToString();
            msg += "\r\ntotal sprites: " + Gondwana.Common.Drawing.Sprites.Sprites.AllSprites.Count.ToString();
            msg += "\r\nEnvironment.CurrentDirectory: " + Environment.CurrentDirectory;
            MessageBox.Show(msg);

            visSurf.RenderBackbuffer(false);
            stopEngine = false;


            //Parser.WriteToFile(path + "bmp.txt", System.IO.FileMode.OpenOrCreate, Bitmaps.AllTilesheets);
            //Gondwana.Scripting.Parser.WriteToFile(path + "bmp.txt", System.IO.FileMode.Append, Program.sprite.TileAnimator.CurrentCycle);
            List<GridPointMatrix> matrixes = new List<GridPointMatrix>();
            matrixes.Add(matrix);
            matrixes.Add(matrix2);
            //matrixes.Add(matrix3);
            //Parser.WriteToFile(path + "bmp.txt", System.IO.FileMode.Append, matrixes);
        }

        public static void AddMovePt(int x, int y)
        {
            PointF ptF = matrix.CoordinateSystem.GetGridPtAtPxl(matrix, new Point(x, y));
            sprite.SpriteMovement.AddMovePoint(3000, ptF);
            sprite.SpriteMovement.Start();
            //sprite.MoveSprite(ptF);
        }

        public static void AddScrollPt(int x, int y)
        {
            PointF ptF = matrix.CoordinateSystem.GetGridPtAtPxl(matrix, new Point(x, y));
            matrix.ScrollSourceGridPoint(2000, ptF);
        }

        public static void ResizePt(Rectangle newLoc)
        {
            //sprite.MoveSprite(newLoc);
            sprite.SpriteMovement.Start(2000, newLoc);
        }

        public static void PlayMedia(Form frm)
        {
            //string file = path + "Castle4_Simon.mid";
            string file = @"d:\documents and settings\madkins\My Documents\My Music\Code Monkey.mp3";
            //string file = path + @"\media\Raton.asx";
            //string file2 = path + @"\media\Copy of Castle4_Simon.mid";

            var monkeySong = new MediaFile("monkey", file, MediaFileType.mp3);
            monkeySong.Looping = true;
            monkeySong.FullScreen = false;
            monkeySong.Play(frm);

            //player.Open(file2, i.ToString());
            //player.Play(i.ToString());
            //i++;

            //System.Media.SoundPlayer player = new System.Media.SoundPlayer(file);
            //player.Play();

            //System.Media.SoundPlayer player2 = new System.Media.SoundPlayer(file2);
            //player2.Play();
        }

        #region instantiation methods
        public static void LoadBitmaps()
        {
            tilesheet = new Tilesheet("original", path + "graphics\\original.bmp");
            tilesheet.TileSize = new Size(64, 32);
            tilesheet.InitialOffsetX = 1;
            tilesheet.InitialOffsetY = 1;
            tilesheet.XPixelsBetweenTiles = 1;
            tilesheet.YPixelsBetweenTiles = 1;

            tilesheetMask = new Tilesheet("original_mask", path + "graphics\\original_mask.bmp");
            tilesheetMask.TileSize = new Size(64, 32);
            tilesheetMask.InitialOffsetX = 1;
            tilesheetMask.InitialOffsetY = 1;
            tilesheetMask.XPixelsBetweenTiles = 1;
            tilesheetMask.YPixelsBetweenTiles = 1;

            sprtBmp = new Tilesheet("rooster", path + "graphics\\rooster.bmp");
            sprtBmp.TileSize = new Size(50, 50);

            sprtBmpMask = new Tilesheet("rooster_mask", path + "graphics\\rooster_mask.bmp");
            sprtBmpMask.TileSize = new Size(50, 50);


            tilesheet.Mask = tilesheetMask;
            sprtBmp.Mask = sprtBmpMask;

            //tilesheet.ApplyBmpSettingsFile(@"E:\TFS\Hidden Worlds Games\Gondwana\TestHarness\GondwanaWinTest\resources\XMLFile1.xml");
            //tilesheetMask.ApplyBmpSettingsFile(@"E:\TFS\Hidden Worlds Games\Gondwana\TestHarness\GondwanaWinTest\resources\XMLFile1.xml");
            //sprtBmp.ApplyBmpSettingsFile(@"E:\TFS\Hidden Worlds Games\Gondwana\TestHarness\GondwanaWinTest\resources\XMLFile2.xml");
            //sprtBmpMask.ApplyBmpSettingsFile(@"E:\TFS\Hidden Worlds Games\Gondwana\TestHarness\GondwanaWinTest\resources\XMLFile2.xml");

            //tilesheet.SaveBmpSettingsFile(path + "tilesheet.xml");
            //tilesheetMask.SaveBmpSettingsFile(path + "tilesheetMask.xml");

            //tilesheet.SaveBmpSettingsFile(@"E:\Finances\" + tilesheet.Name + ".xml");

            FileStream writer = new FileStream(@"E:\TFS\Hidden Worlds Games\Gondwana\TestHarness\GondwanaWinTest\resources\tilesets.xml", FileMode.Create);
            DataContractSerializer ser = new DataContractSerializer(typeof(Tilesheet));
            ser.WriteObject(writer, tilesheet);
            ser.WriteObject(writer, tilesheetMask);
            ser.WriteObject(writer, sprtBmp);
            ser.WriteObject(writer, sprtBmpMask);
            writer.Close();
        }

        public static void LoadMatrixLayers()
        {
            Gondwana.Common.Timers.Timers.Add("matrix_move", TimerType.PreCycle, TimerCycles.Repeating, 0.01).Tick +=
                new Gondwana.Common.EventArgs.TimerEventHandler(Timers_Tick);

            matrix = new GridPointMatrix(8, 8, 64, 32);
            matrix.WrapHorizontally = false;
            matrix.WrapVertically = false;
            matrix.CoordinateSystem = new SquareIsoCoordinates();

            layers = new GridPointMatrixes(matrix);

            matrix2 = new GridPointMatrix(12, 12, 64, 32);
            matrix2.CoordinateSystem = new SquareIsoCoordinates();

            matrix2.BindScrollingToParentGrid(matrix);

            layers.AddLayer(matrix2).LayerSyncModifier = (float)0.25;

            //matrix3 = new GridPointMatrix(12, 12, 64, 32);
            //layers.AddLayer(matrix3).LayerSyncModifier = (float)0.5;
            //matrix3.CoordinateSystem = new SquareIsoCoordinates();
            //matrix3.BindScrollingToParentGrid(matrix);

            /*
            foreach (GridPoint gridPt in matrix)
            {
                if (gridPt.GridCoordinates.X < 2 && gridPt.GridCoordinates.Y < 2)
                {
                    gridPt.EnableFog = false;
                    gridPt.CurrentFrame = new Frame(tilesheet, 2, 7);
                }
            }
            */

            //matrix[5, 5].CurrentFrame = new Frame(tilesheet, 2, 7);

            foreach (GridPoint gridPt in matrix)
                gridPt.CurrentFrame = new Frame(tilesheet, 0, 0);

            int i = 0;
            foreach (GridPoint gridPt in matrix2)
            {
                switch (i++ % 3)
                {
                    case 0:
                        gridPt.CurrentFrame = new Frame(tilesheet, 1, 6);
                        break;
                    case 1:
                        gridPt.CurrentFrame = new Frame(tilesheet, 1, 7);
                        break;
                    case 2:
                        gridPt.CurrentFrame = new Frame(tilesheet, 1, 8);
                        break;
                    default:
                        break;
                }
            }
        }

        static void Timers_Tick(Gondwana.Common.EventArgs.TimerEventArgs e)
        {
            bool isShift = Keyboard.GetKeyDownState(Keys.ShiftKey);
            if (isShift)
                return;

            if (Keyboard.GetKeyDownState(Keys.Up))
                sprite.SpriteMovement.AccelerationY = (-5);
            else if (Keyboard.GetKeyDownState(Keys.Down))
                sprite.SpriteMovement.AccelerationY = 5;
            else
                sprite.SpriteMovement.AccelerationY = 0;

            if (Keyboard.GetKeyDownState(Keys.Left))
                sprite.SpriteMovement.AccelerationX = (-2.5);
            else if (Keyboard.GetKeyDownState(Keys.Right))
                sprite.SpriteMovement.AccelerationX = 2.5;
            else
                sprite.SpriteMovement.AccelerationX = 0;
        }

        public static void LoadVisibleSurfaces(Form1 frm)
        {
            Graphics g = frm.CreateGraphics();
            visSurf = new VisibleSurface(g, frm.Width, frm.Height);
            visSurf.Bind(layers);

            layers[0].ShowGridLines = false;
            layers[1].ShowGridLines = false;

            visSurf.RedrawDirtyRectangleOnly = true;

            fpsCounter = new Text(visSurf, "fps:", new Font("Times New Roman", 8),
                new Rectangle(0, 0, 200, 300), Color.White, Color.Transparent);

            Engine.CPSCalculated += new Gondwana.Common.EventArgs.CyclesPerSecondCalculatedHandler(Engine_CPSCalculated);
        }

        static void Engine_CPSCalculated(Gondwana.Common.EventArgs.CyclesPerSecondCalculatedEventArgs e)
        {
            fps = e.NetCPS;
            cps = e.GrossCPS;
        }

        public static void LoadSprite()
        {
            FrameSequence seq = new FrameSequence();
            seq.AddFrame(sprtBmp, 0, 0);
            seq.AddFrame(sprtBmp, 1, 0);
            seq.AddFrame(sprtBmp, 2, 0);
            seq.AddFrame(sprtBmp, 3, 0);
            seq.SequenceCycleType = Gondwana.Common.Enums.CycleType.PingPong;

            Cycle cycle = new Cycle(seq, 0.03, "groovin");

            sprite = Sprites.CreateSprite(matrix, seq[0]);
            sprite.TileAnimator.CurrentCycle = cycle;
            sprite.RenderSize = new Size(50, 50);
            sprite.VertAlign = Gondwana.Common.Enums.VerticalAlignment.Top;
            sprite.HorizAlign = Gondwana.Common.Enums.HorizontalAlignment.Left;
            sprite.MoveSprite(3, 3);
            sprite.Visible = true;
            sprite.TileAnimator.StartAnimation();
            sprite.DetectCollision = CollisionDetection.All;

            //matrix2[1, 1].EnableAnimator = true;
            //matrix2[1, 1].TileAnimator.CurrentCycle = Cycles.GetAnimationCycle("groovin");
            //matrix2[1, 1].TileAnimator.StartAnimation();
        }

        public static void LoadSounds()
        {
            var chickenSound = new MediaFile("chicken", path + @"\media\chicken-1.wav", MediaFileType.wav);
            var boomSound = new MediaFile("boom", path + @"\media\explosion-01.wav", MediaFileType.wav);
        }

        public static void InitializeKeyboardEvents()
        {
            Keyboard.StartMonitoringKey(Keys.A);
            Keyboard.StartMonitoringKey(Keys.S);
            Keyboard.StartMonitoringKey(Keys.X);
            Keyboard.StartMonitoringKey(Keys.Y);
            Keyboard.StartMonitoringKey(Keys.Escape);
            Keyboard.StartMonitoringKey(Keys.Left);
            Keyboard.StartMonitoringKey(Keys.Right);
            Keyboard.StartMonitoringKey(Keys.Up);
            Keyboard.StartMonitoringKey(Keys.Down);
            Keyboard.StartMonitoringKey(Keys.Q);
            Keyboard.StartMonitoringKey(Keys.Z);
            Keyboard.StartMonitoringKey(Keys.D);
            Keyboard.StartMonitoringKey(Keys.C);
            Keyboard.StartMonitoringKey(Keys.V);
            Keyboard.StartMonitoringKey(Keys.B);
            Keyboard.StartMonitoringKey(Keys.W);

            Keyboard.SetTimeBetweenEvents(Keys.C, 0.5);
            Keyboard.SetTimeBetweenEvents(Keys.V, 0.5);
            Keyboard.SetTimeBetweenEvents(Keys.B, 2.5);
            Keyboard.SetTimeBetweenEvents(Keys.W, 0.5);
            Keyboard.SetTimeBetweenEvents(Keys.D, 0.5);
        }
        #endregion

        #region event handlers
        static void Engine_AfterEngineCycle(EngineCycleEventArgs e)
        {
            totalEngineCycles++;

            StringBuilder msg = new StringBuilder();
            msg.AppendLine(string.Format("fps: {0}", fps.ToString("F3")));
            msg.AppendLine(string.Format("fps (event): {0}", e.NetFPS.ToString("F3")));
            msg.AppendLine(string.Format("cps: {0}", cps.ToString("F3")));
            msg.AppendLine(string.Format("cps (event): {0}", e.GrossCPS.ToString("F3")));
            msg.AppendLine(string.Format("net cycles: {0}", e.NetCyclesTotal.ToString()));
            msg.AppendLine(string.Format("gross cycles: {0}", e.GrossCyclesTotal.ToString()));
            msg.AppendLine(string.Format("seconds running: {0}", Gondwana.Engine.TotalSecondsEngineRunning.ToString()));
            msg.AppendLine(string.Format("sprites: {0}", Sprites.AllSprites.Count.ToString()));
            msg.AppendLine(string.Format("x1: {0}", matrix.SourceGridPoint.X.ToString("F3")));
            msg.AppendLine(string.Format("x2: {0}", matrix2.SourceGridPoint.X.ToString("F3")));
            msg.AppendLine(string.Format("y1: {0}", matrix.SourceGridPoint.Y.ToString("F3")));
            msg.AppendLine(string.Format("y2: {0}", matrix2.SourceGridPoint.Y.ToString("F3")));
            msg.AppendLine(string.Format("sprite x: {0}", sprite.GridCoordinates.X.ToString("F3")));
            msg.AppendLine(string.Format("sprite y: {0}", sprite.GridCoordinates.Y.ToString("F3")));
            msg.AppendLine(string.Format("DirectDrawings: {0}", DirectDrawing.Count.ToString()));
            msg.AppendLine(string.Format("GridPt Zero Pxl: {0}", matrix.GridPointZeroPixel.ToString()));
            msg.AppendLine(string.Format("Sprite source pxl: {0}", sprite.DrawLocation.Location.ToString()));
            msg.AppendLine(string.Format("X velocity: {0}", sprite.SpriteMovement.VelocityX.ToString("F3")));
            msg.AppendLine(string.Format("Y velocity: {0}", sprite.SpriteMovement.VelocityY.ToString("F3")));
            msg.AppendLine(string.Format("X acceleration: {0}", sprite.SpriteMovement.AccelerationX.ToString("F3")));
            msg.AppendLine(string.Format("Y acceleration: {0}", sprite.SpriteMovement.AccelerationY.ToString("F3")));

            fpsCounter.TextDisplay = msg.ToString();
        }

        static void Keyboard_KeyDown(KeyDownEventArgs e)
        {
            switch (e.KeyConfig.Key)
            {
                case Keys.Up:
                    if (e.IsShift)
                        //matrix.SetSourceGridPoint(new PointF(matrix.SourceGridPoint.X, matrix.SourceGridPoint.Y - (float).1));
                        matrix.VelocityY = -1;
                    //else
                    //    //sprite.MoveSprite(sprite.GridCoordinates.X, sprite.GridCoordinates.Y - .1);
                    //    sprite.SpriteMovement.VelocityY = -1;
                    break;
                case Keys.Down:
                    if (e.IsShift)
                        //matrix.SetSourceGridPoint(new PointF(matrix.SourceGridPoint.X, matrix.SourceGridPoint.Y + (float).1));
                        matrix.VelocityY = 1;
                    //else
                    //    //sprite.MoveSprite(sprite.GridCoordinates.X, sprite.GridCoordinates.Y + .1);
                    //    sprite.SpriteMovement.VelocityY = 1;
                    break;
                case Keys.Left:
                    if (e.IsShift)
                        //matrix.SetSourceGridPoint(new PointF(matrix.SourceGridPoint.X - (float).1, matrix.SourceGridPoint.Y));
                        matrix.VelocityX = -1;
                    //else
                    //    //sprite.MoveSprite(sprite.GridCoordinates.X - 0.1, sprite.GridCoordinates.Y);
                    //    sprite.SpriteMovement.VelocityX = -1;
                    break;
                case Keys.Right:
                    if (e.IsShift)
                        //matrix.SetSourceGridPoint(new PointF(matrix.SourceGridPoint.X + (float).1, matrix.SourceGridPoint.Y));
                        matrix.VelocityX = 1;
                    //else
                    //    //sprite.MoveSprite(sprite.GridCoordinates.X + 0.1, sprite.GridCoordinates.Y);
                    //    sprite.SpriteMovement.VelocityX = 1;
                    break;
                case Keys.A:
                    matrix.Visible = true;
                    break;
                case Keys.S:
                    matrix.Visible = false;
                    break;
                case Keys.X:
                    Sprites.PauseAllAnimation(true);
                    break;
                case Keys.Y:
                    Sprites.PauseAllAnimation(false);
                    break;
                case Keys.Escape:
                    stopEngine = true;
                    break;
                case Keys.Q:
                    sprite.TileAnimator.StopAnimation();
                    break;
                case Keys.Z:
                    sprite.TileAnimator.StartAnimation();
                    break;
                case Keys.D:
                    sprite.Dispose();
                    sprite = null;
                    DirectDrawing.Clear();
                    MediaFile.GetMediaFile("boom").Play();;
                    break;
                case Keys.C:
                    //Sprite cloned = (Sprite)sprite.Clone();
                    //cloned.TileAnimator.StartAnimation("groovin");
                    MediaFile.GetMediaFile("chicken").Play();
                    break;
                case Keys.V:
                    //MediaFile.FullScreen = !MediaFile.FullScreen;
                    break;
                case Keys.B:
                    //DirectDrawing.ClearAll();
                    Text text = new Text(visSurf, "BEWARE THE HORNY CHICKENS",
                        new Font("Times New Roman", 24), new Rectangle(200, 200, 700, 100),
                        Color.Orange, Color.Transparent, TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter);
                    break;
                case Keys.W:
                    matrix.WrapHorizontally = !matrix.WrapHorizontally;
                    //matrix3.WrapHorizontally = !matrix3.WrapHorizontally;
                    break;
                default:
                    break;
            }
        }

        static void Tile_SpriteCollision(Gondwana.Common.EventArgs.CollisionEventArgs e)
        {
            foreach (Collision collision in e.Collisions)
            {
                Sprite sprite = collision.SecondaryTile as Sprite;
                Movement movement = sprite.SpriteMovement;

                //switch (collision.CollisionDirectionFrom)
                //{
                //    case CollisionDirectionFrom.N:
                movement.Stop();

                movement.AddMovePoint(0.500, new PointF(sprite.GridCoordinates.X, sprite.GridCoordinates.Y - 1));
                movement.AddMovePoint(0.500, new PointF(sprite.GridCoordinates.X + 1, sprite.GridCoordinates.Y - 1));
                movement.AddMovePoint(0.500, new PointF(sprite.GridCoordinates.X + 1, sprite.GridCoordinates.Y));
                movement.AddMovePoint(0.500, new PointF(sprite.GridCoordinates.X, sprite.GridCoordinates.Y));

                movement.AddMovePoint(movement.CurrentMovePoint);
                //movement.AddMovePoint(movement.CurrentMovePoint.NextMovePoint);

                //movement.Start(1000, new PointF(sprite.GridCoordinates.X, sprite.GridCoordinates.Y - 1));
                //movement.AddMovePoint(500, new PointF(sprite.GridCoordinates.X, sprite.GridCoordinates.Y - (float)1.5));
                //MessageBox.Show("total time: " + movement.TimeRemaining.ToString());
                movement.Start();
                //    break;
                //case CollisionDirectionFrom.NE:
                //    break;
                //case CollisionDirectionFrom.E:
                //    break;
                //case CollisionDirectionFrom.SE:
                //    break;
                //case CollisionDirectionFrom.S:
                //    break;
                //case CollisionDirectionFrom.SW:
                //    break;
                //case CollisionDirectionFrom.W:
                //    break;
                //case CollisionDirectionFrom.NW:
                //    break;
                //case CollisionDirectionFrom.Center:
                //    break;
                //}
            }
        }
        #endregion
    }
}