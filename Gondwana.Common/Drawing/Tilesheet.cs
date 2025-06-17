using Gondwana.Common.EventArgs;
using Gondwana.Common.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using Gondwana.Common.Resource;

namespace Gondwana.Common.Drawing
{
    /// <summary>
    /// <see cref="Bitmap"/> instance and related metadata corresponding to a single graphic and/or 2D tilesheet
    /// </summary>
    [DataContract(IsReference = true)]
    public class Tilesheet : IDisposable
    {
        #region events
        public event TilesheetDisposedHandler Disposed;
        #endregion

        #region public fields
        /// <summary>
        /// pixels; used to trim left edge
        /// </summary>
        [DataMember]
        public int InitialOffsetX;

        /// <summary>
        /// pixels; used to trim top edge
        /// </summary>
        [DataMember]
        public int InitialOffsetY;

        /// <summary>
        /// pixels; vertical pixels between <see cref="Frame"/>s
        /// </summary>
        [DataMember]
        public int XPixelsBetweenTiles;

        /// <summary>
        /// pixels; horizontal pixels between <see cref="Frame"/>s
        /// </summary>
        [DataMember]
        public int YPixelsBetweenTiles;

        /// <summary>
        /// <see cref="Tilesheet"/> instance of masking <see cref="Bitmap"/>
        /// </summary>
        [DataMember]
        public Tilesheet Mask;
        #endregion

        #region private / internal fields
        [DataMember]
        private Size _tileSize;

        [DataMember]
        private string _name;

        private Bitmap _bmp;
        private Graphics _dc;

        [DataMember]
        private int _extraTopSpace;    // pixels; for use in isometric displays
        private IntPtr _hDC;
        private IntPtr _hBmp;
        private string _imgFile;
        #endregion

        #region constructors / finalizer
        private Tilesheet() { }

        public Tilesheet(string name, Bitmap bmp)
        {
            InitVals(name, bmp, null, null);
            ValueBag = new Dictionary<string, string>();
        }

        public Tilesheet(string name, Stream stream)
        {
            InitVals(name, new Bitmap(stream), null, null);
            ValueBag = new Dictionary<string, string>();
        }

        public Tilesheet(string name, string file)
        {
            InitVals(name, null, file, null);
            ValueBag = new Dictionary<string, string>();
        }

        public Tilesheet(string file)
        {
            InitVals(Path.GetFileNameWithoutExtension(file), null, file, null);
            ValueBag = new Dictionary<string, string>();
        }

        public Tilesheet(EngineResourceFile resFile, string entryName)
        {
            ResourceIdentifier = new EngineResourceFileIdentifier(resFile, EngineResourceFileTypes.Bitmap, entryName);
            InitVals(entryName, null, null, ResourceIdentifier);
            ValueBag = new Dictionary<string, string>();
        }

        public Tilesheet(EngineResourceFile resFile, string entryName, string tilesheetName)
        {
            ResourceIdentifier = new EngineResourceFileIdentifier(resFile, EngineResourceFileTypes.Bitmap, entryName);
            InitVals(tilesheetName, null, null, ResourceIdentifier);
            ValueBag = new Dictionary<string, string>();
        }

        public Tilesheet(EngineResourceFileIdentifier resourceId)
        {
            ResourceIdentifier = resourceId;
            InitVals(resourceId.ResourceName, null, null, ResourceIdentifier);
            ValueBag = new Dictionary<string, string>();
        }

        public Tilesheet(EngineResourceFileIdentifier resourceId, string tilesheetName)
        {
            ResourceIdentifier = resourceId;
            InitVals(tilesheetName, null, null, ResourceIdentifier);
            ValueBag = new Dictionary<string, string>();
        }

        public Tilesheet(Tilesheet tilesheet, string name, string file)
        {
            this.InitialOffsetX = tilesheet.InitialOffsetX;
            this.InitialOffsetY = tilesheet.InitialOffsetY;
            this.XPixelsBetweenTiles = tilesheet.XPixelsBetweenTiles;
            this.YPixelsBetweenTiles = tilesheet.YPixelsBetweenTiles;
            this.Mask = tilesheet.Mask;
            this._tileSize = tilesheet._tileSize;
            this._extraTopSpace = tilesheet._extraTopSpace;
            this.ValueBag = new Dictionary<string, string>(tilesheet.ValueBag);

            InitVals(name, null, file, null);
        }

