using Gondwana.Common.EventArgs;
using System;

namespace Gondwana.Common.Drawing.Animation
{
    public class Animator : IDisposable
    {
        #region events
        public event AnimatorEventHandler Started;
        public event AnimatorEventHandler Stopped;
        public event AnimatorEventHandler Cycled;
        #endregion events

        #region private/internal fields
        private Tile parent;
        private bool cycling = false;
        private long LastTick = HighResTimer.GetCurrentTickCount();
        #endregion private/internal fields

        #region constructors / finalizer
        protected internal Animator(Tile tile)
        {
            parent = tile;
        }

        ~Animator()
        {
            Dispose();
        }
        #endregion constructors / finalizer

        #region properties
        public Tile Parent
        {
            get { return parent; }
        }

        public Cycle CurrentCycle { get; set; }

        public bool IsCycling
        {
            get { return cycling; }
            set
            {
                if (cycling != value)
                {
                    if (value == true)
                        StartAnimation();
                    else
                        StopAnimation();
                }
            }
        }
        #endregion properties

        #region public methods
        public Cycle SetCurrentCycle(string cycleKey)
        {
            CurrentCycle = Cycle.GetAnimationCycle(cycleKey);
            return CurrentCycle;
        }

        public void StartAnimation()
        {
            if (CurrentCycle != null)
            {
                cycling = true;

                if (Tile.TilesAnimating.IndexOf(parent) == -1)
                    Tile.TilesAnimating.Add(parent);

                if (Started != null)
                    Started(new AnimatorEventArgs(parent, this));
            }
        }

        public void StartAnimation(string cycleKey)
        {
            CurrentCycle = Cycle.GetAnimationCycle(cycleKey);
            StartAnimation();
        }

        public void StopAnimation()
        {
            // only perform action if actually cycling
            if (cycling == true)
            {
                cycling = false;
                CurrentCycle.Sequence.StopCycle();

                Tile.TilesAnimating.Remove(parent);

                if (Stopped != null)
                    Stopped(new AnimatorEventArgs(parent, this));

                if (CurrentCycle.NextCycle != null)
                    this.CurrentCycle = CurrentCycle.NextCycle;
                else    // there is no next cycle, so make Visible = false
                    parent.Visible = false;
            }
        }

        public void CycleAnimation(long currentTick)
        {
            // if throttle is 0, stop the animation so loop doesn't hang
            if (CurrentCycle._throttle == 0)
            {
                StopAnimation();
                return;
            }

            //if "throttle" time has passed
            while (currentTick >= LastTick + CurrentCycle._throttle)
            {
                // capture the LastTick value
                LastTick += CurrentCycle._throttle;

                // do not change image if animation is paused
                if (parent.PauseAnimation)
                    return;

                // cycle the frame and set the sprite image; this will add it to RefreshQueue
                parent.CurrentFrame = CurrentCycle.Sequence.AdvanceFrame();

                // raise the event
                if (Cycled != null)
                    Cycled(new AnimatorEventArgs(parent, this));

                // if terminating cycle is done, stop the animation
                if (CurrentCycle.Sequence.CycleFinished)
                    StopAnimation();
            }
        }
        #endregion

        #region IDisposable Members
        public void Dispose()
        {
            Started = null;
            Stopped = null;
            Cycled = null;
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}