using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using System.Drawing;

namespace Gondwana.Common.Drawing.Direct
{
    public static class DirectDrawings
    {
        //internal static List<DirectDrawing> drawInstances = new List<DirectDrawing>();

        public static void RenderAll()
        {
            DirectDrawing._instances.Sort();

            foreach (DirectDrawing drawing in DirectDrawing._instances)
            {
                if (drawing.Bounds.IntersectsWith(drawing.Surface.Buffer.DirtyRectangle))
                {
                    if (drawing._dirty)
                    {
                        drawing.Render();
                        drawing._dirty = false;
                    }
                }
            }
        }

        public static void Clear()
        {
            for (int i = 0; i < DirectDrawing._instances.Count; i++)
                DirectDrawing._instances[i].Dispose();
        }

        public static void Clear(string name)
        {
            DirectDrawing tmpDraw = GetDirectDrawing(name);

            if (tmpDraw != null)
                tmpDraw.Dispose();
        }

        public static List<DirectDrawing> AllDirectDrawings
        {
            get { return DirectDrawing._instances; }
        }

        public static int Count
        {
            get { return DirectDrawing._instances.Count; }
        }

        public static DirectDrawing GetDirectDrawing(string name)
        {
            foreach (DirectDrawing drawing in DirectDrawing._instances)
            {
                if (drawing.Name == name)
                    return drawing;
            }

            return null;
        }
    }
}
