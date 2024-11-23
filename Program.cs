using System;
using System.Threading;
using System.Windows.Forms;

namespace LXLauncher
{
    internal static class Program
    {
        private static Mutex _mutex;

        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            const string mutexName = "Lightning-X-Mutex";

            _mutex = new Mutex(true, mutexName, out var isNewInstance);

            if (!isNewInstance)
            {
                MessageBox.Show(@"The application is already running.", @"LXLauncher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return; // Exit the application
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LxLauncher());

            // Ensure the mutex is released when the application exits
            GC.KeepAlive(_mutex);
        }
    }
}