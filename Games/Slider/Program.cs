using System;
using System.Windows.Forms;

namespace Slider
{
    internal static class Program
    {
        internal static Puzzle puzzle = null;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PuzzleForm());
        }
    }
}