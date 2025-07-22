using Gondwana.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gondwana.Design
{
    static class Program
    {
        internal static DesignerState State = null;
        internal static bool AppIsClosing = false;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Program.State = new DesignerState(args);

            Application.Run(new ScriptDesigner());

            AppIsClosing = true;

            if (State != null)
                State.Dispose();
        }
    }
}
