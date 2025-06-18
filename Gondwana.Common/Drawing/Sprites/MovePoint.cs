using Gondwana.Common;
using Gondwana.Timers;

namespace Gondwana.Drawing.Sprites
{
    public class MovePoint
    {
        private Sprite parent;

        internal PointF origCoord;
        internal Size origSize;
        internal long TotalTicks;
        internal long TotalTicksRunning;
        internal MovePoint nextMovePoint;

        public readonly PointF DestCoord;
        public readonly Size DestSize;

        protected internal MovePoint(Sprite sprite, double totalTime, PointF destCoord, Size destSize)
        {
            parent = sprite;
            TotalTicks = (long)(totalTime * (double)HighResTimer.TicksPerSecond);
            TotalTicksRunning = 0;
            DestCoord = destCoord;
            DestSize = destSize;
        }

        protected internal MovePoint(Sprite sprite, double totalTime, Rectangle destLoc)
        {
            parent = sprite;
            TotalTicks = (long)(totalTime * (double)HighResTimer.TicksPerSecond);
            TotalTicksRunning = 0;
            DestCoord = Sprites.GridCoordinates(sprite, sprite.ParentGrid, destLoc);
            DestSize = new Size(destLoc.Width, destLoc.Height);
        }

        protected internal void InitializeMovePoint()
        {
            TotalTicksRunning = 0;
            origCoord = parent.GridCoordinates;
            origSize = parent.RenderSize;
        }

        public MovePoint NextMovePoint
        {
            get { return nextMovePoint; }
        }

        public long TicksUntilCompletion
        {
            get
            {
                long timeRemain = TotalTicks - TotalTicksRunning;
                if (timeRemain <= 0)
                    return 0;
                else
                    return timeRemain;
            }
        }

        public double SecondsUntilCompletion
        {
            get { return (double)TicksUntilCompletion / (double)HighResTimer.TicksPerSecond; }
        }

        public Rectangle DestDrawLocation
        {
            get { return Sprites.DrawLocation(parent, parent.ParentGrid, DestCoord, DestSize); }
        }
    }
}
