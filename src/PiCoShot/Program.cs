using System;
using System.Threading;
using System.Windows.Forms;

namespace PiCoShot
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\PiCoShot.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    Settings.Load();
                    L.Init(Settings.Language);
                    TrayApp.NotifyAlreadyRunning();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => Log.Error(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Error(e.ExceptionObject as Exception);

                Settings.Load();
                L.Init(Settings.Language);
                Application.Run(new TrayApp());
            }
        }
    }
}