        ~Tilesheet()
        {
            Dispose();
        }

        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            // if Bitmap is not a physical file and there is no ResourceFile, create resource file so it can be serialized
            if (string.IsNullOrWhiteSpace(_imgFile) && ResourceIdentifier == null)
            {
                var resFile = new EngineResourceFile(string.Format("tilesheet_{0}.zip", this.Name), null, false);
                var converter = new ImageConverter();
                //resFile.Add(EngineResourceFileTypes.Bitmap, this.Name, (Stream)converter.ConvertTo(_bmp, typeof(Stream)));
                ResourceIdentifier = new EngineResourceFileIdentifier(resFile, EngineResourceFileTypes.Bitmap, this.Name);
            }
        }

        [OnDeserialized()]
        private void OnDeserialized(StreamingContext context)
        {
            InitVals(this.Name, null, this.ImageFilePath, this.ResourceIdentifier);

            if (ValueBag == null)
                ValueBag = new Dictionary<string, string>();
        }
        #endregion

        #region properties
        [IgnoreDataMember]
        public string Name
        {
            get { return _name; }
            set
            {
                Tilesheet._tilesheets.Remove(_name);
                _name = value;

                if (Tilesheet._tilesheets.ContainsKey(_name))
                    Tilesheet._tilesheets[_name] = this;
                else
                    Tilesheet._tilesheets.Add(_name, this);
            }
        }

        [IgnoreDataMember]
        public Bitmap Bmp
        {
            get { return _bmp; }
        }

        [DataMember]
        public string ImageFilePath
        {
            get { return _imgFile; }
            private set { _imgFile = value; }
        }

        /// <summary>
        /// size of individual tile on tile sheet
        /// </summary>
        [IgnoreDataMember]
        public Size TileSize
        {
            get { return _tileSize; }
            set
            {
                _tileSize = value;
                Tilesheet.RecalcMaxOverlapRatio();
            }
        }

        [IgnoreDataMember]
        public int ExtraTopSpace
        {
            get { return _extraTopSpace; }
            set
            {
                _extraTopSpace = value;
                Tilesheet.RecalcMaxOverlapRatio();
            }
        }

        [IgnoreDataMember]
        public int PrimaryHeight
        {
            get { return _tileSize.Height - _extraTopSpace; }
        }

        [IgnoreDataMember]
        public float ExtraTopSpaceToPrimaryRatio
        {
            get { return (float)_extraTopSpace / (float)PrimaryHeight; }
        }

        [IgnoreDataMember]
        public IntPtr hDC
        {
            get { return _hDC; }
        }

        [IgnoreDataMember]
        public string MaskName
        {
            get { return Mask == null ? string.Empty : Mask.Name; }
        }

        [DataMember]
        public Dictionary<string, string> ValueBag { get; set; }

        [DataMember]
        public EngineResourceFileIdentifier ResourceIdentifier { get; private set; }
        #endregion

        #region public methods
        public Rectangle GetSourceRange(int xTile, int yTile)
        {
            Point ptSrc = new Point();
            ptSrc.X = (xTile * (TileSize.Width + XPixelsBetweenTiles)) + InitialOffsetX;
            ptSrc.Y = (yTile * (TileSize.Height + YPixelsBetweenTiles)) + InitialOffsetY;

            return new Rectangle(ptSrc, TileSize);
        }

        public List<Frame> GetFrames()
        {
            var frames = new List<Frame>();

            int xTile = 0;
            int yTile = 0;

            var range = GetSourceRange(xTile, yTile);
            int x = range.X;
            int y = range.Y;

            while (y < _bmp.Height)
            {
                while (x < _bmp.Width)
                {
                    frames.Add(new Frame(this, xTile, yTile));
                    range = GetSourceRange(++xTile, yTile);
                    x = range.X;
                }

                xTile = 0;
                range = GetSourceRange(xTile, ++yTile);
                x = range.X;
                y = range.Y;
            }

            return frames;
        }

        public string SaveToFile()
        {
            string file = Environment.CurrentDirectory + @"\" + this.Name + "_"
                + HighResTimer.GetCurrentTickCount().ToString() + ".bmp";

            return SaveToFile(file);
        }

        public string SaveToFile(string file)
        {
            Bitmap toSave = new Bitmap(_bmp);
            Graphics graphics = Graphics.FromImage(toSave);

            IntPtr graphicsDC = graphics.GetHdc();
            Win32Support.DrawBitmap(graphicsDC, 0, 0, toSave.Width, toSave.Height,
                hDC, 0, 0, toSave.Width, toSave.Height, TernaryRasterOperations.SRCCOPY);
            graphics.ReleaseHdc(graphicsDC);

            toSave.Save(file);
            graphics.Dispose();
            toSave.Dispose();

            return file;
        }
        #endregion

