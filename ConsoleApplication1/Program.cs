using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gondwana.Common.Drawing;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            Bitmap bmp = new Bitmap(@"C:\test\shops 2 tileset.bmp");
            
            //Bitmap bmpMask = Tilesheet.CreateBitmapMask(ref bmp);
            //bmpMask.Save(@"C:\test\shops 2 tileset mask.bmp");
            

            var transparentCol = Tilesheet.InferBackgroundColor(bmp);
            var black = Color.Black;

            
            //RemapColor(bmp, transparentCol, black);
            //bmp.Save(@"C:\test\shops 2 tileset new.bmp");

            var mask = CreateMask(bmp, Tilesheet.InferBackgroundColor(bmp));
            mask.Save(@"C:\test\shops 2 tileset new mask.bmp");

            mask.Dispose();
            bmp.Dispose();
        }

        private static void RemapColor(Bitmap image, Color oldColor, Color newColor)
        {
            var graphics = Graphics.FromImage(image);
            var imageAttributes = new ImageAttributes();

            var colorMap = new ColorMap();
            colorMap.OldColor = oldColor;
            colorMap.NewColor = newColor;

            ColorMap[] remapTable = { colorMap };
            imageAttributes.SetRemapTable(remapTable, ColorAdjustType.Bitmap);

            graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                               0, 0, image.Width, image.Height,
                               GraphicsUnit.Pixel, imageAttributes);
            graphics.Dispose();
        }

        private static Bitmap CreateMask(Bitmap image, Color transparentColor)
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
    }
}
