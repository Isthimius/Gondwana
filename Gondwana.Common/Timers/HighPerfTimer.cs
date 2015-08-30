using System.Runtime.InteropServices;

namespace Gondwana.Common
{
    public static class HighResTimer
    {
        #region Win32 p/invoke
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern int GetTickCount();

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);
        #endregion

        #region properties
        public static long TicksPerSecond { get; private set; }
        public static bool HighPerfSupported { get; private set; }
        #endregion

        #region ctor
        /// <summary>
        /// Static constructor that gets the system QueryPerformanceFrequency;
        /// QueryPerformanceCounter is used for Engine cycling.
        /// If high performance counter is not available GetTickCount is used for cycling.
        /// </summary>
        static HighResTimer()
        {
            long freq;
            HighPerfSupported = QueryPerformanceFrequency(out freq);

            // use HighResTimer.GetCurrentTickCount() if high perf not supported
            if (HighPerfSupported)
            {
                TicksPerSecond = freq;
                GetTick = HiResTickCount;
            }
            else
            {
                TicksPerSecond = 1000;
                GetTick = LowResTickCount;
            }
        }
        #endregion

        #region public methods
        public static long GetCurrentTickCount()
        {
            return GetTick();
        }

        public static double GetDuration(long start, long stop)
        {
            return (double)(stop - start) / (double)TicksPerSecond;
        }
        #endregion

        #region private static methods
        private delegate long GetTickDel();
        private static GetTickDel GetTick;

        private static long LowResTickCount()
        {
            return (long)GetTickCount();
        }

        private static long HiResTickCount()
        {
            long time;
            QueryPerformanceCounter(out time);
            return time;
        }
        #endregion
    }
}