        #region private / internal methods
        protected void InitVals(string name, Bitmap bmp, string bmpFilePath, EngineResourceFileIdentifier resourceId)
        {
            _name = name;

            if (bmp != null)
                _bmp = bmp;
            else
            {
                if (resourceId != null)
                    _bmp = new Bitmap(resourceId.Data);
                else
                {
                    _imgFile = bmpFilePath;
                    Bitmap tempBmp = new Bitmap(bmpFilePath);
                    _bmp = new Bitmap(tempBmp, tempBmp.Width, tempBmp.Height);
                    tempBmp.Dispose();
                }
            }

            CreateGDICompatibleBmp();

            if (Tilesheet._tilesheets.ContainsKey(_name))
                Tilesheet._tilesheets[_name].Dispose();

            Tilesheet._tilesheets.Add(_name, this);
            Tilesheet.RecalcMaxOverlapRatio();
        }

        private void CreateGDICompatibleBmp()
        {
            // get a Graphics object from the Bitmap
            _dc = Graphics.FromImage(_bmp);

            // get exclusive handle to the Graphics object
            _hDC = _dc.GetHdc();

            // create and get handle to a GDI bitmap object compatible with the GDI+ Bitmap
            _hBmp = _bmp.GetHbitmap();

            // associate the new bitmap handle with the Graphics handle
            pInvoke.SelectObject(_hDC, _hBmp);
        }
        #endregion

        #region IDisposable Members
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            Tilesheet._tilesheets.Remove(_name);
            Tilesheet.RecalcMaxOverlapRatio();
            pInvoke.DeleteObject(_hBmp);
            _dc.Dispose();
            _bmp.Dispose();

