using Gondwana;
using Gondwana.Media;
using System;
using System.Windows.Forms;

namespace Slider
{
    static class Program
    {
        internal static Puzzle puzzle = null;
        internal static MediaFile slideSound;
        internal static MediaFile tadaSound;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PuzzleForm());
        }
    }
}