            if (Disposed != null)
                Disposed(new TilesheetDisposedEventArgs(this));
        }
        #endregion

        #region static members
        #region private / internal static fields
        /// <summary>
        /// Dictionary of Tilesheet objects
        /// </summary>
        internal static Dictionary<string, Tilesheet> _tilesheets = new Dictionary<string, Tilesheet>();
        #endregion

        #region public static methods
        /// <summary>
        /// Maximum ExtraTopSpaceToPrimaryRatio value over entire collection of instantiated <see cref="Tilesheet"/> objects.
        /// </summary>
        public static float MaxExtraTopSpaceRatio
        {
            get;
            internal set;
        }

        /// <summary>
        /// Returns a list of all <see cref="Tilesheet"/> Name values.
        /// </summary>
        /// <returns><see cref="List"/> of all <see cref="Tilesheet"/> Name values</returns>
        public static List<string> GetTilesheetKeys()
        {
            return new List<string>(_tilesheets.Keys);
        }

        /// <summary>
        /// Returns the currently instantiated <see cref="Tilesheet"/> object where the Name matches the bmpKey.
        /// </summary>
        /// <param name="bmpKey">Name of the <see cref="Tilesheet"/> object to return</param>
        /// <returns><see cref="Tilesheet"/> object where the Name matches the bmpKey</returns>
        public static Tilesheet GetTilesheet(string name)
        {
            if (_tilesheets.ContainsKey(name))
                return _tilesheets[name];
            else
                return null;
        }

        /// <summary>
        /// Dispose of the <see cref="Tilesheet"/> object with the Name that matches name.
        /// </summary>
        /// <param name="name">Name of <see cref="Tilesheet"/> object to Dispose</param>
        public static void ClearTilesheet(string name)
        {
            if (_tilesheets.ContainsKey(name))
                _tilesheets[name].Dispose();
        }

        /// <summary>
        /// Disposes all currently instantiated <see cref="Tilesheet"/> objects.
        /// </summary>
        public static void ClearAllTilesheets()
        {
            List<Tilesheet> tilesheets = new List<Tilesheet>(_tilesheets.Values);
            foreach (Tilesheet tilesheet in tilesheets)
                tilesheet.Dispose();
        }

        public static Color InferBackgroundColor(Bitmap bitmap)
        {
            // get max coordinate values for bitmap (Width and Height are 0-based)
            var maxX = bitmap.Width - 1;
            var maxY = bitmap.Height - 1;

            // get pixel color at 8 points along border of bitmap
            var colorUL = bitmap.GetPixel(0, 0);                // upper left
            var colorML = bitmap.GetPixel(0, maxY / 2);         // mid left
            var colorLL = bitmap.GetPixel(0, maxY);             // lower left
            var colorUM = bitmap.GetPixel(maxX / 2, 0);         // upper mid
            var colorUR = bitmap.GetPixel(maxX, 0);             // upper right
            var colorLM = bitmap.GetPixel(maxX / 2, maxY);      // lower mid
            var colorLR = bitmap.GetPixel(maxX, maxY);          // lower right
            var colorMR = bitmap.GetPixel(maxX, maxY / 2);      // mid right

            // get count of how many times each color was identified
            var colorCount = new Dictionary<Color, int>();

            if (!colorCount.ContainsKey(colorUL))
                colorCount.Add(colorUL, 0);

            if (!colorCount.ContainsKey(colorML))
                colorCount.Add(colorML, 0);

            if (!colorCount.ContainsKey(colorLL))
                colorCount.Add(colorLL, 0);

            if (!colorCount.ContainsKey(colorUM))
                colorCount.Add(colorUM, 0);

            if (!colorCount.ContainsKey(colorUR))
                colorCount.Add(colorUR, 0);

            if (!colorCount.ContainsKey(colorLM))
                colorCount.Add(colorLM, 0);

            if (!colorCount.ContainsKey(colorLR))
                colorCount.Add(colorLR, 0);

            if (!colorCount.ContainsKey(colorMR))
                colorCount.Add(colorMR, 0);

            colorCount[colorUL]++;
            colorCount[colorML]++;
            colorCount[colorLL]++;
            colorCount[colorUM]++;
            colorCount[colorUR]++;
            colorCount[colorLM]++;
            colorCount[colorLR]++;
            colorCount[colorMR]++;

            // return color most frequently found
            var color = new List<Color>(colorCount.OrderByDescending(col => col.Value)
                                                  .Select(col => col.Key)
                                                  .Take(1));

            return color[0];
        }

        public static Bitmap RemapColor(Bitmap image, Color oldColor, Color newColor)
        {
            var newImage = new Bitmap(image);

            var graphics = Graphics.FromImage(newImage);
            var imageAttributes = new ImageAttributes();

            var colorMap = new ColorMap();
            colorMap.OldColor = oldColor;
            colorMap.NewColor = newColor;

            ColorMap[] remapTable = { colorMap };
            imageAttributes.SetRemapTable(remapTable, ColorAdjustType.Bitmap);

            graphics.DrawImage(newImage, new Rectangle(0, 0, newImage.Width, newImage.Height),
                               0, 0, newImage.Width, newImage.Height,
                               GraphicsUnit.Pixel, imageAttributes);
            graphics.Dispose();

            return newImage;
        }

        public static Bitmap CreateMask(Bitmap image)
        {
            return CreateMask(image, InferBackgroundColor(image));
        }

        public static Bitmap CreateMask(Bitmap image, Color transparentColor)
        {
            var mask = new Bitmap(image.Width, image.Height);

            for (int y = 0; y <= image.Height - 1; y++)
            {
                for (int x = 0; x <= image.Width - 1; x++)
                {
                    var pixel = image.GetPixel(x, y);

                    if (pixel.R == transparentColor.R &&
                        pixel.G == transparentColor.G &&
                        pixel.B == transparentColor.B &&
                        pixel.A == transparentColor.A)
                        mask.SetPixel(x, y, Color.White);
                    else
                        mask.SetPixel(x, y, Color.Black);
                }
            }

            return mask;
        }
        #endregion

        #region public static properties
        /// <summary>
        /// Returns a <see cref="List"/> of all currently instantiated <see cref="Tilesheet"/> objects.
        /// </summary>
        public static List<Tilesheet> AllTilesheets
        {
            get { return new List<Tilesheet>(_tilesheets.Values); }
        }

        /// <summary>
        /// Returns the count of the total number of currently instantiated <see cref="Tilesheet"/> objects.
        /// </summary>
        public static int Count
        {
            get { return _tilesheets.Count; }
        }
        #endregion

        #region private static methods
        private static void RecalcMaxOverlapRatio()
        {
            MaxExtraTopSpaceRatio = 0;

            foreach (Tilesheet tilesheet in _tilesheets.Values)
            {
                if (tilesheet.ExtraTopSpaceToPrimaryRatio > MaxExtraTopSpaceRatio)
                    MaxExtraTopSpaceRatio = tilesheet.ExtraTopSpaceToPrimaryRatio;
            }
        }
        #endregion
        #endregion
    }
}
